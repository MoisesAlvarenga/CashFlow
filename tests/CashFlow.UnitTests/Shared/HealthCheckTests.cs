namespace CashFlow.UnitTests.Shared;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

public class HealthCheckTests
{
    [Fact]
    public async Task AddDefaultHealthChecks_RegistersSelfLivenessCheck()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();
        var app = builder.Build();

        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();

        // Act - Only run checks with "live" tag (simulates /alive endpoint)
        var report = await healthCheckService.CheckHealthAsync(r => r.Tags.Contains("live"));

        // Assert
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.True(report.Entries.ContainsKey("self"));
        Assert.Equal(HealthStatus.Healthy, report.Entries["self"].Status);
    }

    [Fact]
    public void AddDefaultHealthChecks_WhenPostgresConnectionStringProvided_RegistersPostgresCheck()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=secret";
        builder.AddDefaultHealthChecks();
        var app = builder.Build();

        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        // Assert
        var registrations = options.Value.Registrations;
        Assert.Contains(registrations, r => r.Name == "postgresql" && r.Tags.Contains("ready"));
    }

    [Fact]
    public void AddDefaultHealthChecks_WhenRabbitMqConfigured_RegistersRabbitMqCheck()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["RabbitMq:Host"] = "localhost";
        builder.Configuration["RabbitMq:Port"] = "5672";
        builder.AddDefaultHealthChecks();
        var app = builder.Build();

        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        // Assert
        var registrations = options.Value.Registrations;
        Assert.Contains(registrations, r => r.Name == "rabbitmq" && r.Tags.Contains("ready"));
    }
}
