# Event

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`Event` is a base class in Reactive Domain that implements the `ICorrelatedMessage` interface and serves as the foundation for all event messages in the system. Events represent facts that have occurred in the domain and are a critical component of event-sourced systems.

## Overview

Events in Reactive Domain represent immutable facts that have occurred in the system. They are the historical record of changes to the domain and form the basis of event sourcing. The `Event` base class provides common functionality for all event implementations, including correlation and causation tracking, which is essential for debugging and auditing in distributed systems.

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

- **Message Identity**: Provides a unique `MsgId` for each event instance
- **Correlation Tracking**: Implements `ICorrelatedMessage` for tracking related messages across the system
- **Immutability**: Ensures events are immutable after creation, preserving the historical record
- **Type Safety**: Provides a type-safe base for all event implementations in the domain

## Usage

### Defining an Event

To create a new event type, inherit from the `Event` base class:

```csharp
public class AccountCreated : Event
{
    public readonly Guid AccountId;
    public readonly string AccountNumber;
    public readonly string CustomerName;
    
    // Constructor for new events (starts a new correlation chain)
    public AccountCreated(Guid accountId, string accountNumber, string customerName)
        : base()
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
    
    // Constructor for correlated events (maintains the correlation chain)
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

It's recommended to use the `MessageBuilder` factory to create events with proper correlation information:

```csharp
// Create an event from a command (maintains correlation chain)
ICorrelatedMessage command = // ... existing command
var createdEvent = MessageBuilder.From(command, () => new AccountCreated(
    Guid.NewGuid(), 
    "ACC-123", 
    "John Doe"
));

// Create a new event (starts a new correlation chain)
var newEvent = MessageBuilder.New(() => new AccountCreated(
    Guid.NewGuid(),
    "ACC-456",
    "Jane Smith"
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

Events are produced by aggregates in response to commands. This is a core pattern in Domain-Driven Design and CQRS:

```csharp
public class Account : AggregateRoot
{
    private string _accountNumber;
    private string _customerName;
    private decimal _balance;
    
    // Constructor for creating a new account
    public Account(Guid id, ICorrelatedMessage source) : base(id)
    {
        // Create and apply the AccountCreated event
        Apply(MessageBuilder.From(source, () => new AccountCreated(
            id, 
            "ACC-" + id.ToString().Substring(0, 8), 
            "New Customer"
        )));
    }
    
    // Event handler for AccountCreated
    private void Apply(AccountCreated @event)
    {
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
        _balance = 0;
    }
    
    // Command handler for deposit
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Create and apply the FundsDeposited event
        Apply(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
    
    // Event handler for FundsDeposited
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

1. **Immutable Events**: Make all event properties read-only to preserve the historical record
2. **Past Tense Names**: Use past tense naming convention (e.g., `AccountCreated`, `FundsDeposited`) to indicate that these are facts that have occurred
3. **Complete Data**: Include all data needed to understand what happened, making events self-contained
4. **Use MessageBuilder**: Always use `MessageBuilder` to create events with proper correlation information
5. **Versioning Strategy**: Plan for event schema evolution to handle changes over time
6. **Meaningful Events**: Design events to represent meaningful business occurrences, not just data changes
7. **Event Documentation**: Document the purpose and content of each event type for better understanding

## Common Pitfalls

1. **Mutable Events**: Avoid mutable properties in events as they should represent immutable facts
2. **Business Logic in Events**: Events should be simple data carriers without business logic or behavior
3. **Missing Correlation**: Ensure correlation information is properly maintained throughout the system
4. **Insufficient Data**: Include enough data in events to fully understand what happened without external context
5. **Overloaded Events**: Avoid creating events that represent multiple business occurrences
6. **Temporal Coupling**: Ensure events can be processed in any order by making them self-contained

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
