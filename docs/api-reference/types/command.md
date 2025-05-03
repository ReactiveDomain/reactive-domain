# Command

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`Command` is a base class in Reactive Domain that implements the `ICorrelatedMessage` interface and serves as the foundation for all command messages in the system.

## Overview

Commands in Reactive Domain represent requests for the system to perform an action. They are part of the write side of the CQRS pattern and typically result in state changes. The `Command` base class provides common functionality for all command implementations, including correlation and causation tracking.

## Class Definition

```csharp
public abstract class Command : ICommand, ICorrelatedMessage
{
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    protected Command()
    {
        MsgId = Guid.NewGuid();
        CorrelationId = MsgId;
        CausationId = MsgId;
    }
    
    protected Command(Guid correlationId, Guid causationId)
    {
        MsgId = Guid.NewGuid();
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Key Features

- **Message Identity**: Provides a unique `MsgId` for each command
- **Correlation Tracking**: Implements `ICorrelatedMessage` for tracking related messages
- **Immutability**: Ensures commands are immutable after creation
- **Type Safety**: Provides a type-safe base for all command implementations

## Usage

### Defining a Command

To create a new command type, inherit from the `Command` base class:

```csharp
public class CreateAccount : Command
{
    public readonly Guid AccountId;
    public readonly string AccountNumber;
    public readonly string CustomerName;
    
    public CreateAccount(Guid accountId, string accountNumber, string customerName)
        : base()
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
    
    // Constructor for correlated commands
    public CreateAccount(Guid accountId, string accountNumber, string customerName, 
                         Guid correlationId, Guid causationId)
        : base(correlationId, causationId)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
}
```

### Using MessageBuilder with Commands

It's recommended to use the `MessageBuilder` factory to create commands with proper correlation:

```csharp
// Create a new command that starts a correlation chain
var createCommand = MessageBuilder.New(() => new CreateAccount(
    Guid.NewGuid(), 
    "ACC-123", 
    "John Doe"
));

// Create a command from an existing message
var depositCommand = MessageBuilder.From(createCommand, () => new DepositFunds(
    ((CreateAccount)createCommand).AccountId, 
    100.00m
));
```

### Handling Commands

Commands are typically handled by command handlers:

```csharp
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly ICorrelatedRepository _repository;
    
    public CreateAccountHandler(ICorrelatedRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccount command)
    {
        var account = new Account(command.AccountId, command);
        _repository.Save(account, command);
    }
}
```

## Integration with Aggregates

Commands are used to modify aggregates, which then produce events:

```csharp
public class Account : AggregateRoot
{
    public Account(Guid id, ICorrelatedMessage source) : base(id)
    {
        Apply(MessageBuilder.From(source, () => new AccountCreated(id, source.CorrelationId, source.MsgId)));
    }
    
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        Apply(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
}
```

## Best Practices

1. **Immutable Commands**: Make all command properties read-only
2. **Descriptive Names**: Use verb-noun naming convention (e.g., `CreateAccount`, `DepositFunds`)
3. **Minimal Data**: Include only the data needed to perform the action
4. **Use MessageBuilder**: Always use `MessageBuilder` to create commands with proper correlation
5. **Validation**: Validate commands before processing them

## Common Pitfalls

1. **Mutable Commands**: Avoid mutable properties in commands
2. **Business Logic in Commands**: Commands should be simple data carriers without business logic
3. **Missing Correlation**: Ensure correlation information is properly maintained
4. **Large Commands**: Keep commands focused and minimal

## Related Components

- [ICommand](./icommand.md): Interface for command messages
- [ICorrelatedMessage](./icorrelated-message.md): Interface for messages with correlation information
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [ICommandHandler](./icommand-handler.md): Interface for handling commands

---

**Navigation**:
- [← Previous: MessageBuilder](./message-builder.md)
- [↑ Back to Top](#command)
- [→ Next: Event](./event.md)
