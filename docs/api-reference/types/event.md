# Event

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`Event` is a base class in Reactive Domain that implements the `ICorrelatedMessage` interface and serves as the foundation for all event messages in the system.

## Overview

Events in Reactive Domain represent facts that have occurred in the system. They are immutable records of something that happened and form the basis of event sourcing. The `Event` base class provides common functionality for all event implementations, including correlation and causation tracking.

## Class Definition

```csharp
public abstract class Event : IEvent, ICorrelatedMessage
{
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    protected Event()
    {
        MsgId = Guid.NewGuid();
        CorrelationId = MsgId;
        CausationId = MsgId;
    }
    
    protected Event(Guid correlationId, Guid causationId)
    {
        MsgId = Guid.NewGuid();
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Key Features

- **Message Identity**: Provides a unique `MsgId` for each event
- **Correlation Tracking**: Implements `ICorrelatedMessage` for tracking related messages
- **Immutability**: Ensures events are immutable after creation
- **Type Safety**: Provides a type-safe base for all event implementations

## Usage

### Defining an Event

To create a new event type, inherit from the `Event` base class:

```csharp
public class AccountCreated : Event
{
    public readonly Guid AccountId;
    public readonly string AccountNumber;
    public readonly string CustomerName;
    
    public AccountCreated(Guid accountId, string accountNumber, string customerName)
        : base()
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
    
    // Constructor for correlated events
    public AccountCreated(Guid accountId, string accountNumber, string customerName, 
                         Guid correlationId, Guid causationId)
        : base(correlationId, causationId)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
}
```

### Using MessageBuilder with Events

It's recommended to use the `MessageBuilder` factory to create events with proper correlation:

```csharp
// Create an event from a command
ICorrelatedMessage command = // ... existing command
var createdEvent = MessageBuilder.From(command, () => new AccountCreated(
    Guid.NewGuid(), 
    "ACC-123", 
    "John Doe"
));
```

### Handling Events

Events are typically handled by event handlers:

```csharp
public class AccountCreatedHandler : IEventHandler<AccountCreated>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountCreatedHandler(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(@event.AccountNumber, @event.CustomerName, 0);
        _repository.Save(accountSummary);
    }
}
```

## Integration with Aggregates

Events are produced by aggregates in response to commands:

```csharp
public class Account : AggregateRoot
{
    private string _accountNumber;
    private string _customerName;
    private decimal _balance;
    
    public Account(Guid id, ICorrelatedMessage source) : base(id)
    {
        Apply(MessageBuilder.From(source, () => new AccountCreated(id, "ACC-" + id.ToString().Substring(0, 8), "New Customer")));
    }
    
    private void Apply(AccountCreated @event)
    {
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
        _balance = 0;
    }
    
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        Apply(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
}
```

## Event Sourcing

Events are the foundation of event sourcing, where the state of an aggregate is reconstructed by replaying events:

```csharp
public void LoadFromHistory(IEnumerable<IEvent> history)
{
    foreach (var @event in history)
    {
        Dispatch(@event);
    }
}

private void Dispatch(IEvent @event)
{
    // Use reflection or a dictionary to find the appropriate Apply method
    // This is a simplified example
    var method = GetType().GetMethod("Apply", 
        BindingFlags.NonPublic | BindingFlags.Instance, 
        null, 
        new[] { @event.GetType() }, 
        null);
        
    if (method != null)
    {
        method.Invoke(this, new object[] { @event });
    }
}
```

## Best Practices

1. **Immutable Events**: Make all event properties read-only
2. **Past Tense Names**: Use past tense naming convention (e.g., `AccountCreated`, `FundsDeposited`)
3. **Complete Data**: Include all data needed to understand what happened
4. **Use MessageBuilder**: Always use `MessageBuilder` to create events with proper correlation
5. **Versioning Strategy**: Plan for event schema evolution

## Common Pitfalls

1. **Mutable Events**: Avoid mutable properties in events
2. **Business Logic in Events**: Events should be simple data carriers without business logic
3. **Missing Correlation**: Ensure correlation information is properly maintained
4. **Insufficient Data**: Include enough data to fully understand what happened

## Related Components

- [IEvent](./ievent.md): Interface for event messages
- [ICorrelatedMessage](./icorrelated-message.md): Interface for messages with correlation information
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [IEventHandler](./ievent-handler.md): Interface for handling events

---

**Navigation**:
- [← Previous: Command](./command.md)
- [↑ Back to Top](#event)
- [→ Next: ICorrelatedMessage](./icorrelated-message.md)
