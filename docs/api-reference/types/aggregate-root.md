# AggregateRoot Class

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `AggregateRoot` class is a base class for domain aggregates in Reactive Domain. It inherits from `EventDrivenStateMachine` and implements the `IEventSource` interface, providing comprehensive functionality for event sourcing. Aggregates are the central building blocks in Domain-Driven Design (DDD) and serve as the primary consistency boundary for business rules and invariants.

In event-sourced systems, aggregates don't store their state directly but instead derive it from a sequence of events. The `AggregateRoot` class provides the infrastructure to:

1. **Record Events**: Capture domain events that represent state changes
2. **Apply Events**: Update the aggregate's state based on these events
3. **Enforce Invariants**: Validate business rules before state changes
4. **Maintain Version**: Track the version for optimistic concurrency control
5. **Support Correlation**: Maintain correlation and causation chains for traceability

Aggregates in Reactive Domain follow the Command-Event pattern, where:

- **Commands** are requests to change the aggregate's state, which may be rejected if they violate business rules
- **Events** are facts that have occurred, representing actual state changes
- **Apply Methods** update the aggregate's state in response to events

This pattern ensures that all state changes are explicit, traceable, and can be replayed to reconstruct the aggregate's state at any point in time.

## Constructors

### AggregateRoot(Guid)

Initializes a new instance of the `AggregateRoot` class with the specified ID. This constructor is typically used when creating a new aggregate or when loading an aggregate from the repository.

```csharp
protected AggregateRoot(Guid id);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.

**Example**:
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
    
    // Method to initialize a new account
    public void Initialize(string accountNumber, string customerName, ICorrelatedMessage source)
    {
        // Enforce business rules
        if (string.IsNullOrEmpty(accountNumber))
            throw new ArgumentException("Account number is required", nameof(accountNumber));
            
        if (string.IsNullOrEmpty(customerName))
            throw new ArgumentException("Customer name is required", nameof(customerName));
            
        // Raise the event
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(
            Id, accountNumber, customerName, DateTime.UtcNow)));
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
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    
    // Constructor for creating a new account with correlation
    public Account(Guid id, CreateAccount command) : base(id, command)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
        
        // Initialize the aggregate by raising an event
        RaiseEvent(MessageBuilder.From(command, () => new AccountCreated(
            id, 
            command.AccountNumber, 
            command.CustomerName, 
            DateTime.UtcNow)));
            
        // If initial deposit is provided, perform the deposit
        if (command.InitialDeposit > 0)
        {
            RaiseEvent(MessageBuilder.From(command, () => new FundsDeposited(
                id, 
                command.InitialDeposit, 
                "Initial deposit", 
                DateTime.UtcNow)));
        }
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

### Register<TEvent>

Registers an event handler method for a specific event type. This method is typically called in the constructor of the aggregate to set up event handling.

```csharp
protected void Register<TEvent>(Action<TEvent> handler);
```

**Parameters**:
- `handler` (`System.Action<TEvent>`): The method that will handle events of type `TEvent`.

**Example**:
```csharp
public class Account : AggregateRoot
{
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Event handlers
    private void Apply(AccountCreated @event) { /* ... */ }
    private void Apply(FundsDeposited @event) { /* ... */ }
    private void Apply(FundsWithdrawn @event) { /* ... */ }
    private void Apply(AccountClosed @event) { /* ... */ }
}
```

### RaiseEvent

Raises an event, which will be recorded and applied to the aggregate. This is the primary method for creating and handling new events in an event-sourced system. When called, `RaiseEvent()` does two things:

1. It applies the event to the aggregate's state by calling the appropriate `Apply()` method
2. It records the event for persistence in the event store

```csharp
protected void RaiseEvent(object @event);
```

**Parameters**:
- `event` (`System.Object`): The event to raise. This should be created using the `MessageBuilder` to ensure proper correlation tracking.

> **Important**: The `RaiseEvent()` method does NOT automatically add correlation information to events. You must explicitly use `MessageBuilder.From(source, () => new Event(...))` to create events with proper correlation information. Simply passing an event to `RaiseEvent()` without using MessageBuilder will result in lost correlation tracking.

**Example**:
```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    
    // Command handler method
    public void Deposit(decimal amount, string reference, ICorrelatedMessage source)
    {
        // Validate business rules
        if (!_isActive)
            throw new InvalidOperationException("Cannot deposit to an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Raise the event with proper correlation
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(
            Id, 
            amount, 
            reference, 
            DateTime.UtcNow)));
    }
    
    // Event handler method
    private void Apply(FundsDeposited @event)
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
    // Retrieve events from the event store
    var events = _eventStore.GetEvents(GetStreamName(typeof(TAggregate), id));
    
    // Create a new instance of the aggregate
    var aggregate = new TAggregate();
    
    // Set the ID property
    typeof(TAggregate)
        .GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
        .SetValue(aggregate, id);
    
    // Restore the aggregate state from events
    aggregate.RestoreFromEvents(events);
    
    // Set the expected version for optimistic concurrency
    aggregate.ExpectedVersion = events.Count();
    
    return aggregate;
}
```

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
        try
        {
            // Persist the events to the event store
            _eventStore.AppendToStream(
                GetStreamName(aggregate.GetType(), aggregate.Id),
                aggregate.ExpectedVersion,
                events);
                
            // Update the expected version for next save
            aggregate.ExpectedVersion += events.Length;
            
            // Publish events to event handlers
            foreach (var @event in events)
            {
                _eventPublisher.Publish(@event);
            }
        }
        catch (ConcurrencyException ex)
        {
            // Handle optimistic concurrency conflicts
            throw new AggregateVersionException(
                $"Aggregate {aggregate.Id} has been modified concurrently", 
                ex);
        }
    }
}
```

