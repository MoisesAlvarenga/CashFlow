namespace CashFlow.IntegrationTests.Fixtures;

using Testcontainers.RabbitMq;
using Xunit;

/// <summary>
/// Fixture de gerenciamento do contêiner RabbitMQ 3 para testes de integração de mensageria.
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    public RabbitMqContainer? Container { get; private set; }
    public string ConnectionString => Container?.GetConnectionString() ?? string.Empty;
    public string HostName => Container?.Hostname ?? "localhost";
    public ushort Port => Container?.GetMappedPublicPort(5672) ?? 5672;
    public string UserName => "guest";
    public string Password => "guest";

    public async Task InitializeAsync()
    {
        if (DockerEnvironment.IsDockerAvailable())
        {
            Container = new RabbitMqBuilder("rabbitmq:3-management-alpine")
                .WithUsername("guest")
                .WithPassword("guest")
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
