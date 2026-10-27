namespace CashFlow.UnitTests.Transactions.Infrastructure;

using System;
using System.Threading.Tasks;
using CashFlow.Shared.Contracts.Security;
using CashFlow.Transactions.Infrastructure.Data;
using CashFlow.Transactions.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TransactionsClientAuthServiceTests
{
    private readonly SecretHasher _hasher = new();

    private TransactionsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TransactionsDbContext(options);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithValidClientAndSecret_ShouldReturnClient()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var (hash, salt) = _hasher.HashSecret("secret-123");
        var client = new ApiClient
        {
            Id = Guid.NewGuid(),
            ClientId = "valid-client",
            SecretHash = hash,
            Salt = salt,
            Name = "Valid Test Client",
            Roles = "Operator,Admin",
            IsActive = true
        };
        dbContext.ApiClients.Add(client);
        await dbContext.SaveChangesAsync();

        var service = new TransactionsClientAuthService(dbContext, _hasher);

        // Act
        var result = await service.ValidateCredentialsAsync("valid-client", "secret-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("valid-client", result.ClientId);
        Assert.Equal("Valid Test Client", result.Name);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithUnknownClientId_ShouldReturnNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new TransactionsClientAuthService(dbContext, _hasher);

        // Act
        var result = await service.ValidateCredentialsAsync("non-existent-client", "secret");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithWrongSecret_ShouldReturnNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var (hash, salt) = _hasher.HashSecret("correct-secret");
        var client = new ApiClient
        {
            Id = Guid.NewGuid(),
            ClientId = "client-wrong-secret",
            SecretHash = hash,
            Salt = salt,
            Name = "Test Client",
            Roles = "Operator",
            IsActive = true
        };
        dbContext.ApiClients.Add(client);
        await dbContext.SaveChangesAsync();

        var service = new TransactionsClientAuthService(dbContext, _hasher);

        // Act
        var result = await service.ValidateCredentialsAsync("client-wrong-secret", "wrong-secret");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInactiveClient_ShouldReturnNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var (hash, salt) = _hasher.HashSecret("secret-123");
        var client = new ApiClient
        {
            Id = Guid.NewGuid(),
            ClientId = "inactive-client",
            SecretHash = hash,
            Salt = salt,
            Name = "Inactive Client",
            Roles = "Operator",
            IsActive = false
        };
        dbContext.ApiClients.Add(client);
        await dbContext.SaveChangesAsync();

        var service = new TransactionsClientAuthService(dbContext, _hasher);

        // Act
        var result = await service.ValidateCredentialsAsync("inactive-client", "secret-123");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("", "secret")]
    [InlineData("client", null)]
    [InlineData("client", "")]
    public async Task ValidateCredentialsAsync_WithNullOrEmpty_ShouldReturnNull(string? clientId, string? secret)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new TransactionsClientAuthService(dbContext, _hasher);

        // Act
        var result = await service.ValidateCredentialsAsync(clientId!, secret!);

        // Assert
        Assert.Null(result);
    }
}
