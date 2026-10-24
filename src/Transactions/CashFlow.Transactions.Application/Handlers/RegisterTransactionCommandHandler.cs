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
    private readonly ICancellationTokenProvider _tokenProvider;

    public RegisterTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IOutboxRepository outboxRepository,
        ITransactionsUnitOfWork unitOfWork,
        IValidator<RegisterTransactionCommand> validator,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
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

        await _transactionRepository.AddAsync(transaction);
        await _outboxRepository.AddMessageAsync(outboxMessage);
        await _unitOfWork.SaveChangesAsync();

        return new RegisterTransactionResult(
            TransactionId: transaction.Id,
            Status: "Registered",
            RegisteredAt: transaction.CreatedAt
        );
    }
}
