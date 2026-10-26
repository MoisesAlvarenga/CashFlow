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

        public Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedByFilterAsync(
            DateOnly? startDate, DateOnly? endDate, int page, int pageSize)
        {
            var query = Added.AsEnumerable();
            if (startDate.HasValue) query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.Date <= endDate.Value);
            var list = query.ToList();
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<Transaction> Items, int TotalCount)>((items, list.Count));
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

    private class FakeIdempotencyRepository : IIdempotencyRepository
    {
        public readonly Dictionary<string, IdempotencyRecord> Records = new();

        public Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            Records.TryGetValue(key, out var record);
            return Task.FromResult<IdempotencyRecord?>(record);
        }

        public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            Records[record.Key] = record;
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

    /// <summary>
    /// Garante que, ao enviar uma nova chave de idempotência, a transação, o outbox e o registro de idempotência sejam persistidos.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithNewIdempotencyKey_ShouldSaveRecordAndReturnResult()
    {
        // Arrange
        var txRepo = new FakeTransactionRepository();
        var outboxRepo = new FakeOutboxRepository();
        var idempotencyRepo = new FakeIdempotencyRepository();
        var unitOfWork = new FakeTransactionsUnitOfWork();
        var validator = new RegisterTransactionCommandValidator();
        var handler = new RegisterTransactionCommandHandler(txRepo, outboxRepo, unitOfWork, validator, idempotencyRepo);

        var command = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 250.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Payment with idempotency key",
            IdempotencyKey: "unique-key-12345"
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Single(txRepo.Added);
        Assert.Single(outboxRepo.Messages);
        Assert.True(idempotencyRepo.Records.ContainsKey("unique-key-12345"));

        var record = idempotencyRepo.Records["unique-key-12345"];
        Assert.Equal(result.TransactionId, record.TransactionId);
        Assert.Equal(201, record.StatusCode);
        Assert.Contains(result.TransactionId.ToString(), record.ResponsePayload);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    /// <summary>
    /// Garante que, ao reenviar uma chave de idempotência já existente, o resultado anterior em cache seja retornado sem gerar nova transação.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithExistingIdempotencyKey_ShouldReturnCachedResultWithoutCallingDatabaseOrOutbox()
    {
        // Arrange
        var txRepo = new FakeTransactionRepository();
        var outboxRepo = new FakeOutboxRepository();
        var idempotencyRepo = new FakeIdempotencyRepository();
        var unitOfWork = new FakeTransactionsUnitOfWork();
        var validator = new RegisterTransactionCommandValidator();
        var handler = new RegisterTransactionCommandHandler(txRepo, outboxRepo, unitOfWork, validator, idempotencyRepo);

        var existingTxId = Guid.NewGuid();
        var existingDate = DateTimeOffset.UtcNow.AddMinutes(-5);
        var cachedPayload = JsonSerializer.Serialize(new RegisterTransactionResult(
            TransactionId: existingTxId,
            Status: "Registered",
            RegisteredAt: existingDate
        ));

        await idempotencyRepo.AddAsync(new IdempotencyRecord(
            key: "already-processed-key",
            transactionId: existingTxId,
            responsePayload: cachedPayload,
            statusCode: 201
        ));

        var duplicateCommand = new RegisterTransactionCommand(
            Type: TransactionType.Credit,
            Amount: 250.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Duplicate attempt",
            IdempotencyKey: "already-processed-key"
        );

        // Act
        var result = await handler.HandleAsync(duplicateCommand);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingTxId, result.TransactionId);
        Assert.Equal("Registered", result.Status);
        Assert.Empty(txRepo.Added);
        Assert.Empty(outboxRepo.Messages);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    /// <summary>
    /// Garante que, em caso de concorrência com chave duplicada, a colisão seja tratada e retorne o registro persistido pela requisição concorrente.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithConcurrentDuplicateKey_ShouldCatchConflictAndReturnExistingRecord()
    {
        // Arrange
        var txRepo = new FakeTransactionRepository();
        var outboxRepo = new FakeOutboxRepository();
        var idempotencyRepo = new FakeIdempotencyRepository();
        var concurrentTxId = Guid.NewGuid();
        var cachedPayload = JsonSerializer.Serialize(new RegisterTransactionResult(
            TransactionId: concurrentTxId,
            Status: "Registered",
            RegisteredAt: DateTimeOffset.UtcNow
        ));

        var unitOfWork = new FakeTransactionsUnitOfWork(() =>
        {
            // Simula uma requisição concorrente que inseriu o registro no exato momento do SaveChangesAsync
            idempotencyRepo.Records["concurrent-race-key"] = new IdempotencyRecord(
                key: "concurrent-race-key",
                transactionId: concurrentTxId,
                responsePayload: cachedPayload,
                statusCode: 201
            );
            throw new InvalidOperationException("Duplicate primary key violation");
        });

        var validator = new RegisterTransactionCommandValidator();
        var handler = new RegisterTransactionCommandHandler(txRepo, outboxRepo, unitOfWork, validator, idempotencyRepo);

        var command = new RegisterTransactionCommand(
            Type: TransactionType.Debit,
            Amount: 100.00m,
            Date: new DateOnly(2026, 10, 16),
            Description: "Concurrent request",
            IdempotencyKey: "concurrent-race-key"
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(concurrentTxId, result.TransactionId);
    }
}
