using FluentValidation;
using ApexWallet.Api.Modules.UserModule;

namespace ApexWallet.Api.Modules.Validation
{
    public class UpdateProfileValidator : AbstractValidator<UpdateProfileDto>
    {
        public UpdateProfileValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Full Name cannot be empty.")
                .MinimumLength(3).WithMessage("Full Name must be at least 3 characters long.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email address is required.")
                .EmailAddress().WithMessage("Please enter a valid email address structure.");
        }
    }
}