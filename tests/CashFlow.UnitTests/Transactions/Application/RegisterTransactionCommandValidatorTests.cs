using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.Validators;
using Xunit;

namespace CashFlow.UnitTests.Transactions.Application;

public class RegisterTransactionCommandValidatorTests
{
    private readonly RegisterTransactionCommandValidator _validator = new();

    /// <summary>
    /// Garante que um comando de cadastro de crédito com dados válidos passe com sucesso pela validação do FluentValidation.
    /// </summary>
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

    /// <summary>
    /// Garante que um comando de cadastro de débito com dados válidos passe com sucesso pela validação do FluentValidation.
    /// </summary>
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

    /// <summary>
    /// Garante que valores zero ou negativos sejam reprovados pela regra AmountGreaterThanZero do FluentValidation.
    /// </summary>
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

    /// <summary>
    /// Garante que descrições vazias, nulas ou compostas por espaços sejam reprovadas com mensagem de erro clara.
    /// </summary>
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

    /// <summary>
    /// Garante que descrições com mais de 255 caracteres sejam rejeitadas para preservar a integridade do banco de dados.
    /// </summary>
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

    /// <summary>
    /// Garante que valores numéricos inválidos para o enum TransactionType (diferentes de Credit=1 e Debit=2) sejam rejeitados.
    /// </summary>
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

    /// <summary>
    /// Garante que uma chave de idempotência válida passe pela validação.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithValidIdempotencyKey_ShouldPass()
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 100.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Valid description",
            IdempotencyKey: "req-12345-abcde"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Garante que chaves de idempotência com mais de 128 caracteres sejam rejeitadas.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithIdempotencyKeyExceeding128Characters_ShouldFail()
    {
        // Arrange
        var longKey = new string('k', 129);
        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 100.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Valid description",
            IdempotencyKey: longKey
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTransactionCommand.IdempotencyKey));
    }
}
