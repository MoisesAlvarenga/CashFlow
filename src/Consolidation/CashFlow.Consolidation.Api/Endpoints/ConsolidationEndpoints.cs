namespace CashFlow.Consolidation.Api.Endpoints;

using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Application.Services;

/// <summary>
/// Métodos de extensão para mapeamento dos endpoints Minimal API de Consolidação.
/// </summary>
public static class ConsolidationEndpoints
{
    /// <summary>
    /// Registra todos os endpoints do microsserviço de Consolidação de Saldo Diário (Consolidation API).
    /// </summary>
    /// <param name="app">A instância de WebApplication do pipeline HTTP.</param>
    /// <returns>A mesma instância de WebApplication para encadeamento fluente.</returns>
    public static WebApplication ConsolidationAPI(this WebApplication app)
    {
        return app.MapConsolidationEndpoints();
    }

    /// <summary>
    /// Alias fluente no padrão Map* para registro de rotas de Consolidação.
    /// </summary>
    public static WebApplication MapConsolidationEndpoints(this WebApplication app)
    {
        // Redirecionamento da raiz para o Swagger UI
        app.MapGet("/", () => Results.Redirect("/swagger"))
           .ExcludeFromDescription();

        // GET /api/v1/daily-balances - Consultar saldo diário consolidado
        app.MapGet("/api/v1/daily-balances", async (
            DateOnly? date,
            IDailyBalanceService balanceService) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var balance = await balanceService.GetDailyBalanceAsync(targetDate);

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


        // GET /health - Diagnóstico operacional
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Consolidation.Api", timestamp = DateTimeOffset.UtcNow }))
           .WithName("HealthCheck")
           .WithTags("Health")
           .WithSummary("Verifica a saúde operacional do serviço de Consolidação")
           .WithDescription("Retorna 200 OK com o estado do serviço e timestamp atual.")
           .Produces(StatusCodes.Status200OK);

        return app;
    }
}
