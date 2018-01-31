using FluentValidation;

namespace ReactiveDomain.Example
{
    public class StartGroupValidator : AbstractValidator<StartGroup>
    {
        public StartGroupValidator()
        {
            RuleFor(cmd => cmd.GroupId)
                .NotEmpty();
            RuleFor(cmd => cmd.AdministratorId)
                .NotEmpty();
            RuleFor(cmd => cmd.Name)
                .NotNull()
                .Length(1, 256);
        }
    }


}