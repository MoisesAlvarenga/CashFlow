using CashFlow.Consolidation.Domain.Entities;
using Xunit;

namespace CashFlow.UnitTests.Consolidation.Domain;

public class DailyBalanceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ShouldInitializeZeroedBalanceForDate()
    {
        // Arrange
        var targetDate = new DateOnly(2026, 10, 15);

        // Act
        var balance = DailyBalance.Create(targetDate);

        // Assert
        Assert.Equal(targetDate, balance.Date);
        Assert.Equal(0m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(0m, balance.NetBalance);
        Assert.Equal(0, balance.TransactionCount);
        Assert.True(balance.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddCredit_WithValidAmount_ShouldIncrementTotalCreditsAndCount()
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act
        balance.AddCredit(150.00m);
        balance.AddCredit(50.25m);

        // Assert
        Assert.Equal(200.25m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(200.25m, balance.NetBalance);
        Assert.Equal(2, balance.TransactionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddDebit_WithValidAmount_ShouldIncrementTotalDebitsAndCount()
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act
        balance.AddDebit(80.00m);
        balance.AddDebit(20.50m);

        // Assert
        Assert.Equal(0m, balance.TotalCredits);
        Assert.Equal(100.50m, balance.TotalDebits);
        Assert.Equal(-100.50m, balance.NetBalance);
        Assert.Equal(2, balance.TransactionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NetBalance_WithBothCreditsAndDebits_ShouldComputeAccurateDifference()
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act
        balance.AddCredit(1000.00m);
        balance.AddDebit(350.50m);
        balance.AddCredit(200.00m);
        balance.AddDebit(150.00m);

        // Assert
        Assert.Equal(1200.00m, balance.TotalCredits);
        Assert.Equal(500.50m, balance.TotalDebits);
        Assert.Equal(699.50m, balance.NetBalance);
        Assert.Equal(4, balance.TransactionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NetBalance_WhenDebitsExceedCredits_ShouldBeNegative()
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act
        balance.AddCredit(100.00m);
        balance.AddDebit(300.00m);

        // Assert
        Assert.Equal(-200.00m, balance.NetBalance);
        Assert.Equal(2, balance.TransactionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NetBalance_WhenCreditsEqualDebits_ShouldBeZero()
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act
        balance.AddCredit(250.00m);
        balance.AddDebit(250.00m);

        // Assert
        Assert.Equal(0m, balance.NetBalance);
        Assert.Equal(2, balance.TransactionCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [Trait("Category", "Unit")]
    public void AddCredit_WithZeroOrNegativeAmount_ShouldThrowArgumentOutOfRangeException(decimal invalidAmount)
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => balance.AddCredit(invalidAmount));
        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25.5)]
    [Trait("Category", "Unit")]
    public void AddDebit_WithZeroOrNegativeAmount_ShouldThrowArgumentOutOfRangeException(decimal invalidAmount)
    {
        // Arrange
        var balance = DailyBalance.Create(new DateOnly(2026, 10, 15));

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => balance.AddDebit(invalidAmount));
        Assert.Equal("amount", ex.ParamName);
    }
}
