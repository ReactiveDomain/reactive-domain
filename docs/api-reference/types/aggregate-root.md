# AggregateRoot Class

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `AggregateRoot` class is a base class for domain aggregates in Reactive Domain. It implements the `IEventSource` interface and provides common functionality for event sourcing. Aggregates are the central building blocks in Domain-Driven Design (DDD) and serve as the primary consistency boundary for business rules and invariants.

In event-sourced systems, aggregates don't store their state directly but instead derive it from a sequence of events. The `AggregateRoot` class provides the infrastructure to record, apply, and retrieve these events, making it easier to implement event-sourced aggregates.

## Constructors

### AggregateRoot(Guid)

Initializes a new instance of the `AggregateRoot` class with the specified ID. This constructor is typically used when creating a new aggregate.

```csharp
protected AggregateRoot(Guid id);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.

**Example**:
```csharp
public class Account : AggregateRoot
{
    public Account(Guid id) : base(id)
    {
        // Initialize a new account
        RaiseEvent(new AccountCreated(id));
    }
}
```

### AggregateRoot(Guid, ICorrelatedMessage)

Initializes a new instance of the `AggregateRoot` class with the specified ID and correlation source. This constructor is used when creating a new aggregate in response to a command, ensuring proper correlation tracking.

```csharp
protected AggregateRoot(Guid id, ICorrelatedMessage source);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Example**:
```csharp
public class Account : AggregateRoot
{
    public Account(Guid id, ICorrelatedMessage source) : base(id, source)
    {
        // Initialize a new account with correlation
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(id)));
    }
}
```

### AggregateRoot(Guid, IEnumerable\<object\>)

Initializes a new instance of the `AggregateRoot` class with the specified ID and restores it from the provided events. This constructor is typically used by repositories when reconstituting an aggregate from its event history.

```csharp
protected AggregateRoot(Guid id, IEnumerable<object> events);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to restore from.

**Example**:
```csharp
// Inside a repository implementation
public TAggregate GetById<TAggregate>(Guid id) where TAggregate : AggregateRoot
{
    // Retrieve events from the event store
    var events = _eventStore.GetEvents(id);
    
    // Create an instance of the aggregate with its history
    return (TAggregate)Activator.CreateInstance(
        typeof(TAggregate), 
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new object[] { id, events },
        null);
}
```

## Properties

### Id

Gets the unique identifier for this aggregate. This property is crucial for identifying the aggregate in the system and is used as the stream identifier in event stores.

```csharp
public Guid Id { get; }
```

**Property Type**: `System.Guid`  
**Accessibility**: `get`

### ExpectedVersion

Gets or sets the expected version this aggregate is at. This is used for optimistic concurrency control when saving the aggregate to an event store, preventing lost updates in concurrent scenarios.

```csharp
public long ExpectedVersion { get; set; }
```

**Property Type**: `System.Int64`  
**Accessibility**: `get`, `set`

**Example**:
```csharp
// Inside a repository implementation
public void Save(AggregateRoot aggregate)
{
    var events = aggregate.TakeEvents();
    
    try {
        _eventStore.AppendToStream(
            aggregate.Id,
            aggregate.ExpectedVersion,
            events);
    }
    catch (ConcurrencyException ex) {
        // Handle the case where another process has modified the aggregate
        throw new AggregateVersionException(
            $"Aggregate {aggregate.Id} has been modified concurrently", 
            ex);
    }
}
```

## Methods

### RaiseEvent

Raises an event, which will be recorded and applied to the aggregate. This is the primary method for changing the state of an aggregate in an event-sourced system.

```csharp
protected void RaiseEvent(object @event);
```

**Parameters**:
- `event` (`System.Object`): The event to raise.

**Example**:
```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Record and apply the event
        RaiseEvent(new AmountDeposited(Id, amount));
    }
    
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
}
```

### RestoreFromEvents

Restores this aggregate from the history of events. This method is typically called by the constructor or by a repository when reconstituting an aggregate.

```csharp
public void RestoreFromEvents(IEnumerable<object> events);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to restore from.

**Example**:
```csharp
// Inside a repository implementation
public TAggregate GetById<TAggregate>(Guid id) where TAggregate : AggregateRoot, new()
{
    var events = _eventStore.GetEvents(id);
    var aggregate = new TAggregate();
    
    // Use reflection to set the Id property
    typeof(TAggregate)
        .GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
        .SetValue(aggregate, id);
    
    // Restore the aggregate state from events
    aggregate.RestoreFromEvents(events);
    
    return aggregate;
}
```

### UpdateWithEvents

Updates this aggregate with the provided events, starting from the expected version. This method is used when new events need to be applied to an existing aggregate, such as when handling concurrent modifications.

