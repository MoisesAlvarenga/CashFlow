namespace CashFlow.IntegrationTests.Fixtures;

using CashFlow.Shared.Contracts.Messaging;
using CashFlow.Transactions.Api;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class TransactionsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly RabbitMqFixture? _rabbitMqFixture;

    public TransactionsWebApplicationFactory(string postgresConnectionString, RabbitMqFixture? rabbitMqFixture = null)
    {
        _postgresConnectionString = postgresConnectionString;
        _rabbitMqFixture = rabbitMqFixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgresConnectionString
            };

            if (_rabbitMqFixture != null)
            {
                inMemoryConfig["RabbitMq:Host"] = _rabbitMqFixture.HostName;
                inMemoryConfig["RabbitMq:Port"] = _rabbitMqFixture.Port.ToString();
                inMemoryConfig["RabbitMq:Username"] = _rabbitMqFixture.UserName;
                inMemoryConfig["RabbitMq:Password"] = _rabbitMqFixture.Password;
            }

            config.AddInMemoryCollection(inMemoryConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove o DbContext existente para reinjetar apontando para o PostgreSQL de teste
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TransactionsDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TransactionsDbContext>(options =>
            {
                options.UseNpgsql(_postgresConnectionString);
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
