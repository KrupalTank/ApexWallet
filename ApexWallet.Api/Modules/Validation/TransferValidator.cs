using FluentValidation;
using ApexWallet.Api.Modules.WalletModule;

namespace ApexWallet.Api.Modules.Validation
{
    public class TransferValidator : AbstractValidator<TransferDto>
    {
        public TransferValidator()
        {
            RuleFor(x => x.ReceiverUserId)
                .GreaterThan(0).WithMessage("A valid positive Recipient User ID is required.");

            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("The transfer amount must be greater than zero.")
                .LessThan(100000).WithMessage("For security compliance, a single transfer cannot exceed 1,000,000 INR.");
        }
    }
}