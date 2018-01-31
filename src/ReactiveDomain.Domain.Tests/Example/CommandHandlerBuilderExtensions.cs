using FluentValidation;

namespace ReactiveDomain.Domain.Tests.Example
{
    public static class CommandHandlerBuilderExtensions
    {
        public static CommandHandlerBuilder<TCommand> Validate<TCommand>(
            this CommandHandlerBuilder<TCommand> builder, 
            IValidator<TCommand> validator)
        {
            return builder.Pipe(
                next =>
                    (envelope, token) =>
                    {
                        validator.ValidateAndThrow(envelope.Command);
                        return next(envelope, token);
                    });
        }

        public static CommandHandlerBuilder<TCommand> ValidateAsync<TCommand>(
            this CommandHandlerBuilder<TCommand> builder,
            IValidator<TCommand> validator)
        {
            return builder.Pipe(
                next =>
                    async (envelope, token) =>
                    {
                        await validator.ValidateAndThrowAsync(envelope.Command);
                        await next(envelope, token);
                    });
        }
    }
}