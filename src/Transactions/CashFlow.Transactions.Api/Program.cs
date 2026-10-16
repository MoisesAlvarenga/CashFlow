using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Validators;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using CashFlow.Transactions.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();

// Persistence & Database (EF Core InMemory for portable zero-config dev/test, swappable for PostgreSQL in production)
builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseInMemoryDatabase("CashFlow_Transactions"));

// Dependency Inversion: Register domain repositories and application handlers
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IValidator<RegisterTransactionCommand>, RegisterTransactionCommandValidator>();
builder.Services.AddScoped<IRegisterTransactionCommandHandler, RegisterTransactionCommandHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// REST API Endpoints
app.MapPost("/api/v1/transactions", async (
    RegisterTransactionCommand command,
    IRegisterTransactionCommandHandler handler,
    CancellationToken ct) =>
{
    try
    {
        var result = await handler.HandleAsync(command, ct);
        return Results.Created($"/api/v1/transactions/{result.TransactionId}", result);
    }
    catch (ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
})
.WithName("RegisterTransaction")
.WithSummary("Registra um lançamento de crédito ou débito no fluxo de caixa com Transactional Outbox");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Transactions.Api" }))
   .WithName("HealthCheck");

await app.RunAsync();
