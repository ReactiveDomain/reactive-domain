using FluentValidation;

namespace ReactiveDomain.Domain.Tests.Example
{
    public class StopGroupValidator : AbstractValidator<StopGroup>
    {
        public StopGroupValidator()
        {
            RuleFor(cmd => cmd.GroupId)
                .NotEmpty();
            RuleFor(cmd => cmd.AdministratorId)
                .NotEmpty();
        }
    }
}