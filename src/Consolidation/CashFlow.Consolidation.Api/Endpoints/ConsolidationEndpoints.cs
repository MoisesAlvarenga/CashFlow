namespace CashFlow.Consolidation.Api.Endpoints;

using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Shared.Contracts.Security;

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

        // POST /api/v1/auth/token - Obter token JWT para acesso autenticado
        app.MapPost("/api/v1/auth/token", async (
            TokenRequest request,
            IJwtTokenService tokenService,
            IClientAuthService authService) =>
        {
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return Results.BadRequest(new { error = "ClientId e ClientSecret são obrigatórios." });
            }

            var client = await authService.ValidateCredentialsAsync(request.ClientId.Trim(), request.ClientSecret);
            if (client == null)
            {
                return Results.Unauthorized();
            }

            var roles = client.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tokenResponse = tokenService.GenerateToken(client.ClientId, roles);
            return Results.Ok(tokenResponse);
        })
        .WithName("GenerateToken")
        .WithTags("Auth")
        .WithSummary("Gera um token JWT Bearer autenticando ClientId e ClientSecret contra o banco de dados")
        .WithDescription("Emite um token JWT assinado válido por 60 minutos caso as credenciais do cliente estejam cadastradas e ativas no banco de dados. Credenciais padrão para avaliação: 'verity-client' / 'verity-secret'.")
        .Produces<TokenResponse>(StatusCodes.Status200OK, contentType: "application/json")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();

        // GET /api/v1/daily-balances - Consultar saldo diário consolidado (query parameter)
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
        .WithDescription("Retorna o resumo consolidado contendo total de créditos, total de débitos, saldo líquido e total de transações processadas para o dia especificado. Se 'date' não for informado, assume a data atual em UTC. Requer autenticação JWT.")
        .Produces<DailyBalanceDto>(StatusCodes.Status200OK, contentType: "application/json")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(policy => policy.RequireRole("Viewer", "Operator", "Admin"));

        // GET /api/v1/daily-balances/{date} - Consultar saldo diário consolidado (route parameter)
        app.MapGet("/api/v1/daily-balances/{date}", async (
            DateOnly date,
            IDailyBalanceService balanceService) =>
        {
            var balance = await balanceService.GetDailyBalanceAsync(date);

            if (balance == null)
            {
                return Results.NotFound(new
                {
                    error = "NotFound",
                    message = $"Nenhum saldo consolidado encontrado para a data {date:yyyy-MM-dd}."
                });
            }

            return Results.Ok(balance);
        })
        .WithName("GetDailyBalanceByRouteDate")
        .WithTags("Consolidation")
        .WithSummary("Consulta o saldo diário consolidado por rota de data")
        .Produces<DailyBalanceDto>(StatusCodes.Status200OK, contentType: "application/json")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(policy => policy.RequireRole("Viewer", "Operator", "Admin"));

        // GET /health - Diagnóstico operacional
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Consolidation.Api", timestamp = DateTimeOffset.UtcNow }))
           .WithName("HealthCheck")
           .WithTags("Health")
           .WithSummary("Verifica a saúde operacional do serviço")
           .WithDescription("Retorna 200 OK com o estado do serviço e timestamp atual.")
           .Produces(StatusCodes.Status200OK)
           .AllowAnonymous();

        return app;
    }
}
