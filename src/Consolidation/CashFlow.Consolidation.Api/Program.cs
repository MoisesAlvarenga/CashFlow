using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Consolidation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();

// Persistence & Database (EF Core InMemory for portable zero-config dev/test, swappable for PostgreSQL in production)
builder.Services.AddDbContext<ConsolidationDbContext>(options =>
    options.UseInMemoryDatabase("CashFlow_Consolidation"));

// Dependency Inversion: Register domain repositories and application services
builder.Services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
builder.Services.AddScoped<IDailyBalanceService, DailyBalanceService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// REST API Endpoints
app.MapGet("/api/v1/daily-balances", async (
    DateOnly? date,
    IDailyBalanceService balanceService,
    CancellationToken ct) =>
{
    var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var balance = await balanceService.GetDailyBalanceAsync(targetDate, ct);

    if (balance == null)
    {
        return Results.NotFound(new
        {
            error = "NotFound",
            message = $"Nenhum saldo consolidado encontrado para a data {targetDate:yyyy-MM-dd}."
        });
    }

    return Results.Ok(balance);
})
.WithName("GetDailyBalance")
.WithSummary("Consulta o saldo diário consolidado (total de créditos, débitos e saldo líquido) por data");

app.MapPost("/api/v1/daily-balances/process", async (
    ProcessTransactionRequest request,
    IDailyBalanceService balanceService,
    CancellationToken ct) =>
{
    try
    {
        var result = await balanceService.ProcessTransactionAsync(
            request.Type,
            request.Amount,
            request.Date,
            ct);

        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ProcessTransactionEvent")
.WithSummary("Processa um evento de lançamento para atualizar o saldo consolidado do dia");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Consolidation.Api" }))
   .WithName("HealthCheck");

await app.RunAsync();

public record ProcessTransactionRequest(string Type, decimal Amount, DateOnly Date);
