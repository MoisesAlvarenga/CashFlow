using CashFlow.Consolidation.Domain.Entities;
using Xunit;

namespace CashFlow.UnitTests.Consolidation.Domain;

public class DailyBalanceTests
{
    /// <summary>
    /// Garante a inicialização do saldo diário zerado com data definida, sem movimentações e timestamp de atualização válido.
    /// </summary>
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

    /// <summary>
    /// Garante que a adição de créditos incremente corretamente o TotalCredits, a contagem de transações e o NetBalance.
    /// </summary>
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

    /// <summary>
    /// Garante que a adição de débitos incremente corretamente o TotalDebits, a contagem de transações e atualize o NetBalance negativamente.
    /// </summary>
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

    /// <summary>
    /// Garante o cálculo aritmético exato do saldo líquido (TotalCredits - TotalDebits) ao processar múltiplos créditos e débitos intercalados.
    /// </summary>
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

    /// <summary>
    /// Garante que o saldo líquido reflita valor negativo quando a soma dos débitos supera a soma dos créditos do dia.
    /// </summary>
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

    /// <summary>
    /// Garante que o saldo líquido retorne exatamente zero quando os valores de crédito e débito se equivalem.
    /// </summary>
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

    /// <summary>
    /// Garante que a adição de crédito com valor zero ou negativo seja impedida pelo domínio através de ArgumentOutOfRangeException.
    /// </summary>
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

    /// <summary>
    /// Garante que a adição de débito com valor zero ou negativo seja impedida pelo domínio através de ArgumentOutOfRangeException.
    /// </summary>
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
