using System.Reflection;
using CashFlow.Shared.Contracts.Context;
using CashFlow.Transactions.Api.Context;
using CashFlow.Transactions.Api.Endpoints;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.DTOs;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Queries;
using CashFlow.Transactions.Application.Validators;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using CashFlow.Transactions.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using CashFlow.Transactions.Api.Workers;
using CashFlow.Shared.Contracts.Messaging;
using CashFlow.Transactions.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

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
});

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

// Map Minimal API Endpoints via extension method
app.TransactionAPI();

await app.RunAsync();
