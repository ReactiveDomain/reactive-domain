# Event

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`Event` is a base class in Reactive Domain that implements the `ICorrelatedMessage` interface and serves as the foundation for all event messages in the system. Events represent immutable facts that have occurred in the domain and are a critical component of event-sourced systems.

## Overview

Events in Reactive Domain represent immutable facts that have occurred in the system. They are the historical record of changes to the domain and form the basis of event sourcing. Events are named in the past tense (e.g., `AccountCreated`, `FundsDeposited`) to emphasize that they represent facts that have already occurred. The `Event` base class provides common functionality for all event implementations, including correlation and causation tracking, which is essential for debugging and auditing in distributed systems.

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
- **Serialization**: Designed to be easily serializable for storage in event stores

## Usage

### Defining an Event

There are two recommended patterns for defining events in Reactive Domain:

#### Pattern 1: Using Factory Methods with MessageBuilder (Recommended)

This pattern uses a private constructor and a static factory method to ensure proper correlation:

```csharp
public class AccountCreated : Event, ICorrelatedMessage
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public string CustomerName { get; }
    
    // Correlation properties (explicitly implementing ICorrelatedMessage)
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method using MessageBuilder
    public static AccountCreated Create(
        Guid accountId, 
        string accountNumber, 
        string customerName,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new AccountCreated(
            accountId, 
            accountNumber, 
            customerName,
            Guid.NewGuid(), 
            source.CorrelationId, 
            source.MsgId));
    }
    
    // Private constructor ensures events are created through the factory method
    private AccountCreated(
        Guid accountId, 
        string accountNumber, 
        string customerName,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

#### Pattern 2: Using Base Class Constructors

This is a simpler pattern that relies on the base class for correlation handling:

```csharp
public class AccountCreated : Event
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public string CustomerName { get; }
    
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

The `MessageBuilder` factory is the recommended way to create events with proper correlation information. There are two main approaches:

#### Approach 1: Using MessageBuilder in Aggregates (Recommended)

When raising events from within an aggregate:

```csharp
// Inside an aggregate method
public void CreateAccount(string accountNumber, string customerName, ICorrelatedMessage source)
{
    // Validate business rules
    if (string.IsNullOrEmpty(accountNumber))
        throw new ArgumentException("Account number is required", nameof(accountNumber));
    
    if (string.IsNullOrEmpty(customerName))
        throw new ArgumentException("Customer name is required", nameof(customerName));
    
    // Raise the event using MessageBuilder
    RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(
        Id, 
        accountNumber, 
        customerName,
        Guid.NewGuid(),
        source.CorrelationId,
        source.MsgId
    )));
}
```

#### Approach 2: Using Factory Methods

When using the factory method pattern:

```csharp
// In a command handler or service
public void HandleCreateAccount(CreateAccount command)
{
    // Create the event using the factory method
    var accountCreatedEvent = AccountCreated.Create(
        Guid.NewGuid(),
        $"ACC-{Guid.NewGuid().ToString().Substring(0, 8)}",
        command.CustomerName,
        command  // Source message for correlation
    );
    
    // Use the event
    _eventStore.Save(accountCreatedEvent);
    _eventBus.Publish(accountCreatedEvent);
}
```

#### Starting a New Correlation Chain

When you need to start a new correlation chain:

```csharp
// Create a new event (starts a new correlation chain)
var newEvent = MessageBuilder.New(() => new AccountCreated(
    Guid.NewGuid(),
    "ACC-456",
    "Jane Smith"
));
```

### Handling Events

Events are typically handled by event handlers which implement the `IEventHandler<T>` interface. There are several common patterns for event handling:

#### Pattern 1: Projection Event Handlers

These handlers update read models or projections:

```csharp
public class AccountSummaryProjection : IEventHandler<AccountCreated>, IEventHandler<FundsDeposited>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountSummaryProjection(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(@event.AccountNumber, @event.CustomerName, 0);
        _repository.Save(accountSummary);
    }
    
    public void Handle(FundsDeposited @event)
    {
        var accountSummary = _repository.GetById(@event.AccountId);
        if (accountSummary != null)
        {
            accountSummary.UpdateBalance(accountSummary.Balance + @event.Amount);
            _repository.Save(accountSummary);
        }
    }
}
```

