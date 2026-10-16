using System.Reflection;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Validators;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using CashFlow.Transactions.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

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
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transactions API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "CashFlow - Transactions API Documentation";
});

app.UseHttpsRedirection();

// Redirect root to Swagger UI for quick developer access
app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

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
.WithTags("Transactions")
.WithSummary("Registra um lançamento financeiro de crédito ou débito")
.WithDescription("Valida os dados com FluentValidation, persiste a transação atomicamente no banco de dados e gera uma mensagem de evento na tabela de Outbox para sincronização assíncrona.")
.Produces<RegisterTransactionResult>(StatusCodes.Status201Created, contentType: "application/json")
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError);

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Transactions.Api", timestamp = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health")
   .WithSummary("Verifica a saúde operacional do serviço")
   .WithDescription("Retorna 200 OK com o estado do serviço e timestamp atual.")
   .Produces(StatusCodes.Status200OK);

await app.RunAsync();
