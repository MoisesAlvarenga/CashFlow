namespace CashFlow.IntegrationTests.Diagnostics;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

public class HealthCheckIntegrationTests : IClassFixture<PostgreSqlFixture>, IClassFixture<RabbitMqFixture>
{
    private readonly PostgreSqlFixture _postgreSqlFixture;
    private readonly RabbitMqFixture _rabbitMqFixture;

    public HealthCheckIntegrationTests(PostgreSqlFixture postgreSqlFixture, RabbitMqFixture rabbitMqFixture)
    {
        _postgreSqlFixture = postgreSqlFixture;
        _rabbitMqFixture = rabbitMqFixture;
    }

    [DockerFact]
    public async Task TransactionsApi_AliveEndpoint_ReturnsOkAndHealthy()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(
            _postgreSqlFixture.ConnectionString,
            _rabbitMqFixture);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/alive");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [DockerFact]
    public async Task TransactionsApi_HealthEndpoint_ReturnsDetailedJsonWithPostgresAndRabbitMq()
    {
        // Arrange
        using var factory = new TransactionsWebApplicationFactory(
            _postgreSqlFixture.ConnectionString,
            _rabbitMqFixture);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("Healthy");

        var entries = json.GetProperty("entries");
        entries.TryGetProperty("self", out var self).Should().BeTrue();
        self.GetProperty("status").GetString().Should().Be("Healthy");

        entries.TryGetProperty("postgresql", out var postgres).Should().BeTrue();
        postgres.GetProperty("status").GetString().Should().Be("Healthy");

        entries.TryGetProperty("rabbitmq", out var rabbit).Should().BeTrue();
        rabbit.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [DockerFact]
    public async Task ConsolidationApi_AliveAndHealthEndpoints_ReturnHealthy()
    {
        // Arrange
        using var factory = new ConsolidationWebApplicationFactory(
            _postgreSqlFixture.ConnectionString,
            _rabbitMqFixture);
        using var client = factory.CreateClient();

        // Act
        var aliveResponse = await client.GetAsync("/alive");
        var healthResponse = await client.GetAsync("/health");

        // Assert
        aliveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await healthResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("Healthy");
    }
}
