# EventDrivenStateMachine

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

The `EventDrivenStateMachine` is the base class for event-sourced entities in Reactive Domain. It provides the core functionality for routing events, recording state changes, and managing the event history of an entity.

## Overview

In event-sourced systems, entities maintain their state by applying a sequence of events. The `EventDrivenStateMachine` implements this pattern by providing mechanisms for:

1. **Event Routing**: Directing events to the appropriate handler methods
2. **Event Recording**: Tracking events that have been applied but not yet persisted
3. **State Reconstruction**: Rebuilding entity state by replaying historical events
4. **Version Management**: Tracking the version of the entity based on applied events

The `EventDrivenStateMachine` serves as the foundation for `AggregateRoot`, which is the primary base class for domain entities in Reactive Domain.

## Namespace

```csharp
namespace ReactiveDomain
```

## Syntax

```csharp
public abstract class EventDrivenStateMachine : IEventSource
```

## Properties

### HasRecordedEvents

Indicates whether this instance has recorded events that haven't been persisted yet.

```csharp
public bool HasRecordedEvents { get; }
```

**Returns**: `true` if there are recorded events; otherwise, `false`.

### Id

Gets the unique identifier for this event-sourced entity.

```csharp
public Guid Id { get; protected set; }
```

### Version

Gets the current version of the entity, which represents the number of events that have been applied to it.

```csharp
public long Version { get; }
```

## Methods

### RestoreFromEvents(IEnumerable\<object\>)

Restores the state of the entity from a sequence of historical events.

```csharp
public void RestoreFromEvents(IEnumerable<object> events)
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The historical events to restore from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when the entity has recorded events that haven't been persisted.

### UpdateWithEvents(IEnumerable\<object\>, long)

Updates the entity with additional events, ensuring the expected version matches.

```csharp
public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to update with.
- `expectedVersion` (`System.Int64`): The expected version of the entity before applying these events.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when the entity has no historical events or when the expected version doesn't match the current version.

### TakeEvents()

Returns all events recorded since the entity was loaded or since the last time `TakeEvents()` was called, and clears the event recorder.

```csharp
public object[] TakeEvents()
```

**Returns**: An array of objects representing the recorded events.

### Register\<TEvent\>(Action\<TEvent\>)

Registers a route for a specific type of event to the logic that needs to be applied to this instance.

```csharp
protected void Register<TEvent>(Action<TEvent> route)
```

**Type Parameters**:
- `TEvent`: The type of event.

**Parameters**:
- `route` (`System.Action<TEvent>`): The logic to route the event to.

### Register(Type, Action\<object\>)

Registers a route for a specific type of event to the logic that needs to be applied to this instance.

```csharp
protected void Register(Type typeOfEvent, Action<object> route)
```

**Parameters**:
- `typeOfEvent` (`System.Type`): The type of event.
- `route` (`System.Action<object>`): The logic to route the event to.

### Raise(object)

Raises the specified event - applies it to this instance and records it in its history. This method is typically called `RaiseEvent` in the `AggregateRoot` subclass for clarity.

```csharp
protected void Raise(object @event)
```

**Parameters**:
- `event` (`System.Object`): The event to apply and record.

**Remarks**:
When an event is raised:
1. The `OnEventRaised` method is called, allowing for customization of the event raising process
2. The event is routed to the appropriate handler method via the `EventRouter`
3. The event is recorded in the `EventRecorder` for later persistence

This method is the core mechanism for changing the state of an event-sourced entity.

## Protected Methods

### TakeEventStarted()

Called before the events are taken from the event recorder.

```csharp
protected virtual void TakeEventStarted()
```

### TakeEventsCompleted()

Called after the events are taken from the event recorder and it has been reset.

```csharp
protected virtual void TakeEventsCompleted()
```

### OnEventRaised(object)

Called when an event is raised.

```csharp
protected virtual void OnEventRaised(object @event)
```

**Parameters**:
- `event` (`System.Object`): The event that was raised.

## Usage

The `EventDrivenStateMachine` is the foundation for implementing event-sourced entities in Reactive Domain. It's typically not used directly but through its subclass `AggregateRoot`.

