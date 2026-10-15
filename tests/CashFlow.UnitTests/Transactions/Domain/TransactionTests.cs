using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Domain.Entities;
using Xunit;

namespace CashFlow.UnitTests.Transactions.Domain;

public class TransactionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateCredit_WithValidParameters_ShouldInstantiateCreditTransaction()
    {
        // Arrange
        var amount = 250.75m;
        var date = new DateOnly(2026, 10, 15);
        var description = "Client invoice payment";

        // Act
        var transaction = Transaction.CreateCredit(amount, date, description);

        // Assert
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(TransactionType.Credit, transaction.Type);
        Assert.Equal(amount, transaction.Amount);
        Assert.Equal(date, transaction.Date);
        Assert.Equal(description, transaction.Description);
        Assert.True(transaction.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateDebit_WithValidParameters_ShouldInstantiateDebitTransaction()
    {
        // Arrange
        var amount = 100.00m;
        var date = new DateOnly(2026, 10, 15);
        var description = "Supplier invoice";

        // Act
        var transaction = Transaction.CreateDebit(amount, date, description);

        // Assert
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(TransactionType.Debit, transaction.Type);
        Assert.Equal(amount, transaction.Amount);
        Assert.Equal(date, transaction.Date);
        Assert.Equal(description, transaction.Description);
        Assert.True(transaction.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10.50)]
    [Trait("Category", "Unit")]
    public void CreateCredit_WithZeroOrNegativeAmount_ShouldThrowArgumentOutOfRangeException(decimal invalidAmount)
    {
        // Arrange
        var date = new DateOnly(2026, 10, 15);
        var description = "Invalid credit";

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.CreateCredit(invalidAmount, date, description));

        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50.00)]
    [Trait("Category", "Unit")]
    public void CreateDebit_WithZeroOrNegativeAmount_ShouldThrowArgumentOutOfRangeException(decimal invalidAmount)
    {
        // Arrange
        var date = new DateOnly(2026, 10, 15);
        var description = "Invalid debit";

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.CreateDebit(invalidAmount, date, description));

        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Category", "Unit")]
    public void CreateCredit_WithEmptyOrNullDescription_ShouldThrowArgumentException(string? invalidDescription)
    {
        // Arrange
        var amount = 100.00m;
        var date = new DateOnly(2026, 10, 15);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Transaction.CreateCredit(amount, date, invalidDescription!));

        Assert.Equal("description", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [Trait("Category", "Unit")]
    public void CreateDebit_WithEmptyOrNullDescription_ShouldThrowArgumentException(string? invalidDescription)
    {
        // Arrange
        var amount = 50.00m;
        var date = new DateOnly(2026, 10, 15);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Transaction.CreateDebit(amount, date, invalidDescription!));

        Assert.Equal("description", ex.ParamName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateTransaction_ShouldTrimDescriptionWhitespace()
    {
        // Arrange
        var rawDescription = "   Office Supplies Purchase   ";

        // Act
        var transaction = Transaction.CreateDebit(45.90m, new DateOnly(2026, 10, 15), rawDescription);

        // Assert
        Assert.Equal("Office Supplies Purchase", transaction.Description);
    }
}