#### Pattern 2: Integration Event Handlers

These handlers integrate with external systems:

```csharp
public class AccountCreatedNotificationHandler : IEventHandler<AccountCreated>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<AccountCreatedNotificationHandler> _logger;
    
    public AccountCreatedNotificationHandler(
        INotificationService notificationService,
        ILogger<AccountCreatedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }
    
    public void Handle(AccountCreated @event)
    {
        try
        {
            _notificationService.SendWelcomeNotification(
                @event.AccountId,
                @event.CustomerName,
                @event.AccountNumber);
                
            _logger.LogInformation(
                "Welcome notification sent for account {AccountId}", 
                @event.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send welcome notification for account {AccountId}",
                @event.AccountId);
                
            // Consider retry or compensation strategies
        }
    }
}
```

#### Pattern 3: Process Manager Event Handlers

These handlers coordinate complex workflows across multiple aggregates:

```csharp
public class AccountOpeningProcessManager : 
    IEventHandler<AccountCreated>,
    IEventHandler<WelcomePackageSent>
{
    private readonly ICommandBus _commandBus;
    private readonly IProcessManagerRepository<AccountOpeningProcess> _repository;
    
    public AccountOpeningProcessManager(
        ICommandBus commandBus,
        IProcessManagerRepository<AccountOpeningProcess> repository)
    {
        _commandBus = commandBus;
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        // Create or update process state
        var process = new AccountOpeningProcess(@event.AccountId);
        process.AccountCreated();
        _repository.Save(process);
        
        // Send next command in the process
        _commandBus.Send(SendWelcomePackage.Create(
            @event.AccountId,
            @event.CustomerName,
            @event.AccountNumber,
            @event)); // Pass event for correlation
    }
    
    public void Handle(WelcomePackageSent @event)
    {
        // Update process state
        var process = _repository.GetById(@event.AccountId);
        process.WelcomePackageSent();
        _repository.Save(process);
        
        // Continue the process if needed
        if (process.IsReadyForActivation)
        {
            _commandBus.Send(ActivateAccount.Create(
                @event.AccountId,
                @event)); // Pass event for correlation
        }
    }
}
```

## Integration with Aggregates

Events are produced by aggregates in response to commands using the `RaiseEvent()` method. This is a core pattern in Domain-Driven Design and CQRS. When an aggregate raises an event:

1. The event is applied to update the aggregate's state via the appropriate `Apply()` method
2. The event is recorded for persistence in the event store
3. Once persisted, the event can be published to event handlers and projections

### Recommended Aggregate Pattern

```csharp
public class Account : AggregateRoot
{
    private string _accountNumber;
    private string _customerName;
    private decimal _balance;
    private bool _isActive;
    
    // Constructor for creating a new aggregate
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Constructor for creating a new account with correlation
    public Account(Guid id, ICorrelatedMessage source) : base(id, source)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
        
        // Initialize the aggregate by raising an event
        RaiseEvent(AccountCreated.Create(
            id, 
            $"ACC-{id.ToString().Substring(0, 8)}", 
            "New Customer",
            source
        ));
    }
    
    // Command handler for deposit
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        // Validate business rules
        if (!_isActive)
            throw new InvalidOperationException("Cannot deposit to an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Raise the event
        RaiseEvent(FundsDeposited.Create(Id, amount, source));
    }
    
    // Command handler for withdrawal
    public void Withdraw(decimal amount, ICorrelatedMessage source)
    {
        // Validate business rules
        if (!_isActive)
            throw new InvalidOperationException("Cannot withdraw from an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InsufficientFundsException($"Insufficient funds. Available: {_balance}, Requested: {amount}");
            
        // Raise the event
        RaiseEvent(FundsWithdrawn.Create(Id, amount, source));
    }
    
    // Command handler for closing account
    public void Close(ICorrelatedMessage source)
    {
        // Validate business rules
        if (!_isActive)
            throw new InvalidOperationException("Account is already closed");
            
        // Raise the event
        RaiseEvent(AccountClosed.Create(Id, source));
    }
    
    // Event handlers
    private void Apply(AccountCreated @event)
    {
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
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
    }
    
    // Custom exception
    public class InsufficientFundsException : Exception
    {
        public InsufficientFundsException(string message) : base(message) { }
    }
}
```

