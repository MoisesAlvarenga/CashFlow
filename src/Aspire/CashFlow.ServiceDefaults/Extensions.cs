namespace Microsoft.Extensions.Hosting;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

/// <summary>
/// Métodos de extensão para configuração de padrões de resiliência, telemetria e health checks
/// compartilhados via .NET Aspire em todos os microsserviços da solução.
/// </summary>
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Adiciona resiliência padrão para chamadas HTTP
            http.AddStandardResilienceHandler();

            // Adiciona service discovery para resolução de nomes de microsserviços
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        var healthChecks = builder.Services.AddHealthChecks()
            // Verificação básica de vivacidade (Liveness)
            .AddCheck("self", () => HealthCheckResult.Healthy("Serviço operacional."), ["live"]);

        // Verificação profunda do PostgreSQL (Readiness)
        var pgConnString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? builder.Configuration.GetConnectionString("transactionsdb")
            ?? builder.Configuration.GetConnectionString("consolidationdb")
            ?? builder.Configuration.GetConnectionString("cashflow_db");

        if (!string.IsNullOrWhiteSpace(pgConnString))
        {
            healthChecks.AddNpgSql(
                connectionString: pgConnString,
                name: "postgresql",
                tags: ["ready", "db"]);
        }

        // Verificação profunda do RabbitMQ (Readiness)
        var rabbitSection = builder.Configuration.GetSection("RabbitMq");
        var host = rabbitSection["Host"] ?? "localhost";
        var portStr = rabbitSection["Port"] ?? "5672";
        var user = rabbitSection["Username"] ?? "verity";
        var pass = rabbitSection["Password"] ?? "verity_secret";

        if (!int.TryParse(portStr, out var port))
        {
            port = 5672;
        }

        healthChecks.AddAsyncCheck("rabbitmq", async (cancellationToken) =>
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = host,
                    Port = port,
                    UserName = user,
                    Password = pass,
                    RequestedHeartbeat = TimeSpan.FromSeconds(5)
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                await using var connection = await factory.CreateConnectionAsync(cts.Token);
                if (connection.IsOpen)
                {
                    return HealthCheckResult.Healthy("Broker RabbitMQ conectado e operacional.");
                }

                return HealthCheckResult.Unhealthy("Conexão com o broker RabbitMQ não está aberta.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Broker RabbitMQ inacessível: {ex.Message}", ex);
            }
        }, tags: ["ready", "broker"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Endpoint detalhado de prontidão (Readiness) com diagnóstico JSON completo
        var healthOptions = new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteHealthResponseAsync
        };
        app.MapHealthChecks("/health", healthOptions);
        app.MapHealthChecks("/health/ready", healthOptions);

        // Endpoint leve de vivacidade (Liveness)
        var aliveOptions = new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        };
        app.MapHealthChecks("/alive", aliveOptions);
        app.MapHealthChecks("/health/live", aliveOptions);

        return app;
    }

    private static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    duration = entry.Value.Duration.ToString(),
                    description = entry.Value.Description,
                    data = entry.Value.Data,
                    error = entry.Value.Exception?.Message
                })
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, response, jsonOptions);
    }
}
