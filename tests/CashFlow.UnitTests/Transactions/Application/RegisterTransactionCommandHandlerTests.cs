using System.Text.Json;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Validators;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using FluentValidation;
using Xunit;

namespace CashFlow.UnitTests.Transactions.Application;

public class RegisterTransactionCommandHandlerTests
{
    private class FakeTransactionRepository : ITransactionRepository
    {
        public readonly List<Transaction> Added = [];

        public Task AddAsync(Transaction transaction)
        {
            Added.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<Transaction?> GetByIdAsync(Guid id)
        {
            return Task.FromResult(Added.FirstOrDefault(t => t.Id == id));
        }

        public Task<IReadOnlyList<Transaction>> GetByFilterAsync(DateOnly? startDate, DateOnly? endDate)
        {
            var query = Added.AsEnumerable();
            if (startDate.HasValue) query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.Date <= endDate.Value);
            return Task.FromResult<IReadOnlyList<Transaction>>(query.ToList());
        }
    }

    private class FakeOutboxRepository : IOutboxRepository
    {
        public readonly List<OutboxMessage> Messages = [];

        public Task AddMessageAsync(OutboxMessage message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize)
        {
            var pending = Messages.Where(m => m.ProcessedAt == null).Take(batchSize).ToList();
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
        }

        public Task MarkAsProcessedAsync(Guid messageId)
        {
            var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.ProcessedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }
    }

    private class FakeTransactionsUnitOfWork(Action? onSave = null) : ITransactionsUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task SaveChangesAsync()
        {
            SaveChangesCallCount++;
            onSave?.Invoke();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Garante a persistência atômica da transação de crédito e da respectiva mensagem Outbox para publicação no RabbitMQ.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithValidCreditCommand_ShouldPersistTransactionAndOutboxEvent()
    {
        // Arrange
        var txRepo = new FakeTransactionRepository();
        var outboxRepo = new FakeOutboxRepository();
        var unitOfWork = new FakeTransactionsUnitOfWork(() =>
        {
            Assert.Single(txRepo.Added);
            Assert.Single(outboxRepo.Messages);
        });
        var validator = new RegisterTransactionCommandValidator();
        var handler = new RegisterTransactionCommandHandler(txRepo, outboxRepo, unitOfWork, validator);

        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 500.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Client subscription fee"
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Registered", result.Status);
        Assert.NotEqual(Guid.Empty, result.TransactionId);

        // Verify Transaction persisted
        Assert.Single(txRepo.Added);
        var savedTx = txRepo.Added[0];
        Assert.Equal(result.TransactionId, savedTx.Id);
        Assert.Equal(TransactionType.Credit, savedTx.Type);
        Assert.Equal(500.00m, savedTx.Amount);

        // Verify Outbox Message persisted
        Assert.Single(outboxRepo.Messages);
        var savedOutbox = outboxRepo.Messages[0];
        Assert.Equal(nameof(TransactionRegisteredEvent), savedOutbox.EventType);
        Assert.Null(savedOutbox.ProcessedAt);

        // Verify deserialized payload
        var deserializedEvent = JsonSerializer.Deserialize<TransactionRegisteredEvent>(savedOutbox.Payload);
        Assert.NotNull(deserializedEvent);
        Assert.Equal(savedTx.Id, deserializedEvent.TransactionId);
        Assert.Equal("Credit", deserializedEvent.Type);
        Assert.Equal(500.00m, deserializedEvent.Amount);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    /// <summary>
    /// Garante a persistência de lançamento de débito e evento Outbox correspondente através do handler de aplicação.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithValidDebitCommand_ShouldPersistDebitTransaction()
    {
        // Arrange
        var txRepo = new FakeTransactionRepository();
        var outboxRepo = new FakeOutboxRepository();
        var unitOfWork = new FakeTransactionsUnitOfWork();
        var validator = new RegisterTransactionCommandValidator();
        var handler = new RegisterTransactionCommandHandler(txRepo, outboxRepo, unitOfWork, validator);

        var command = new RegisterTransactionCommand(
            Type: TransactionType.Debit,
            Amount: 120.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Server infrastructure costs"
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Single(txRepo.Added);
        Assert.Equal(TransactionType.Debit, txRepo.Added[0].Type);
        Assert.Single(outboxRepo.Messages);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    /// <summary>
    /// Garante que um comando inválido seja barrado pelo validador antes de tocar os repositórios (Fail-Fast).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var txRepo = new FakeTransactionRepository();
        var outboxRepo = new FakeOutboxRepository();
        var unitOfWork = new FakeTransactionsUnitOfWork();
        var validator = new RegisterTransactionCommandValidator();
        var handler = new RegisterTransactionCommandHandler(txRepo, outboxRepo, unitOfWork, validator);

        var invalidCommand = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: -10.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: ""
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(invalidCommand));
        Assert.Empty(txRepo.Added);
        Assert.Empty(outboxRepo.Messages);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }
}