### Initialize

A common pattern in Reactive Domain is to use an `Initialize` method instead of raising events directly in the constructor. This allows for more explicit validation and better control over the initialization process.

```csharp
public void Initialize(/* parameters */, ICorrelatedMessage source)
{
    // Validate parameters
    // ...
    
    // Raise initialization event
    RaiseEvent(MessageBuilder.From(source, () => new EntityCreated(/* ... */)));
}
```

**Example**:
```csharp
public class Product : AggregateRoot
{
    private string _name;
    private string _sku;
    private decimal _price;
    private bool _isActive;
    
    public Product(Guid id) : base(id)
    {
        Register<ProductCreated>(Apply);
        Register<ProductPriceChanged>(Apply);
        Register<ProductDeactivated>(Apply);
    }
    
    public void Initialize(string name, string sku, decimal price, ICorrelatedMessage source)
    {
        // Validate business rules
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Product name is required", nameof(name));
            
        if (string.IsNullOrEmpty(sku))
            throw new ArgumentException("SKU is required", nameof(sku));
            
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));
            
        // Raise the event
        RaiseEvent(MessageBuilder.From(source, () => new ProductCreated(
            Id, name, sku, price, DateTime.UtcNow)));
    }
    
    private void Apply(ProductCreated @event)
    {
        _name = @event.Name;
        _sku = @event.Sku;
        _price = @event.Price;
        _isActive = true;
    }
}
```

## Usage

The `AggregateRoot` class is designed to be subclassed by domain aggregates. Here's a step-by-step guide for implementing aggregates in Reactive Domain:

1. **Create a Class**: Create a new class that inherits from `AggregateRoot`
2. **Define State**: Define private fields to hold the aggregate's state
3. **Register Event Handlers**: In the constructor, register event handlers using the `Register<TEvent>(Apply)` method
4. **Implement Command Methods**: Create public methods that handle commands, validate business rules, and raise events
5. **Implement Event Handlers**: Create private `Apply` methods for each event type to update the aggregate's state
6. **Implement Initialization**: Use an `Initialize` method or command-handling constructor to set up new aggregates

## Example Implementation

