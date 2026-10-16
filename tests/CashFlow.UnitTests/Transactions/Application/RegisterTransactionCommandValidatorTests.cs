using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.Validators;
using Xunit;

namespace CashFlow.UnitTests.Transactions.Application;

public class RegisterTransactionCommandValidatorTests
{
    private readonly RegisterTransactionCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithValidCreditCommand_ShouldPass()
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 150.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Payment for order #1234"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithValidDebitCommand_ShouldPass()
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Debit,
            Amount: 75.50m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Office electricity bill"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50.00)]
    [Trait("Category", "Unit")]
    public void Validate_WithZeroOrNegativeAmount_ShouldFail(decimal invalidAmount)
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: invalidAmount,
            Date: new DateOnly(2026, 10, 16),
            Description: "Valid description"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTransactionCommand.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Category", "Unit")]
    public void Validate_WithEmptyOrWhitespaceDescription_ShouldFail(string? invalidDescription)
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 100.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: invalidDescription!
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTransactionCommand.Description));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithDescriptionExceeding255Characters_ShouldFail()
    {
        // Arrange
        var longDescription = new string('A', 256);
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Debit,
            Amount: 100.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: longDescription
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTransactionCommand.Description));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithInvalidEnumType_ShouldFail()
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            Type: (TransactionType)99,
            Amount: 100.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Valid description"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTransactionCommand.Type));
    }
}
