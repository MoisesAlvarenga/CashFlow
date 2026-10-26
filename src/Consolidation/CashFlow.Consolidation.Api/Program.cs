using System.Reflection;
using CashFlow.Consolidation.Api.Context;
using CashFlow.Consolidation.Api.Endpoints;
using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Shared.Contracts.Context;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Consolidation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using CashFlow.Consolidation.Api.Workers;
using CashFlow.Shared.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

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
});

// Persistence & Database: PostgreSQL Code-First via Npgsql with automatic InMemory fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
builder.Services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
builder.Services.AddScoped<IConsumedMessageRepository, ConsumedMessageRepository>();
builder.Services.AddScoped<IConsolidationUnitOfWork, ConsolidationUnitOfWork>();
builder.Services.AddScoped<IDailyBalanceService, DailyBalanceService>();

// RabbitMQ Messaging Configuration & Background Consumer
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<TransactionRegisteredConsumer>();

var app = builder.Build();

// Automatic Database Migration on startup (EF Core Code-First)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
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

// Map Minimal API Endpoints via extension method
app.ConsolidationAPI();

await app.RunAsync();
