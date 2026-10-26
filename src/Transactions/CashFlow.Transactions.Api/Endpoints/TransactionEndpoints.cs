namespace CashFlow.Transactions.Api.Endpoints;

using CashFlow.Shared.Contracts.Common;
using CashFlow.Shared.Contracts.Security;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.DTOs;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Queries;
using FluentValidation;

/// <summary>
/// Métodos de extensão para mapeamento dos endpoints Minimal API de Lançamentos.
/// </summary>
public static class TransactionEndpoints
{
    /// <summary>
    /// Registra todos os endpoints do microsserviço de Lançamentos (Transactions API).
    /// </summary>
    /// <param name="app">A instância de WebApplication do pipeline HTTP.</param>
    /// <returns>A mesma instância de WebApplication para encadeamento fluente.</returns>
    public static WebApplication TransactionAPI(this WebApplication app)
    {
        return app.MapTransactionEndpoints();
    }

    /// <summary>
    /// Alias fluente no padrão Map* para registro de rotas de Lançamentos.
    /// </summary>
    public static WebApplication MapTransactionEndpoints(this WebApplication app)
    {
        // Redirecionamento da raiz para o Swagger UI
        app.MapGet("/", () => Results.Redirect("/swagger"))
           .ExcludeFromDescription();

        // POST /api/v1/auth/token - Obter token JWT para acesso autenticado
        app.MapPost("/api/v1/auth/token", (
            TokenRequest request,
            IJwtTokenService tokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return Results.BadRequest(new { error = "ClientId e ClientSecret são obrigatórios." });
            }

            var tokenResponse = tokenService.GenerateToken(request.ClientId.Trim());
            return Results.Ok(tokenResponse);
        })
        .WithName("GenerateToken")
        .WithTags("Auth")
        .WithSummary("Gera um token JWT Bearer para autenticação nos endpoints")
        .WithDescription("Emite um token JWT assinado válido por 60 minutos contendo as claims do cliente informado. Use o token retornado no botão 'Authorize' do Swagger ou no cabeçalho 'Authorization: Bearer {token}'.")
        .Produces<TokenResponse>(StatusCodes.Status200OK, contentType: "application/json")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AllowAnonymous();

        // POST /api/v1/transactions - Registrar lançamento financeiro
        app.MapPost("/api/v1/transactions", async (
            RegisterTransactionCommand command,
            HttpContext context,
            IRegisterTransactionCommandHandler handler) =>
        {
            var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault()
                ?? context.Request.Headers["X-Idempotency-Key"].FirstOrDefault()
                ?? command.IdempotencyKey;

            var effectiveCommand = !string.IsNullOrWhiteSpace(idempotencyKey)
                ? command with { IdempotencyKey = idempotencyKey.Trim() }
                : command;

            try
            {
                var result = await handler.HandleAsync(effectiveCommand);
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
        .WithTags("Transactions")
        .WithSummary("Registra um lançamento financeiro de crédito ou débito")
        .WithDescription("Valida os dados com FluentValidation, persiste a transação atomicamente no banco de dados e gera uma mensagem de evento na tabela de Outbox para sincronização assíncrona. Suporta o cabeçalho 'Idempotency-Key' para garantir que requisições repetidas retornem o mesmo resultado sem duplicar lançamentos. Requer autenticação JWT.")
        .Produces<RegisterTransactionResult>(StatusCodes.Status201Created, contentType: "application/json")
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .RequireAuthorization();

        // GET /api/v1/transactions - Consultar lançamentos paginados com filtros opcionais de data
        app.MapGet("/api/v1/transactions", async (
            DateOnly? date,
            DateOnly? startDate,
            DateOnly? endDate,
            int? page,
            int? pageSize,
            IGetTransactionsQueryHandler handler) =>
        {
            var actualPage = page ?? GetTransactionsQuery.DefaultPage;
            var actualPageSize = pageSize ?? GetTransactionsQuery.DefaultPageSize;

            if (actualPage < 1)
            {
                return Results.BadRequest(new { error = "O parâmetro 'page' deve ser maior ou igual a 1." });
            }

            if (actualPageSize < 1 || actualPageSize > GetTransactionsQuery.MaxPageSize)
            {
                return Results.BadRequest(new { error = $"O parâmetro 'pageSize' deve estar entre 1 e {GetTransactionsQuery.MaxPageSize}." });
            }

            var query = new GetTransactionsQuery(date, startDate, endDate, actualPage, actualPageSize);
            var transactions = await handler.HandleAsync(query);
            return Results.Ok(transactions);
        })
        .WithName("GetTransactions")
        .WithTags("Transactions")
        .WithSummary("Consulta os lançamentos financeiros com filtro de data e paginação")
        .WithDescription("Retorna a lista paginada de transações registradas, permitindo filtrar por uma data pontual ('date') ou intervalo de datas ('startDate' e 'endDate'). Suporta navegação por páginas com 'page' (padrão 1) e 'pageSize' (padrão 20, máximo 100). Requer autenticação JWT.")
        .Produces<PagedResult<TransactionDto>>(StatusCodes.Status200OK, contentType: "application/json")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        // GET /health - Diagnóstico operacional
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Transactions.Api", timestamp = DateTimeOffset.UtcNow }))
           .WithName("HealthCheck")
           .WithTags("Health")
           .WithSummary("Verifica a saúde operacional do serviço")
           .WithDescription("Retorna 200 OK com o estado do serviço e timestamp atual.")
           .Produces(StatusCodes.Status200OK)
           .AllowAnonymous();

        return app;
    }
}