### Basic Implementation

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Command handler
    public void CreateAccount(string accountNumber, ICorrelatedMessage source)
    {
        if (_isActive)
            throw new InvalidOperationException("Account already exists");
            
        // Raise an event using MessageBuilder for correlation
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(Id, accountNumber)));
    }
    
    // Command handler
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Raise an event - this will call Apply(FundsDeposited) and record the event
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
    
    // Command handler
    public void Withdraw(decimal amount, ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        // Raise an event - this will call Apply(FundsWithdrawn) and record the event
        RaiseEvent(MessageBuilder.From(source, () => new FundsWithdrawn(Id, amount)));
    }
    
    // Command handler
    public void CloseAccount(ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is already closed");
            
        if (_balance != 0)
            throw new InvalidOperationException("Cannot close account with non-zero balance");
            
        // Raise an event - this will call Apply(AccountClosed) and record the event
        RaiseEvent(MessageBuilder.From(source, () => new AccountClosed(Id)));
    }
    
    // Event handlers
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _accountNumber = @event.AccountNumber;
        _balance = 0;
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

### Using with a Repository

The `EventDrivenStateMachine` is typically used with a repository that handles loading and saving the entity's events:

```csharp
public class AccountService
{
    private readonly IRepository _repository;
    
    public AccountService(IRepository repository)
    {
        _repository = repository;
    }
    
    public void HandleCreateAccount(CreateAccount command)
    {
        // Create a new account
        var account = new Account(command.AccountId);
        account.CreateAccount(command.AccountNumber, command);
        
        // Save the account (persists the events)
        _repository.Save(account);
    }
    
    public void HandleDeposit(DepositFunds command)
    {
        // Load the account
        var account = _repository.GetById<Account>(command.AccountId);
        
        // Process the command
        account.Deposit(command.Amount, command);
        
        // Save the account (persists the new events)
        _repository.Save(account);
    }
}
```

## Best Practices

1. **Register Handlers in Constructor**: Always register event handlers in the constructor to ensure they're available when events are applied
2. **Command-Event Pattern**: Use command methods that validate business rules and raise events
3. **Private Apply Methods**: Keep event handlers (`Apply` methods) private to enforce that state changes only happen through events
4. **Idempotent Handlers**: Make event handlers idempotent so they can be safely replayed
5. **Validate Before Raising**: Validate business rules before raising events
6. **Use MessageBuilder**: Use `MessageBuilder` to create correlated events
7. **Avoid Side Effects**: Keep event handlers free of side effects like I/O operations
8. **Clear Error Messages**: Provide clear error messages when business rules are violated
9. **Consistent Naming**: Use consistent naming for command methods and event handlers
10. **Version Checking**: Use optimistic concurrency control with version checking when saving

## Common Pitfalls

1. **Missing Handlers**: Forgetting to register handlers for all event types
2. **Side Effects in Handlers**: Including side effects in event handlers can cause issues during replay
3. **Business Logic in Handlers**: Putting business logic in event handlers instead of command methods
4. **Circular Event References**: Raising events from within event handlers can cause infinite loops
5. **Ignoring Version Conflicts**: Not properly handling optimistic concurrency conflicts
6. **Large Aggregates**: Creating aggregates that handle too many responsibilities
7. **Direct State Modification**: Modifying state directly instead of through events

## Remarks

- The `EventDrivenStateMachine` implements the event sourcing pattern, where an entity's state is determined by a sequence of events.
- It uses an internal `EventRecorder` to track events that have been applied but not yet persisted.
- It uses an `EventRouter` to route events to the appropriate handler methods.
- When an event is raised using the `Raise` method, it is both applied to update the entity's state and recorded for later persistence.
- The `TakeEvents` method is typically called by a repository when persisting the entity's changes.
- The separation of concerns between command methods (which enforce business rules) and event handlers (which update state) is a key aspect of the design.

## Related Components

- [AggregateRoot](aggregate-root.md): The main subclass of `EventDrivenStateMachine` used for domain entities
- [EventRecorder](event-recorder.md): Component used internally to record events
- [IEventSource](ievent-source.md): Interface implemented by `EventDrivenStateMachine`
- [IRepository](./irepository.md): Interface for repositories that load and save event-sourced entities
- [Command](./command.md): Messages that trigger state changes in event-sourced entities
- [Event](./event.md): Messages that represent state changes in event-sourced entities
- [MessageBuilder](./message-builder.md): Factory for creating correlated events

---

**Navigation**:
- [← Previous: EventRecorder](./event-recorder.md)
- [↑ Back to Top](#eventdrivenstatemachine)
- [→ Next: AggregateRoot](./aggregate-root.md)
