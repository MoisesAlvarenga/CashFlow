namespace CashFlow.IntegrationTests.Transactions;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CashFlow.IntegrationTests.Fixtures;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Shared.Contracts.Security;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("PostgreSqlCollection")]
public class TransactionsDualWriteIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public TransactionsDualWriteIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<string> AuthenticateClientAsync(HttpClient client)
    {
        var tokenReq = new TokenRequest("integration-tester", "verity-secret");
        var tokenRes = await client.PostAsJsonAsync("/api/v1/auth/token", tokenReq);
        tokenRes.EnsureSuccessStatusCode();
        var tokenObj = await tokenRes.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenObj!.AccessToken;
    }

    [DockerFact]
    public async Task PostTransaction_WithoutAuthorization_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var command = new
        {
            Type = "Credit",
            Amount = 100.00m,
            Date = "2026-10-26",
            Description = "Anonymous request"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/transactions", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task PostTransaction_WithValidPayloadAndJwtToken_ShouldPersistTransactionAndOutboxAtomicallyInPostgreSql()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var token = await AuthenticateClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = $"test-key-{Guid.NewGuid()}";
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var command = new
        {
            Type = "Credit",
            Amount = 1500.50m,
            Date = "2026-10-26",
            Description = "Integration Test Invoice Payment"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/transactions", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RegisterTransactionResult>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("Registered");
        result.TransactionId.Should().NotBeEmpty();

        // Verifica integridade física atômica no PostgreSQL real
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();

        var savedTx = await dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == result.TransactionId);
        savedTx.Should().NotBeNull();
        savedTx!.Amount.Should().Be(1500.50m);
        savedTx.Type.Should().Be(TransactionType.Credit);
        savedTx.Description.Should().Be("Integration Test Invoice Payment");

        var savedOutbox = await dbContext.OutboxMessages.FirstOrDefaultAsync(o => o.Payload.Contains(result.TransactionId.ToString()));
        savedOutbox.Should().NotBeNull();
        savedOutbox!.EventType.Should().Be(nameof(TransactionRegisteredEvent));
        savedOutbox.ProcessedAt.Should().BeNull();

        var savedIdempotency = await dbContext.IdempotencyRecords.FirstOrDefaultAsync(i => i.Key == idempotencyKey);
        savedIdempotency.Should().NotBeNull();
        savedIdempotency!.TransactionId.Should().Be(result.TransactionId);
        savedIdempotency.StatusCode.Should().Be(201);
    }

    [DockerFact]
    public async Task PostTransaction_WithSameIdempotencyKey_ShouldReturnCachedResultWithoutDuplicatingDatabaseRecords()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var token = await AuthenticateClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var sharedKey = $"idempotent-repeat-{Guid.NewGuid()}";
        client.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);

        var command = new
        {
            Type = "Debit",
            Amount = 300.00m,
            Date = "2026-10-26",
            Description = "Initial request"
        };

        // Act 1: Primeira requisição
        var response1 = await client.PostAsJsonAsync("/api/v1/transactions", command);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var result1 = await response1.Content.ReadFromJsonAsync<RegisterTransactionResult>();

        // Act 2: Segunda requisição idêntica com a mesma chave (ex: retry de rede)
        var response2 = await client.PostAsJsonAsync("/api/v1/transactions", command);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        var result2 = await response2.Content.ReadFromJsonAsync<RegisterTransactionResult>();

        // Assert
        result2.Should().NotBeNull();
        result2!.TransactionId.Should().Be(result1!.TransactionId);
        result2.Status.Should().Be("Registered");

        // Verifica que no PostgreSQL não houve duplicidade de registros
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();

        var txCount = await dbContext.Transactions.CountAsync(t => t.Id == result1.TransactionId);
        txCount.Should().Be(1);

        var outboxCount = await dbContext.OutboxMessages.CountAsync(o => o.Payload.Contains(result1.TransactionId.ToString()));
        outboxCount.Should().Be(1);
    }
}
