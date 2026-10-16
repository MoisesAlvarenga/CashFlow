namespace CashFlow.Transactions.Application.Validators;

using CashFlow.Transactions.Application.Commands;
using FluentValidation;

public class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    public RegisterTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(255)
            .WithMessage("Description cannot exceed 255 characters.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Transaction type must be Credit (1) or Debit (2).");

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Transaction date is required.");
    }
}