## Event Sourcing

Events are the foundation of event sourcing, where the state of an aggregate is reconstructed by replaying events. This process, often called rehydration, applies each event in sequence to rebuild the aggregate's state.

### Event Registration and Dispatch

In Reactive Domain, the `EventDrivenStateMachine` base class (which `AggregateRoot` inherits from) handles event registration and dispatch:

```csharp
// In the aggregate constructor
public Account(Guid id) : base(id)
{
    // Register event handlers
    Register<AccountCreated>(Apply);
    Register<FundsDeposited>(Apply);
    Register<FundsWithdrawn>(Apply);
}
```

This registration approach is more efficient than using reflection at runtime, as it builds a dispatch dictionary during initialization.

### Loading from History

When loading an aggregate from the event store, the repository typically calls:

```csharp
// Inside the repository implementation
public T GetById<T>(Guid id) where T : AggregateRoot, new()
{
    // Create a new instance of the aggregate
    var aggregate = new T();
    
    // Load events from the event store
    var events = _eventStore.GetEvents(GetStreamName(typeof(T), id));
    
    // Restore the aggregate state by replaying events
    aggregate.RestoreFromEvents(events);
    
    return aggregate;
}
```

The `RestoreFromEvents` method in the `EventDrivenStateMachine` base class handles applying each event to rebuild the aggregate state:

```csharp
// In EventDrivenStateMachine
public void RestoreFromEvents(IEnumerable<IEvent> events)
{
    foreach (var @event in events)
    {
        // Apply the event to update state
        ApplyEvent(@event);
        
        // Update the version
        Version++;
    }
}

private void ApplyEvent(IEvent @event)
{
    // Look up the handler in the registration dictionary
    if (_eventHandlers.TryGetValue(@event.GetType(), out var handler))
    {
        // Invoke the handler
        handler.Invoke(this, new object[] { @event });
    }
    else
    {
        throw new InvalidOperationException($"No handler registered for event type {@event.GetType().Name}");
    }
}
```

### Event Versioning and Evolution

As your system evolves, you may need to handle different versions of events. Reactive Domain supports this through explicit event versioning:

```csharp
// Original event
public class FundsDeposited : Event { /* ... */ }

// New version with additional fields
public class FundsDepositedV2 : Event
{
    public string Currency { get; }
    // Other properties...
    
    // Factory method
    public static FundsDepositedV2 Create(/* ... */) { /* ... */ }
}

// In the aggregate
public Account(Guid id) : base(id)
{
    // Register handlers for both versions
    Register<FundsDeposited>(ApplyV1);
    Register<FundsDepositedV2>(ApplyV2);
}

private void ApplyV1(FundsDeposited @event)
{
    // Handle original version
    _balance += @event.Amount;
}

private void ApplyV2(FundsDepositedV2 @event)
{
    // Handle new version with currency
    if (@event.Currency == "USD")
    {
        _balance += @event.Amount;
    }
    else
    {
        // Convert currency if needed
        _balance += _currencyConverter.Convert(@event.Amount, @event.Currency, "USD");
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
8. **Use RaiseEvent()**: Always use the `RaiseEvent()` method in aggregates to create new events, not direct calls to `Apply()`
9. **Idempotent Event Handlers**: Ensure `Apply()` methods are idempotent as they may be called multiple times during event replay

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
- [AggregateRoot](./aggregate-root.md): Domain entities that raise events in response to commands
- [Command](./command.md): Messages that trigger state changes resulting in events
- [ReadModelBase](./read-model-base.md): Read models that are updated in response to events

For a comprehensive view of how events interact with other components, see the [Key Component Relationships](../../architecture.md#key-component-relationships) section in the Architecture Guide, particularly the [Command and Event Relationship](../../architecture.md#command-and-event-relationship) and [ReadModelBase and Event Handlers](../../architecture.md#readmodelbase-and-event-handlers) diagrams.

---

**Navigation**:
- [← Previous: Command](./command.md)
- [↑ Back to Top](#event)
- [→ Next: ICorrelatedMessage](./icorrelated-message.md)
