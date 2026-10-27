namespace CashFlow.IntegrationTests.Security;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CashFlow.IntegrationTests.Fixtures;
using CashFlow.Shared.Contracts.Security;
using Xunit;

[Collection("PostgreSqlCollection")]
public class AuthenticationIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public AuthenticationIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [DockerFact]
    public async Task TransactionsApi_PostAuthToken_ShouldReturnValidJwtBearerToken()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var request = new TokenRequest("operator-user", "verity-secret");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenObj = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenObj.Should().NotBeNull();
        tokenObj!.TokenType.Should().Be("Bearer");
        tokenObj.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokenObj.ExpiresInSeconds.Should().Be(3600);
    }

    [DockerFact]
    public async Task TransactionsApi_GetTransactions_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task ConsolidationApi_GetDailyBalances_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = new ConsolidationWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/daily-balances");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task ConsolidationApi_GetDailyBalances_WithValidToken_ShouldAuthorizeRequest()
    {
        // Arrange
        using var factory = new ConsolidationWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        // 1. Gera token
        var tokenRes = await client.PostAsJsonAsync("/api/v1/auth/token", new TokenRequest("consolidation-viewer", "verity-secret"));
        tokenRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenObj = await tokenRes.Content.ReadFromJsonAsync<TokenResponse>();

        // 2. Chama endpoint protegido com o token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenObj!.AccessToken);
        var response = await client.GetAsync($"/api/v1/daily-balances?date={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");

        // Assert: Não pode retornar 401 Unauthorized (pode retornar 404 se não houver registros ou 200)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [DockerFact]
    public async Task TransactionsApi_PostAuthToken_WithInvalidCredentials_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var request = new TokenRequest("non-existent-client", "invalid-secret");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task TransactionsApi_PostAuthToken_WithVerityClient_ShouldReturnValidJwtBearerToken()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var request = new TokenRequest("verity-client", "verity-secret");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenObj = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenObj.Should().NotBeNull();
        tokenObj!.TokenType.Should().Be("Bearer");
        tokenObj.AccessToken.Should().NotBeNullOrWhiteSpace();
    }
}
