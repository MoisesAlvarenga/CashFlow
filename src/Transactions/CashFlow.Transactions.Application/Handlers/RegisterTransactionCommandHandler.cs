namespace CashFlow.Transactions.Application.Handlers;

using System.Text.Json;
using CashFlow.Shared.Contracts.Context;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Application.Commands;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using FluentValidation;

public interface IRegisterTransactionCommandHandler
{
    Task<RegisterTransactionResult> HandleAsync(RegisterTransactionCommand command);
}

public class RegisterTransactionCommandHandler : IRegisterTransactionCommandHandler
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ITransactionsUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterTransactionCommand> _validator;
    private readonly IIdempotencyRepository? _idempotencyRepository;
    private readonly ICancellationTokenProvider _tokenProvider;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RegisterTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IOutboxRepository outboxRepository,
        ITransactionsUnitOfWork unitOfWork,
        IValidator<RegisterTransactionCommand> validator,
        IIdempotencyRepository? idempotencyRepository = null,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _idempotencyRepository = idempotencyRepository;
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public async Task<RegisterTransactionResult> HandleAsync(RegisterTransactionCommand command)
    {
        var ct = _tokenProvider.Token;
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var normalizedKey = command.IdempotencyKey?.Trim();

        // 1. Verifica se já existe resposta armazenada para a Idempotency-Key
        if (!string.IsNullOrEmpty(normalizedKey) && _idempotencyRepository != null)
        {
            var existingRecord = await _idempotencyRepository.GetByKeyAsync(normalizedKey, ct);
            if (existingRecord != null && !string.IsNullOrEmpty(existingRecord.ResponsePayload))
            {
                var cachedResult = JsonSerializer.Deserialize<RegisterTransactionResult>(existingRecord.ResponsePayload, JsonOptions);
                if (cachedResult != null)
                {
                    return cachedResult;
                }
            }
        }

        var transaction = command.Type == TransactionType.Credit
            ? Transaction.CreateCredit(command.Amount, command.Date, command.Description)
            : Transaction.CreateDebit(command.Amount, command.Date, command.Description);

        var integrationEvent = new TransactionRegisteredEvent(
            TransactionId: transaction.Id,
            Type: transaction.Type.ToString(),
            Amount: transaction.Amount,
            Date: transaction.Date,
            Description: transaction.Description,
            Timestamp: transaction.CreatedAt
        );

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(TransactionRegisteredEvent),
            Payload = JsonSerializer.Serialize(integrationEvent),
            CreatedAt = DateTimeOffset.UtcNow,
            ProcessedAt = null,
            RetryCount = 0
        };

        var result = new RegisterTransactionResult(
            TransactionId: transaction.Id,
            Status: "Registered",
            RegisteredAt: transaction.CreatedAt
        );

        await _transactionRepository.AddAsync(transaction);
        await _outboxRepository.AddMessageAsync(outboxMessage);

        if (!string.IsNullOrEmpty(normalizedKey) && _idempotencyRepository != null)
        {
            var idempotencyRecord = new IdempotencyRecord(
                key: normalizedKey,
                transactionId: transaction.Id,
                responsePayload: JsonSerializer.Serialize(result),
                statusCode: 201
            );
            await _idempotencyRepository.AddAsync(idempotencyRecord, ct);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception) when (!string.IsNullOrEmpty(normalizedKey) && _idempotencyRepository != null)
        {
            // Tratamento de concorrência: duas requisições simultâneas com a mesma chave
            var existingRecord = await _idempotencyRepository.GetByKeyAsync(normalizedKey, ct);
            if (existingRecord != null && !string.IsNullOrEmpty(existingRecord.ResponsePayload))
            {
                var cachedResult = JsonSerializer.Deserialize<RegisterTransactionResult>(existingRecord.ResponsePayload, JsonOptions);
                if (cachedResult != null)
                {
                    return cachedResult;
                }
            }

            throw;
        }

        return result;
    }
}