```csharp
public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to update with.
- `expectedVersion` (`System.Int64`): The expected version to start from.

### TakeEvents

Takes the recorded history of events from this aggregate. This method is typically called by a repository when saving the aggregate to extract the new events that need to be persisted.

```csharp
public object[] TakeEvents();
```

**Returns**: `System.Object[]` - The recorded events.

**Example**:
```csharp
// Inside a repository implementation
public void Save(AggregateRoot aggregate)
{
    // Extract the new events from the aggregate
    var events = aggregate.TakeEvents();
    
    if (events.Length > 0)
    {
        // Persist the events to the event store
        _eventStore.AppendToStream(
            aggregate.Id,
            aggregate.ExpectedVersion,
            events);
            
        // Update the expected version for next save
        aggregate.ExpectedVersion += events.Length;
    }
}
```

## Usage

The `AggregateRoot` class is designed to be subclassed by domain aggregates. Subclasses should:

1. Define private `Apply` methods for each event type to update the aggregate's state
2. Use the `RaiseEvent` method to record and apply events when handling commands
3. Define public methods that represent domain operations and enforce business rules
4. Keep the aggregate's state private and expose it through controlled methods

## Example Implementation

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isClosed;
    private string _accountNumber;
    private string _customerName;
    
    // Constructor for creating a new account
    public Account(Guid id) : base(id)
    {
        // Initialize with default values
        RaiseEvent(new AccountCreated(id, "ACC-" + id.ToString().Substring(0, 8), "New Customer"));
    }
    
    // Constructor for creating a new account with correlation
    public Account(Guid id, ICorrelatedMessage source) : base(id, source)
    {
        // Initialize with default values and maintain correlation
        RaiseEvent(MessageBuilder.From(source, () => 
            new AccountCreated(id, "ACC-" + id.ToString().Substring(0, 8), "New Customer")));
    }
    
    // Constructor for restoring an account from events
    protected Account(Guid id, IEnumerable<object> events) : base(id, events)
    {
        // The base constructor will call RestoreFromEvents
    }
    
    // Command handler for deposit
    public void Deposit(decimal amount, ICorrelatedMessage source = null)
    {
        // Enforce business rules
        if (_isClosed)
            throw new InvalidOperationException("Cannot deposit to a closed account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
        
        // Create and apply the event
        if (source != null)
        {
            RaiseEvent(MessageBuilder.From(source, () => new AmountDeposited(Id, amount)));
        }
        else
        {
            RaiseEvent(new AmountDeposited(Id, amount));
        }
    }
    
    // Command handler for withdrawal
    public void Withdraw(decimal amount, ICorrelatedMessage source = null)
    {
        // Enforce business rules
        if (_isClosed)
            throw new InvalidOperationException("Cannot withdraw from a closed account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
        
        // Create and apply the event
        if (source != null)
        {
            RaiseEvent(MessageBuilder.From(source, () => new AmountWithdrawn(Id, amount)));
        }
        else
        {
            RaiseEvent(new AmountWithdrawn(Id, amount));
        }
    }
    
    // Command handler for closing the account
    public void Close(ICorrelatedMessage source = null)
    {
        // Enforce business rules
        if (_isClosed)
            throw new InvalidOperationException("Account is already closed");
            
        if (_balance > 0)
            throw new InvalidOperationException("Cannot close account with positive balance");
        
        // Create and apply the event
        if (source != null)
        {
            RaiseEvent(MessageBuilder.From(source, () => new AccountClosed(Id)));
        }
        else
        {
            RaiseEvent(new AccountClosed(Id));
        }
    }
    
    // Query method for balance
    public decimal GetBalance()
    {
        return _balance;
    }
    
    // Query method for account status
    public bool IsClosed()
    {
        return _isClosed;
    }
    
    // Event handler for AccountCreated
    private void Apply(AccountCreated @event)
    {
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
        _balance = 0;
        _isClosed = false;
    }
    
    // Event handler for AmountDeposited
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    // Event handler for AmountWithdrawn
    private void Apply(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
    
    // Event handler for AccountClosed
    private void Apply(AccountClosed @event)
    {
        _isClosed = true;
    }
}
```

## Best Practices

1. **Keep Aggregates Small**: Focus on a single business concept and limit the number of properties and methods
2. **Enforce Invariants**: Use command methods to enforce business rules and maintain consistency
3. **Event-First Design**: Design your events before your commands to focus on the business outcomes
4. **Private State**: Keep aggregate state private and expose it through controlled methods
5. **Idempotent Apply Methods**: Ensure that applying the same event multiple times doesn't cause issues
6. **Immutable Events**: Use immutable events to ensure the event history remains unchanged
7. **Correlation Tracking**: Use the correlation-aware constructor when creating aggregates from commands
8. **Optimistic Concurrency**: Use the ExpectedVersion property to prevent lost updates

## Common Pitfalls

1. **Large Aggregates**: Avoid creating aggregates that are too large or contain too many responsibilities
2. **Public State Modification**: Don't allow direct modification of aggregate state from outside
3. **Missing Business Rules**: Ensure all business rules are enforced in command methods
4. **Ignoring Version Conflicts**: Always handle optimistic concurrency exceptions properly
5. **Complex Apply Methods**: Keep event handlers (Apply methods) simple and focused
6. **Side Effects in Apply Methods**: Avoid side effects like I/O operations in Apply methods
7. **Circular Event References**: Avoid raising events from within Apply methods

## Inheritance Hierarchy

- `System.Object`
  - `ReactiveDomain.Foundation.AggregateRoot`

## Implemented Interfaces

- `ReactiveDomain.IEventSource`

## Related Types

- [IEventSource](ievent-source.md): The interface implemented by `AggregateRoot`
- [ICorrelatedEventSource](icorrelated-event-source.md): Interface for correlation tracking
- [ISnapshotSource](isnapshot-source.md): Interface for snapshot support
- [IRepository](irepository.md): Interface for repositories that work with aggregates
- [EventRecorder](event-recorder.md): Utility used internally by `AggregateRoot` to record events
- [Command](./command.md): Messages that trigger state changes in aggregates
- [Event](./event.md): Messages that represent state changes in aggregates
- [MessageBuilder](./message-builder.md): Factory for creating correlated events from aggregates
- [ReadModelBase](./read-model-base.md): Read models that are updated based on events from aggregates

For a comprehensive view of how aggregates interact with other components, see the [Key Component Relationships](../../architecture.md#key-component-relationships) section in the Architecture Guide, particularly the [Command and Event Relationship](../../architecture.md#command-and-event-relationship) and [Aggregate and Repository Interaction](../../architecture.md#aggregate-and-repository-interaction) diagrams.

[↑ Back to Top](#aggregateroot-class) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
