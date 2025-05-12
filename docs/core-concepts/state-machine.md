# Event-Driven State Machine

[← Back to Core Concepts](../core-concepts.md)

## Overview

The `EventDrivenStateMachine` is a fundamental component in Reactive Domain that implements the finite state machine pattern for event-sourced entities. It serves as the base class for both aggregate roots and process managers, providing the core functionality for event handling, state transitions, and event recording.

## Purpose

The primary purposes of the `EventDrivenStateMachine` are:

1. **State Management**: Maintain the current state of an entity based on the events it has processed
2. **Event Routing**: Direct events to the appropriate handlers based on their type
3. **Event Recording**: Track new events that have been applied to the entity
4. **Event Sourcing Support**: Provide mechanisms for restoring entity state from an event stream

## Key Components

### Event Router

The `EventRouter` handles the dispatching of events to the appropriate handler methods. When an event is received, the router determines which handler method should process it based on the event type.

```csharp
// Inside EventDrivenStateMachine
protected readonly EventRouter Router;

// Registering an event handler
protected void Register<TEvent>(Action<TEvent> route) {
    Router.RegisterRoute(route);
}
```

### Event Recorder

The `EventRecorder` keeps track of all new events that have been applied to the entity. These events represent state changes that need to be persisted to the event store.

```csharp
// Inside EventDrivenStateMachine
private readonly EventRecorder _recorder;

// Recording an event
protected void Raise(object @event) {
    OnEventRaised(@event);
    Router.Route(@event);
    _recorder.Record(@event);
}
```

### Version Tracking

The `EventDrivenStateMachine` maintains a version number that represents the number of events that have been applied to the entity. This is used for optimistic concurrency control when saving changes.

```csharp
private long _version;
public long Version => _version;
```

## Core Operations

### Registering Event Handlers

Event handlers are registered using the `Register<TEvent>` method, which associates an event type with a handler method:

```csharp
public class Account : AggregateRoot // AggregateRoot inherits from EventDrivenStateMachine
{
    private decimal _balance;
    private bool _isActive;
    
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Event handlers
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _balance = @event.InitialDeposit;
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    // More event handlers...
}
```

### Raising Events

New events are raised using the `Raise` method, which:
1. Applies the event to the current state by routing it to the appropriate handler
2. Records the event for later persistence

```csharp
public void Deposit(decimal amount, string reference, ICorrelatedMessage source)
{
    if (!_isActive)
        throw new InvalidOperationException("Cannot deposit to a closed account");
    
    if (amount <= 0)
        throw new ArgumentException("Deposit amount must be positive", nameof(amount));
    
    // Raise the event
    RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(Id, amount, reference)));
}
```

### Restoring State from Events

The `RestoreFromEvents` method is used to rebuild the entity's state from its event history:

```csharp
// Inside EventDrivenStateMachine
public void RestoreFromEvents(IEnumerable<object> events)
{
    if (events == null)
        throw new ArgumentNullException(nameof(events));
    
    if (_recorder.HasRecordedEvents)
        throw new InvalidOperationException("Restoring from events is not possible when an instance has recorded events.");

    foreach (var @event in events)
    {
        if (_version < 0)
            _version = 0;
        else
            _version++;
        
        Router.Route(@event);
    }
}
```

### Taking Uncommitted Events

The `TakeEvents` method returns all new events that have been raised since the entity was loaded or since the last time `TakeEvents` was called:

```csharp
// Inside EventDrivenStateMachine
public object[] TakeEvents()
{
    TakeEventStarted();
    var records = _recorder.RecordedEvents;
    _recorder.Reset();
    _version += records.Length;
    TakeEventsCompleted();
    return records;
}
```

## State Machine in Action

The event-driven state machine pattern in Reactive Domain follows these steps:

1. **Initialization**: Create a new instance of an entity (aggregate or process manager)
2. **Registration**: Register event handlers for each event type the entity needs to handle
3. **Command Handling**: Process commands by validating business rules and raising appropriate events
4. **Event Application**: Apply events to the entity's state through the registered event handlers
5. **Event Recording**: Record new events for later persistence
6. **State Persistence**: Save the new events to the event store
7. **State Restoration**: When loading an entity, restore its state by replaying all historical events

## Example: Account State Machine

Here's a complete example of an `Account` aggregate that uses the event-driven state machine pattern:

```csharp
public class Account : AggregateRoot
{
    // State variables
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    
    // Constructor
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Command handlers
    public void CreateAccount(string accountNumber, decimal initialDeposit, ICorrelatedMessage source)
    {
        if (_isActive)
            throw new InvalidOperationException("Account already exists");
        
        if (initialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative", nameof(initialDeposit));
        
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(Id, accountNumber, initialDeposit)));
    }
    
    public void Deposit(decimal amount, string reference, ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Cannot deposit to a closed account");
        
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));
        
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(Id, amount, reference)));
    }
    
    public void Withdraw(decimal amount, string reference, ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Cannot withdraw from a closed account");
        
        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
        
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
        
        RaiseEvent(MessageBuilder.From(source, () => new FundsWithdrawn(Id, amount, reference)));
    }
    
    public void CloseAccount(ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is already closed");
        
        if (_balance > 0)
            throw new InvalidOperationException("Cannot close account with positive balance");
        
        RaiseEvent(MessageBuilder.From(source, () => new AccountClosed(Id)));
    }
    
    // Event handlers
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _accountNumber = @event.AccountNumber;
        _balance = @event.InitialDeposit;
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
    
    private void Apply(AccountClosed @event)
    {
        _isActive = false;
    }
}
```

## Benefits of the Event-Driven State Machine

1. **Clear Separation of Concerns**: Command methods validate business rules and raise events, while event handlers update state.
2. **Audit Trail**: All state changes are recorded as events, providing a complete history of the entity.
3. **Temporal Queries**: The ability to determine the state of an entity at any point in time by replaying events up to that point.
4. **Testability**: Easy to test by verifying that the correct events are raised in response to commands.
5. **Event Replay**: The ability to rebuild the state of an entity by replaying its event history.

## Best Practices

1. **Keep Event Handlers Simple**: Event handlers should only update the entity's state and not perform any validation or raise additional events.
2. **Validate in Command Methods**: All business rules should be validated in command methods before raising events.
3. **Use Meaningful Event Names**: Events should be named in the past tense and clearly describe what happened (e.g., `AccountCreated`, `FundsDeposited`).
4. **Register All Event Handlers**: Ensure that all event types that can be applied to an entity have registered handlers.
5. **Avoid Side Effects in Event Handlers**: Event handlers should only update the entity's internal state and not interact with external systems.
6. **Use Correlation for Message Tracking**: Always use the `MessageBuilder` to create correlated events from command sources.
7. **Implement Proper Error Handling**: Add appropriate null checks and validation in command handlers.

## Relationship with Other Components

The `EventDrivenStateMachine` is a foundational component in Reactive Domain that interacts with several other key components:

1. **Repository**: Loads and saves aggregates by retrieving and storing their events.
2. **Event Store**: Persists the events generated by aggregates.
3. **Command Bus**: Routes commands to the appropriate handlers, which then operate on aggregates.
4. **Event Bus**: Publishes events to interested subscribers after they are persisted.
5. **Process Manager**: Coordinates activities across multiple aggregates in response to events.

## Navigation

**Section Navigation**:
- [← Previous: Event Sourcing](event-sourcing.md)
- [↑ Parent: Core Concepts](../core-concepts.md)
- [→ Next: CQRS](cqrs.md)

**Quick Links**:
- [Home](../README.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