```csharp
public class Account : AggregateRoot
{
    // State fields
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    private string _customerName;
    private AccountType _accountType;
    private DateTime _createdAt;
    private DateTime? _closedAt;
    
    // Constructor for new or loaded aggregates
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
        Register<AccountReopened>(Apply);
    }
    
    // Constructor for handling creation commands
    public Account(Guid id, CreateAccount command) : base(id, command)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
        Register<AccountReopened>(Apply);
        
        // Validate command
        if (string.IsNullOrEmpty(command.AccountNumber))
            throw new ArgumentException("Account number is required", nameof(command.AccountNumber));
            
        if (string.IsNullOrEmpty(command.CustomerName))
            throw new ArgumentException("Customer name is required", nameof(command.CustomerName));
        
        // Raise creation event
        RaiseEvent(MessageBuilder.From(command, () => new AccountCreated(
            id,
            command.AccountNumber,
            command.CustomerName,
            command.AccountType,
            DateTime.UtcNow)));
            
        // If initial deposit is provided, perform the deposit
        if (command.InitialDeposit > 0)
        {
            RaiseEvent(MessageBuilder.From(command, () => new FundsDeposited(
                id,
                command.InitialDeposit,
                "Initial deposit",
                DateTime.UtcNow)));
        }
    }
    
    // Command handler for deposit
    public void Deposit(decimal amount, string reference, ICorrelatedMessage source)
    {
        // Enforce business rules
        if (!_isActive)
            throw new InvalidOperationException("Cannot deposit to an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
        
        // Create and apply the event with proper correlation
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(
            Id, 
            amount, 
            reference, 
            DateTime.UtcNow)));
    }
    
    // Command handler for withdrawal
    public void Withdraw(decimal amount, string reference, ICorrelatedMessage source)
    {
        // Enforce business rules
        if (!_isActive)
            throw new InvalidOperationException("Cannot withdraw from an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
        
        // Create and apply the event with proper correlation
        RaiseEvent(MessageBuilder.From(source, () => new FundsWithdrawn(
            Id, 
            amount, 
            reference, 
            DateTime.UtcNow)));
    }
    
    // Command handler for closing the account
    public void Close(string reason, ICorrelatedMessage source)
    {
        // Enforce business rules
        if (!_isActive)
            throw new InvalidOperationException("Account is already closed");
        
        // Create and apply the event with proper correlation
        RaiseEvent(MessageBuilder.From(source, () => new AccountClosed(
            Id, 
            reason, 
            DateTime.UtcNow)));
    }
    
    // Command handler for reopening the account
    public void Reopen(ICorrelatedMessage source)
    {
        // Enforce business rules
        if (_isActive)
            throw new InvalidOperationException("Account is already active");
        
        // Create and apply the event with proper correlation
        RaiseEvent(MessageBuilder.From(source, () => new AccountReopened(
            Id, 
            DateTime.UtcNow)));
    }
    
    // Query methods - expose state in a controlled manner
    public decimal GetBalance() => _balance;
    public bool IsActive() => _isActive;
    public string GetAccountNumber() => _accountNumber;
    public string GetCustomerName() => _customerName;
    public AccountType GetAccountType() => _accountType;
    
    // Event handlers - update state based on events
    private void Apply(AccountCreated @event)
    {
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
        _accountType = @event.AccountType;
        _createdAt = @event.CreatedAt;
        _balance = 0;
        _isActive = true;
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
        _closedAt = @event.ClosedAt;
    }
    
    private void Apply(AccountReopened @event)
    {
        _isActive = true;
        _closedAt = null;
    }
}
```

## Best Practices

1. **Register Event Handlers in Constructor**: Always register event handlers in the constructor to ensure they're available for event replay.

2. **Separate Command and Query Methods**: Follow Command Query Responsibility Segregation (CQRS) by separating methods that modify state from those that read state.

3. **Validate in Command Methods**: Validate all business rules in command methods before raising events. Once an event is raised, it's considered a fact that has occurred.

4. **Use MessageBuilder for Events**: Always use `MessageBuilder.From(source, () => new Event(...))` to create events with proper correlation tracking.

5. **Keep Apply Methods Simple**: Event handlers should only update state and never contain business logic or raise additional events.

6. **Encapsulate State**: Keep all state private and expose it through controlled query methods.

7. **Immutable Events**: Design events to be immutable to ensure the event history remains unchanged.

8. **Explicit Initialization**: Use an `Initialize` method or command-handling constructor for creating new aggregates, rather than raising events directly in the constructor.

9. **Optimistic Concurrency**: Use the `ExpectedVersion` property to prevent lost updates in concurrent scenarios.

10. **Domain-Focused Naming**: Name aggregates, commands, and events using domain language that business stakeholders understand.

## Common Pitfalls

1. **Missing Event Registration**: Forgetting to register event handlers in the constructor will cause events to be ignored during replay.

2. **Direct State Modification**: Modifying aggregate state directly instead of through events breaks the event sourcing pattern.

3. **Losing Correlation**: Not using `MessageBuilder` when creating events results in lost correlation tracking.

4. **Complex Aggregates**: Creating aggregates that are too large or contain too many responsibilities makes them difficult to maintain and can lead to performance issues.

5. **Side Effects in Apply Methods**: Including side effects like I/O operations or external service calls in Apply methods can cause issues during event replay.

6. **Circular Event References**: Calling `RaiseEvent()` from within `Apply()` methods creates an infinite loop.

7. **Ignoring Version Conflicts**: Not handling optimistic concurrency exceptions properly can lead to data inconsistencies.

8. **Exposing Mutable State**: Returning references to internal collections or complex objects allows external code to modify the aggregate's state directly.

9. **Missing Null Checks**: Not validating inputs in command methods can lead to null reference exceptions or invalid state.

10. **Inconsistent Event Naming**: Using inconsistent naming conventions for events makes the event stream harder to understand and analyze.

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
