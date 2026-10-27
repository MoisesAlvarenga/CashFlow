using System.Reflection;
using System.Security.Claims;
using System.Text;
using CashFlow.Consolidation.Api.Context;
using CashFlow.Consolidation.Api.Endpoints;
using CashFlow.Consolidation.Api.Workers;
using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Consolidation.Infrastructure.Caching;
using CashFlow.Consolidation.Infrastructure.Repositories;
using CashFlow.Consolidation.Infrastructure.Services;
using CashFlow.Shared.Contracts.Context;
using CashFlow.Shared.Contracts.Diagnostics;
using CashFlow.Shared.Contracts.Messaging;
using CashFlow.Shared.Contracts.Security;
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
        serviceName: TelemetryConstants.ConsolidationServiceName,
        serviceVersion: TelemetryConstants.ServiceVersion))
    .WithTracing(tracing => tracing
        .AddSource(TelemetryConstants.ConsolidationServiceName)
        .AddAspNetCoreInstrumentation(opts => opts.RecordException = true))
    .WithMetrics(metrics => metrics
        .AddMeter(TelemetryConstants.ConsolidationServiceName));

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CashFlow - Consolidation API",
        Version = "v1",
        Description = "API RESTful do microsserviço de Consolidação de Saldo Diário de Fluxo de Caixa. Responsável pelo cálculo e agregação contínua de créditos, débitos e saldo líquido diário, garantindo desacoplamento e autonomia em relação ao serviço de transações.",
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
        typeof(DailyBalanceDto).Assembly,
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
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

builder.Services.AddAuthorization();

// Persistence & Database: PostgreSQL Code-First via Npgsql with automatic InMemory fallback
var connectionString = builder.Configuration.GetConnectionString("consolidationdb")
    ?? builder.Configuration.GetConnectionString("transactionsdb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ConsolidationDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(ConsolidationDbContext).Assembly.FullName);
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Consolidation");
        });
    }
    else
    {
        options.UseInMemoryDatabase("CashFlow_Consolidation");
    }
});

// Context & Cancellation: Decoupled CancellationToken Provider from HttpContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICancellationTokenProvider, HttpCancellationTokenProvider>();

// Dependency Inversion: Register domain repositories and application services
builder.Services.AddSingleton<ISecretHasher, SecretHasher>();
builder.Services.AddScoped<IClientAuthService, ConsolidationClientAuthService>();
builder.Services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
builder.Services.AddScoped<IConsumedMessageRepository, ConsumedMessageRepository>();
builder.Services.AddScoped<IConsolidationUnitOfWork, ConsolidationUnitOfWork>();
builder.Services.AddScoped<IDailyBalanceService, DailyBalanceService>();

// Redis Distributed Cache Configuration (EVO-02)
var redisConnectionString = builder.Configuration.GetConnectionString("redis")
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "CashFlow:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSingleton<IDailyBalanceCacheService, RedisDailyBalanceCacheService>();

// RabbitMQ Messaging Configuration & Background Consumer
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<TransactionRegisteredConsumer>();

var app = builder.Build();

// Automatic Database Migration and Client Seeding on startup (EF Core Code-First)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    var hasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
    await ApiClientSeeder.SeedAsync(dbContext, hasher);
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Consolidation API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "CashFlow - Consolidation API Documentation";
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map Minimal API Endpoints via extension method
app.ConsolidationAPI();

// Map default health and liveness endpoints for Aspire
app.MapDefaultEndpoints();

await app.RunAsync();

namespace CashFlow.Consolidation.Api
{
    public partial class Program { }
}
