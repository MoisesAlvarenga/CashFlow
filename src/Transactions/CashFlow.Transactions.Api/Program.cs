using System.Reflection;
using System.Text;
using CashFlow.Shared.Contracts.Context;
using CashFlow.Shared.Contracts.Diagnostics;
using CashFlow.Shared.Contracts.Messaging;
using CashFlow.Shared.Contracts.Security;
using CashFlow.Transactions.Api.Context;
using CashFlow.Transactions.Api.Endpoints;
using CashFlow.Transactions.Api.Messaging;
using CashFlow.Transactions.Api.Workers;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.DTOs;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Queries;
using CashFlow.Transactions.Application.Validators;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using CashFlow.Transactions.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// .NET Aspire Service Defaults: telemetria OTLP, health checks, service discovery e resiliência
builder.AddServiceDefaults();

// Observabilidade padronizada com OpenTelemetry (Distributed Tracing + Métricas + Exportador OTLP)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: TelemetryConstants.TransactionsServiceName,
        serviceVersion: TelemetryConstants.ServiceVersion))
    .WithTracing(tracing => tracing
        .AddSource(TelemetryConstants.TransactionsServiceName)
        .AddAspNetCoreInstrumentation(opts => opts.RecordException = true))
    .WithMetrics(metrics => metrics
        .AddMeter(TelemetryConstants.TransactionsServiceName));

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CashFlow - Transactions API",
        Version = "v1",
        Description = "API RESTful do microsserviço de Lançamentos de Fluxo de Caixa. Responsável pelo registro transacional de créditos e débitos com alta resiliência e implementação do padrão Transactional Outbox para publicação confiável de eventos.",
        Contact = new OpenApiContact
        {
            Name = "Equipe de Arquitetura & Engenharia Verity",
            Email = "contato@verity.com.br"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License"
        }
    });

    // Ingest XML comments from API, Application, and Shared Contracts assemblies
    var docAssemblies = new[]
    {
        Assembly.GetExecutingAssembly(),
        typeof(RegisterTransactionCommand).Assembly,
        typeof(CashFlow.Shared.Contracts.Events.TransactionType).Assembly
    };

    foreach (var asm in docAssemblies)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{asm.GetName().Name}.xml");
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }

    // Configuração de segurança JWT Bearer no Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT gerado via POST /api/v1/auth/token no formato: Bearer {seu_token}"
    });

    c.AddSecurityRequirement(document => new()
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Authentication & Authorization: JWT Bearer Token validation
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Persistence & Database: PostgreSQL Code-First via Npgsql with automatic InMemory fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TransactionsDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(TransactionsDbContext).Assembly.FullName);
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Transactions");
        });
    }
    else
    {
        options.UseInMemoryDatabase("CashFlow_Transactions");
    }
});

// Context & Cancellation: Decoupled CancellationToken Provider from HttpContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICancellationTokenProvider, HttpCancellationTokenProvider>();

// Dependency Inversion: Register domain repositories and application handlers
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<ITransactionsUnitOfWork, TransactionsUnitOfWork>();
builder.Services.AddScoped<IValidator<RegisterTransactionCommand>, RegisterTransactionCommandValidator>();
builder.Services.AddScoped<IRegisterTransactionCommandHandler, RegisterTransactionCommandHandler>();
builder.Services.AddScoped<IGetTransactionsQueryHandler, GetTransactionsQueryHandler>();

// RabbitMQ Messaging Configuration
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// Outbox Background Processor (Configurable interval via appsettings / env vars)
builder.Services.Configure<OutboxProcessorOptions>(
    builder.Configuration.GetSection(OutboxProcessorOptions.SectionName));
builder.Services.AddHostedService<OutboxProcessorWorker>();

var app = builder.Build();

// Automatic Database Migration on startup (EF Core Code-First)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transactions API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "CashFlow - Transactions API Documentation";
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map Minimal API Endpoints via extension method
app.TransactionAPI();

// Map default health and liveness endpoints for Aspire
app.MapDefaultEndpoints();

await app.RunAsync();

namespace CashFlow.Transactions.Api
{
    public partial class Program { }
}
