namespace CashFlow.IntegrationTests.Fixtures;

using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Fixture de gerenciamento do contêiner PostgreSQL 16 para testes de integração.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }
    public string ConnectionString => Container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        if (DockerEnvironment.IsDockerAvailable())
        {
            Container = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("cashflow_integration_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await Container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
    }
}
