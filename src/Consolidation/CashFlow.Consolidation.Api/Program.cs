using System.Reflection;
using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Consolidation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

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
            Email = "contato@verity.com.br",
            Url = new Uri("https://github.com/verity")
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
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

// Persistence & Database (EF Core InMemory for portable zero-config dev/test, swappable for PostgreSQL in production)
builder.Services.AddDbContext<ConsolidationDbContext>(options =>
    options.UseInMemoryDatabase("CashFlow_Consolidation"));

// Dependency Inversion: Register domain repositories and application services
builder.Services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
builder.Services.AddScoped<IDailyBalanceService, DailyBalanceService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Consolidation API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "CashFlow - Consolidation API Documentation";
});

app.UseHttpsRedirection();

// Redirect root to Swagger UI for quick developer access
app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

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
.WithTags("Consolidation")
.WithSummary("Consulta o saldo diário consolidado por data")
.WithDescription("Retorna o resumo consolidado contendo total de créditos, total de débitos, saldo líquido e total de transações processadas para o dia especificado. Se 'date' não for informado, assume a data atual em UTC.")
.Produces<DailyBalanceDto>(StatusCodes.Status200OK, contentType: "application/json")
.ProducesProblem(StatusCodes.Status404NotFound);

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
.WithTags("Consolidation")
.WithSummary("Processa um evento de transação e atualiza o saldo diário")
.WithDescription("Recebe os dados do lançamento financeiro, atualiza os acumuladores (crédito/débito) e recalcula o saldo líquido diário de forma atômica.")
.Produces<DailyBalanceDto>(StatusCodes.Status200OK, contentType: "application/json")
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Consolidation.Api", timestamp = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health")
   .WithSummary("Verifica a saúde operacional do serviço de Consolidação")
   .WithDescription("Retorna 200 OK com o estado do serviço e timestamp atual.")
   .Produces(StatusCodes.Status200OK);

await app.RunAsync();

/// <summary>
/// Requisição para processamento de transação e consolidação de saldo diário.
/// </summary>
/// <param name="Type">Tipo de lançamento financeiro: "Credit" para crédito ou "Debit" para débito.</param>
/// <param name="Amount">Valor monetário do lançamento (positivo).</param>
/// <param name="Date">Data contábil do lançamento financeiro (YYYY-MM-DD).</param>
public record ProcessTransactionRequest(string Type, decimal Amount, DateOnly Date);
