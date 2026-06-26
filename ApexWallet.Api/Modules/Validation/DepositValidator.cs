using FluentValidation;
using ApexWallet.Api.Modules.WalletModule;

namespace ApexWallet.Api.Modules.Validation
{
    public class DepositValidator : AbstractValidator<DepositDto>
    {
        public DepositValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Deposit amount must be greater than zero.")
                .LessThanOrEqualTo(500000).WithMessage("Single deposit limit capped at 500,000 INR for compliance.");
        }
    }
}