# Saga/Process Manager Implementation Patterns

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

This document outlines the key patterns and best practices for implementing sagas (also known as process managers) in Reactive Domain applications. Sagas coordinate complex business processes that span multiple aggregates or bounded contexts, ensuring that the overall process completes correctly despite being distributed across different components.

## Table of Contents

1. [Saga Concepts and Principles](#saga-concepts-and-principles)
2. [Types of Sagas](#types-of-sagas)
3. [State Management](#state-management)
4. [Event Handling Patterns](#event-handling-patterns)
5. [Persistence Mechanisms](#persistence-mechanisms)
6. [Correlation and Tracking](#correlation-and-tracking)
7. [Error Handling and Recovery](#error-handling-and-recovery)
8. [Timeout Management](#timeout-management)
9. [Compensating Actions](#compensating-actions)
10. [Testing Sagas](#testing-sagas)
11. [Best Practices](#best-practices)
12. [Common Pitfalls](#common-pitfalls)

## Saga Concepts and Principles

A saga is a long-lived transaction that can be written as a sequence of transactions that can be interleaved with other transactions. In event-driven architectures, sagas solve the problem of maintaining process integrity across aggregate boundaries.

### Key Characteristics

1. **Coordination**: Sagas coordinate activities across multiple aggregates or bounded contexts
2. **Reactivity**: They react to events and issue commands to drive the process forward
3. **Stateful**: They maintain state to track the progress of the business process
4. **Long-running**: They can span extended periods, from seconds to days or longer
5. **Eventual Consistency**: They ensure eventual consistency across multiple aggregates

### Saga vs. Process Manager

While the terms "saga" and "process manager" are often used interchangeably, there are subtle differences:

- **Saga**: Originally described as a sequence of local transactions where each transaction updates data within a single service, with compensating transactions to undo changes if a step fails
- **Process Manager**: A more general term for a component that coordinates multiple aggregates, reacting to events and issuing commands

In Reactive Domain, the `ProcessManager` base class provides the foundation for implementing both concepts.

## Types of Sagas

Sagas can be categorized based on their implementation approach and behavior patterns.

### 1. Choreography-based Sagas

In a choreography-based saga, there is no central coordinator. Instead, each participant in the process knows what to do based on events published by other participants.

```csharp
// Event handler in the Order service
public class OrderEventHandler : 
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentCompleted>,
    IEventHandler<PaymentFailed>
{
    private readonly ICommandBus _commandBus;
    
    public OrderEventHandler(ICommandBus commandBus)
    {
        _commandBus = commandBus;
    }
    
    public void Handle(OrderPlaced @event)
    {
        // When an order is placed, request payment
        _commandBus.Send(new ProcessPayment(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount));
    }
    
    public void Handle(PaymentCompleted @event)
    {
        // When payment is completed, ship the order
        _commandBus.Send(new ShipOrder(@event.OrderId));
    }
    
    public void Handle(PaymentFailed @event)
    {
        // When payment fails, cancel the order
        _commandBus.Send(new CancelOrder(
            @event.OrderId,
            "Payment failed"));
    }
}

// Event handler in the Payment service
public class PaymentEventHandler : IEventHandler<PaymentRequested>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEventBus _eventBus;
    
    public PaymentEventHandler(
        IPaymentGateway paymentGateway,
        IEventBus eventBus)
    {
        _paymentGateway = paymentGateway;
        _eventBus = eventBus;
    }
    
    public void Handle(PaymentRequested @event)
    {
        try
        {
            var result = _paymentGateway.ProcessPayment(
                @event.CustomerId,
                @event.Amount);
                
            if (result.Success)
            {
                _eventBus.Publish(new PaymentCompleted(
                    @event.OrderId,
                    @event.Amount,
                    result.TransactionId));
            }
            else
            {
                _eventBus.Publish(new PaymentFailed(
                    @event.OrderId,
                    result.FailureReason));
            }
        }
        catch (Exception ex)
        {
            _eventBus.Publish(new PaymentFailed(
                @event.OrderId,
                ex.Message));
        }
    }
}
```

**Advantages**:
- Simpler to implement initially
- No need for a central coordinator
- Naturally distributed

**Disadvantages**:
- Process flow is implicit and distributed across services
- Harder to track the overall process state
- More difficult to implement complex flows and compensating actions

### 2. Orchestration-based Sagas

In an orchestration-based saga, a central coordinator (the saga/process manager) manages the entire process flow, reacting to events and issuing commands.

```csharp
public class OrderProcessManager : ProcessManager,
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentCompleted>,
    IEventHandler<PaymentFailed>,
    IEventHandler<OrderShipped>,
    IEventHandler<ShippingFailed>
{
    private readonly ICommandBus _commandBus;
    
    // Process state
    private bool _orderPlaced;
    private bool _paymentCompleted;
    private bool _orderShipped;
    private Guid _orderId;
    private Guid _customerId;
    private decimal _orderAmount;
    
    public OrderProcessManager(Guid processId, ICommandBus commandBus) 
        : base(processId)
    {
        _commandBus = commandBus;
    }
    
    public void Handle(OrderPlaced @event)
    {
        if (_orderPlaced) return; // Idempotency check
        
        // Update process state
        RaiseEvent(new OrderProcessStarted(
            Id,
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount));
        
        // Request payment
        _commandBus.Send(MessageBuilder.From(@event, () => new ProcessPayment(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount)));
    }
    
    public void Handle(PaymentCompleted @event)
    {
        if (_paymentCompleted || !_orderPlaced) return; // Idempotency and sequence check
        
        // Update process state
        RaiseEvent(new PaymentCompletedForOrder(
            Id,
            @event.OrderId));
        
        // Ship the order
        _commandBus.Send(MessageBuilder.From(@event, () => new ShipOrder(@event.OrderId)));
    }
    
    public void Handle(PaymentFailed @event)
    {
        if (!_orderPlaced) return; // Sequence check
        
        // Update process state
        RaiseEvent(new OrderProcessFailed(
            Id,
            @event.OrderId,
            "Payment failed: " + @event.FailureReason));
        
        // Cancel the order
        _commandBus.Send(MessageBuilder.From(@event, () => new CancelOrder(
            @event.OrderId,
            "Payment failed: " + @event.FailureReason)));
    }
    
    public void Handle(OrderShipped @event)
    {
        if (_orderShipped || !_paymentCompleted) return; // Idempotency and sequence check
        
        // Update process state
        RaiseEvent(new OrderProcessCompleted(
            Id,
            @event.OrderId));
    }
    
    public void Handle(ShippingFailed @event)
    {
        if (!_paymentCompleted) return; // Sequence check
        
        // Update process state
        RaiseEvent(new OrderProcessFailed(
            Id,
            @event.OrderId,
            "Shipping failed: " + @event.FailureReason));
        
        // Refund the payment
        _commandBus.Send(MessageBuilder.From(@event, () => new RefundPayment(
            @event.OrderId,
            _customerId,
            _orderAmount,
            "Shipping failed: " + @event.FailureReason)));
    }
    
    // Event handlers for the process manager's own events
    private void Apply(OrderProcessStarted @event)
    {
        _orderPlaced = true;
        _orderId = @event.OrderId;
        _customerId = @event.CustomerId;
        _orderAmount = @event.OrderAmount;
    }
    
    private void Apply(PaymentCompletedForOrder @event)
    {
        _paymentCompleted = true;
    }
    
    private void Apply(OrderProcessCompleted @event)
    {
        _orderShipped = true;
    }
}
```

**Advantages**:
- Explicit process flow centralized in one component
- Easier to track and monitor the process state
- Better suited for complex flows with many steps
- Simpler to implement compensating actions

**Disadvantages**:
- Introduces a central component that could become a bottleneck
- More complex initial implementation
- Can introduce coupling between services

### 3. Event-Sourced Sagas

Event-sourced sagas store their state as a sequence of events, allowing for complete reconstruction of the saga's state.

```csharp
public class EventSourcedOrderProcessManager : ProcessManager,
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentCompleted>,
    IEventHandler<PaymentFailed>
{
    private readonly ICommandBus _commandBus;
    private readonly ISagaRepository _repository;
    
    // Process state
    private OrderProcessState _state;
    
    public EventSourcedOrderProcessManager(
        Guid processId, 
        ICommandBus commandBus,
        ISagaRepository repository) 
        : base(processId)
    {
        _commandBus = commandBus;
        _repository = repository;
        _state = new OrderProcessState();
    }
    
    public async Task Handle(OrderPlaced @event)
    {
        if (_state.OrderPlaced) return; // Idempotency check
        
        // Update process state
        RaiseEvent(new OrderProcessStarted(
            Id,
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount));
        
        // Save the updated state
        await _repository.SaveAsync(this);
        
        // Request payment
        _commandBus.Send(MessageBuilder.From(@event, () => new ProcessPayment(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount)));
    }
    
    // Additional event handlers...
    
    // Event handlers for the process manager's own events
    private void Apply(OrderProcessStarted @event)
    {
        _state.OrderPlaced = true;
        _state.OrderId = @event.OrderId;
        _state.CustomerId = @event.CustomerId;
        _state.OrderAmount = @event.OrderAmount;
    }
    
    // Additional apply methods...
    
    // State class to encapsulate the process state
    private class OrderProcessState
    {
        public bool OrderPlaced { get; set; }
        public bool PaymentCompleted { get; set; }
        public bool OrderShipped { get; set; }
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal OrderAmount { get; set; }
    }
}
```

**Advantages**:
- Complete audit trail of the saga's execution
- Ability to reconstruct the saga's state at any point in time
- Natural fit with event-sourced aggregates

**Disadvantages**:
- More complex implementation
- Potential performance overhead for long-running sagas with many events

## State Management

Effective state management is crucial for sagas to track their progress and make decisions based on the current state of the process.

### State Representation Approaches

#### 1. Boolean Flags

The simplest approach is to use boolean flags to track the completion of each step in the process.

```csharp
public class OrderProcessManager : ProcessManager
{
    // State represented as boolean flags
    private bool _orderPlaced;
    private bool _paymentProcessed;
    private bool _inventoryReserved;
    private bool _orderShipped;
    
    // Additional state data
    private Guid _orderId;
    private Guid _customerId;
    private decimal _orderAmount;
    
    // Event handlers and business logic...
}
```

**Advantages**:
- Simple and easy to understand
- Minimal overhead

**Disadvantages**:
- Limited expressiveness for complex state transitions
- Can become unwieldy for processes with many steps
- Difficult to visualize the overall process state

#### 2. Enum-based State

Using enums to represent the current state of the process provides a more explicit representation of the process flow.

```csharp
public class OrderProcessManager : ProcessManager
{
    // State represented as an enum
    private enum OrderProcessState
    {
        New,
        OrderPlaced,
        PaymentProcessing,
        PaymentCompleted,
        PaymentFailed,
        InventoryReserving,
        InventoryReserved,
        InventoryReservationFailed,
        Shipping,
        Shipped,
        ShippingFailed,
        Completed,
        Failed,
        Cancelled
    }
    
    private OrderProcessState _currentState;
    
    // Additional state data
    private Guid _orderId;
    private Guid _customerId;
    private decimal _orderAmount;
    private string _failureReason;
    
    public void Handle(OrderPlaced @event)
    {
        if (_currentState != OrderProcessState.New) return;
        
        // Update state
        _currentState = OrderProcessState.OrderPlaced;
        _orderId = @event.OrderId;
        _customerId = @event.CustomerId;
        _orderAmount = @event.TotalAmount;
        
        // Issue commands...
    }
    
    public void Handle(PaymentCompleted @event)
    {
        if (_currentState != OrderProcessState.PaymentProcessing) return;
        
        // Update state
        _currentState = OrderProcessState.PaymentCompleted;
        
        // Issue commands...
    }
    
    // Additional event handlers...
}
```

**Advantages**:
- More explicit representation of the process state
- Easier to visualize and understand the process flow
- Better for processes with well-defined state transitions

**Disadvantages**:
- Less flexible for processes with parallel steps
- Can require frequent enum updates as the process evolves

#### 3. State Object Pattern

Encapsulating the state in a dedicated object provides a more structured approach to state management.

```csharp
public class OrderProcessManager : ProcessManager
{
    // State represented as a dedicated object
    private class OrderProcessState
    {
        public bool OrderPlaced { get; set; }
        public bool PaymentProcessed { get; set; }
        public bool InventoryReserved { get; set; }
        public bool OrderShipped { get; set; }
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal OrderAmount { get; set; }
        public string FailureReason { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
    }
    
    private OrderProcessState _state;
    
    public OrderProcessManager(Guid id) : base(id)
    {
        _state = new OrderProcessState
        {
            StartTime = DateTime.UtcNow
        };
    }
    
    // Event handlers that update the state object...
}
```

**Advantages**:
- Better encapsulation of state
- Easier to persist and reconstruct
- More maintainable for complex processes

**Disadvantages**:
- Slightly more complex implementation
- Can lead to anemic state objects without proper design

### State Persistence

Sagas need to persist their state to survive process restarts and failures. There are several approaches to state persistence:

#### 1. Event Sourcing

With event sourcing, the saga's state is stored as a sequence of events that can be replayed to reconstruct the state.

```csharp
public class EventSourcedOrderProcessManager : ProcessManager
{
    // State is reconstructed from events
    private OrderProcessState _state = new OrderProcessState();
    
    // IEventSource implementation
    public override void RestoreFromEvents(IEnumerable<object> events)
    {
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    // Event handlers for the saga's own events
    private void Apply(OrderProcessStarted @event)
    {
        _state.OrderPlaced = true;
        _state.OrderId = @event.OrderId;
        _state.CustomerId = @event.CustomerId;
        _state.OrderAmount = @event.OrderAmount;
    }
    
    private void Apply(PaymentCompletedForOrder @event)
    {
        _state.PaymentProcessed = true;
    }
    
    // Additional apply methods...
}
```

#### 2. Snapshot-based Persistence

For long-running sagas with many events, snapshot-based persistence can improve performance.

```csharp
public class SnapshotOrderProcessManager : ProcessManager, ISnapshotSource
{
    private OrderProcessState _state = new OrderProcessState();
    
    public long SnapshotVersion { get; set; }
    
    public object CreateSnapshot()
    {
        return _state.Clone(); // Deep copy of the state
    }
    
    public void RestoreFromSnapshot(object snapshot)
    {
        if (snapshot is OrderProcessState state)
        {
            _state = state;
        }
    }
    
    // Additional implementation...
}
```

#### 3. Direct State Persistence

For simpler scenarios, the saga's state can be directly persisted to a data store.

```csharp
public class OrderProcessManager : ProcessManager
{
    private OrderProcessState _state;
    private readonly ISagaStateRepository _stateRepository;
    
    public async Task Persist()
    {
        await _stateRepository.SaveStateAsync(Id, _state);
    }
    
    public static async Task<OrderProcessManager> LoadAsync(
        Guid id, 
        ISagaStateRepository stateRepository,
        ICommandBus commandBus)
    {
        var state = await stateRepository.GetStateAsync<OrderProcessState>(id);
        return new OrderProcessManager(id, state, commandBus, stateRepository);
    }
    
    // Additional implementation...
}

public interface ISagaStateRepository
{
    Task SaveStateAsync<T>(Guid sagaId, T state);
    Task<T> GetStateAsync<T>(Guid sagaId) where T : class, new();
}
```

## Event Handling Patterns

Sagas need to handle events efficiently and reliably to drive the business process forward. Here are key patterns for event handling in sagas:

### 1. Saga Router Pattern

The Saga Router pattern routes events to the appropriate saga instance based on correlation information in the event.

```csharp
public class ProcessManagerRouter<TSaga, TEvent> : IEventHandler<TEvent>
    where TSaga : ProcessManager
    where TEvent : class, IEvent
{
    private readonly ISagaRepository<TSaga> _repository;
    private readonly Func<Guid, TSaga> _factory;
    private readonly Func<TEvent, Guid> _correlationIdSelector;
    private readonly Action<TSaga, TEvent> _handler;
    
    public ProcessManagerRouter(
        ISagaRepository<TSaga> repository,
        Func<Guid, TSaga> factory,
        Func<TEvent, Guid> correlationIdSelector,
        Action<TSaga, TEvent> handler)
    {
        _repository = repository;
        _factory = factory;
        _correlationIdSelector = correlationIdSelector;
        _handler = handler;
    }
    
    public async Task Handle(TEvent @event)
    {
        // Extract correlation ID from the event
        var correlationId = _correlationIdSelector(@event);
        
        // Try to load existing saga instance
        var saga = await _repository.GetByIdAsync(correlationId);
        
        // If not found, create a new instance
        if (saga == null)
        {
            saga = _factory(correlationId);
        }
        
        // Handle the event
        _handler(saga, @event);
        
        // Save the updated saga
        await _repository.SaveAsync(saga);
    }
}

// Usage in configuration
public void ConfigureProcessManagers(
    IEventBus eventBus, 
    ICommandBus commandBus,
    ISagaRepository<OrderProcessManager> repository)
{
    // Create a factory for the process manager
    Func<Guid, OrderProcessManager> factory = 
        id => new OrderProcessManager(id, commandBus);
    
    // Register event handlers that will route events to the appropriate process manager instance
    eventBus.Subscribe<OrderPlaced>(new ProcessManagerRouter<OrderProcessManager, OrderPlaced>(
        repository,
        factory,
        e => e.OrderId, // Use OrderId to find or create process manager instances
        (pm, e) => pm.Handle(e)
    ));
    
    // Register other event handlers similarly
}
```

### 2. Idempotent Event Handling

Idempotent event handling ensures that the same event can be processed multiple times without causing duplicate effects.

```csharp
public class OrderProcessManager : ProcessManager,
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentCompleted>
{
    // State tracking for idempotency
    private HashSet<Guid> _processedEventIds = new HashSet<Guid>();
    
    public void Handle(OrderPlaced @event)
    {
        // Check if we've already processed this event
        if (_processedEventIds.Contains(@event.MessageId))
            return;
        
        // Process the event
        // ...
        
        // Mark the event as processed
        _processedEventIds.Add(@event.MessageId);
    }
    
    // Alternative approach using state flags
    public void Handle(PaymentCompleted @event)
    {
        // Check if we've already processed this event based on state
        if (_paymentProcessed)
            return;
        
        // Process the event
        // ...
        
        // Update state to indicate this step is complete
        _paymentProcessed = true;
    }
}
```

### 3. Event Correlation

Event correlation ensures that events are routed to the correct saga instance.

```csharp
public interface ICorrelatedEvent
{
    Guid CorrelationId { get; }
}

public class OrderPlaced : Event, ICorrelatedEvent
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    
    // Implement correlation based on OrderId
    public Guid CorrelationId => OrderId;
    
    public OrderPlaced(Guid orderId, Guid customerId, decimal totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}

// Saga repository that uses correlation
public class SagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : ProcessManager
{
    private readonly IEventStore _eventStore;
    
    public SagaRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }
    
    public async Task<TSaga> GetByCorrelationIdAsync(Guid correlationId)
    {
        // Load events for the saga with the given correlation ID
        var events = await _eventStore.GetEventsAsync(correlationId);
        
        if (!events.Any())
            return null;
            
        // Create and restore saga instance
        var saga = Activator.CreateInstance(typeof(TSaga), correlationId) as TSaga;
        saga.RestoreFromEvents(events.Select(e => e.Data));
        
        return saga;
    }
    
    // Additional implementation...
}
```

### 4. Event Filtering

Event filtering ensures that sagas only process events that are relevant to their current state.

```csharp
public class OrderProcessManager : ProcessManager,
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentCompleted>,
    IEventHandler<PaymentFailed>
{
    private enum ProcessState
    {
        New,
        OrderPlaced,
        PaymentProcessing,
        PaymentCompleted,
        PaymentFailed,
        Completed,
        Failed
    }
    
    private ProcessState _currentState = ProcessState.New;
    
    public void Handle(OrderPlaced @event)
    {
        // Only process if in the correct state
        if (_currentState != ProcessState.New)
            return;
        
        // Process the event
        // ...
        
        _currentState = ProcessState.OrderPlaced;
    }
    
    public void Handle(PaymentCompleted @event)
    {
        // Only process if in the correct state
        if (_currentState != ProcessState.PaymentProcessing)
            return;
        
        // Process the event
        // ...
        
        _currentState = ProcessState.PaymentCompleted;
    }
    
    public void Handle(PaymentFailed @event)
    {
        // Only process if in the correct state
        if (_currentState != ProcessState.PaymentProcessing)
            return;
        
        // Process the event
        // ...
        
        _currentState = ProcessState.PaymentFailed;
    }
}
```

### 5. Event Versioning

Event versioning ensures that sagas can handle different versions of events as the system evolves.

```csharp
// Version 1 of the event
public class OrderPlacedV1 : Event
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    
    public OrderPlacedV1(Guid orderId, Guid customerId, decimal totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}

// Version 2 of the event with additional fields
public class OrderPlacedV2 : Event
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    public List<OrderItem> Items { get; }
    public string ShippingAddress { get; }
    
    public OrderPlacedV2(
        Guid orderId, 
        Guid customerId, 
        decimal totalAmount,
        List<OrderItem> items,
        string shippingAddress)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Items = items;
        ShippingAddress = shippingAddress;
    }
}

// Saga that handles both versions
public class OrderProcessManager : ProcessManager,
    IEventHandler<OrderPlacedV1>,
    IEventHandler<OrderPlacedV2>
{
    public void Handle(OrderPlacedV1 @event)
    {
        // Handle version 1 of the event
        ProcessOrderPlaced(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount,
            null,
            null);
    }
    
    public void Handle(OrderPlacedV2 @event)
    {
        // Handle version 2 of the event
        ProcessOrderPlaced(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount,
            @event.Items,
            @event.ShippingAddress);
    }
    
    private void ProcessOrderPlaced(
        Guid orderId,
        Guid customerId,
        decimal totalAmount,
        List<OrderItem> items,
        string shippingAddress)
    {
        // Common processing logic
        // ...
    }
}
```

## Persistence Mechanisms

Persistence is crucial for sagas to maintain their state across process restarts and failures. Here are key patterns for persisting sagas:

### 1. Event Sourcing-based Persistence

Event sourcing is a natural fit for saga persistence, as it aligns with the event-driven nature of sagas.

```csharp
public class EventSourcedSagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : ProcessManager
{
    private readonly IEventStore _eventStore;
    private readonly Func<Guid, TSaga> _factory;
    
    public EventSourcedSagaRepository(IEventStore eventStore, Func<Guid, TSaga> factory)
    {
        _eventStore = eventStore;
        _factory = factory;
    }
    
    public async Task<TSaga> GetByIdAsync(Guid sagaId)
    {
        // Load all events for the saga
        var events = await _eventStore.GetEventsAsync(sagaId);
        
        if (!events.Any())
            return null;
            
        // Create a new saga instance
        var saga = _factory(sagaId);
        
        // Restore the saga state from events
        saga.RestoreFromEvents(events.Select(e => e.Data));
        
        return saga;
    }
    
    public async Task SaveAsync(TSaga saga)
    {
        // Get uncommitted events from the saga
        var uncommittedEvents = saga.TakeEvents();
        
        if (uncommittedEvents.Length > 0)
        {
            // Save the events to the event store
            await _eventStore.SaveEventsAsync(
                saga.Id,
                uncommittedEvents,
                saga.ExpectedVersion);
        }
    }
}
```

### 2. Snapshot-based Persistence

For long-running sagas with many events, snapshot-based persistence can improve performance.

```csharp
public class SnapshotSagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : ProcessManager, ISnapshotSource
{
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly Func<Guid, TSaga> _factory;
    
    public SnapshotSagaRepository(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        Func<Guid, TSaga> factory)
    {
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
        _factory = factory;
    }
    
    public async Task<TSaga> GetByIdAsync(Guid sagaId)
    {
        // Try to get the latest snapshot
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync(sagaId);
        
        // Create a new saga instance
        var saga = _factory(sagaId);
        
        if (snapshot != null)
        {
            // Restore from snapshot
            saga.RestoreFromSnapshot(snapshot.State);
            saga.SnapshotVersion = snapshot.Version;
            
            // Load events after the snapshot
            var events = await _eventStore.GetEventsAfterVersionAsync(sagaId, snapshot.Version);
            
            if (events.Any())
            {
                // Apply events after the snapshot
                saga.RestoreFromEvents(events.Select(e => e.Data));
            }
        }
        else
        {
            // No snapshot, load all events
            var events = await _eventStore.GetEventsAsync(sagaId);
            
            if (!events.Any())
                return null;
                
            // Restore from events
            saga.RestoreFromEvents(events.Select(e => e.Data));
        }
        
        return saga;
    }
    
    public async Task SaveAsync(TSaga saga)
    {
        // Get uncommitted events from the saga
        var uncommittedEvents = saga.TakeEvents();
        
        if (uncommittedEvents.Length > 0)
        {
            // Save the events to the event store
            await _eventStore.SaveEventsAsync(
                saga.Id,
                uncommittedEvents,
                saga.ExpectedVersion);
            
            // Check if we need to create a snapshot
            if (ShouldCreateSnapshot(saga))
            {
                var snapshot = saga.CreateSnapshot();
                await _snapshotStore.SaveSnapshotAsync(
                    saga.Id,
                    snapshot,
                    saga.ExpectedVersion);
                saga.SnapshotVersion = saga.ExpectedVersion;
            }
        }
    }
    
    private bool ShouldCreateSnapshot(TSaga saga)
    {
        // Create a snapshot every 100 events
        return (saga.ExpectedVersion - saga.SnapshotVersion) >= 100;
    }
}
```

### 3. Document Database Persistence

Document databases are well-suited for storing saga state directly.

```csharp
public class DocumentDbSagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : ProcessManager
{
    private readonly IMongoCollection<SagaDocument<TSaga>> _collection;
    private readonly Func<Guid, TSaga> _factory;
    
    public DocumentDbSagaRepository(
        IMongoDatabase database,
        Func<Guid, TSaga> factory)
    {
        _collection = database.GetCollection<SagaDocument<TSaga>>("Sagas");
        _factory = factory;
        
        // Create indexes
        var indexKeysDefinition = Builders<SagaDocument<TSaga>>.IndexKeys.Ascending(x => x.Id);
        _collection.Indexes.CreateOne(new CreateIndexModel<SagaDocument<TSaga>>(indexKeysDefinition));
    }
    
    public async Task<TSaga> GetByIdAsync(Guid sagaId)
    {
        var filter = Builders<SagaDocument<TSaga>>.Filter.Eq(x => x.Id, sagaId);
        var document = await _collection.Find(filter).FirstOrDefaultAsync();
        
        if (document == null)
            return null;
            
        return document.State;
    }
    
    public async Task SaveAsync(TSaga saga)
    {
        var filter = Builders<SagaDocument<TSaga>>.Filter.Eq(x => x.Id, saga.Id);
        var document = new SagaDocument<TSaga>
        {
            Id = saga.Id,
            State = saga,
            Version = saga.ExpectedVersion,
            LastUpdated = DateTime.UtcNow
        };
        
        await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true });
    }
    
    private class SagaDocument<T>
    {
        public Guid Id { get; set; }
        public T State { get; set; }
        public long Version { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
```

### 4. Relational Database Persistence

Relational databases can also be used for saga persistence, especially for simpler sagas.

```csharp
public class SqlSagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : ProcessManager
{
    private readonly string _connectionString;
    private readonly Func<Guid, TSaga> _factory;
    private readonly JsonSerializerSettings _serializerSettings;
    
    public SqlSagaRepository(
        string connectionString,
        Func<Guid, TSaga> factory)
    {
        _connectionString = connectionString;
        _factory = factory;
        _serializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };
    }
    
    public async Task<TSaga> GetByIdAsync(Guid sagaId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            var sql = "SELECT Data, Version FROM Sagas WHERE Id = @Id AND Type = @Type";
            
            var parameters = new DynamicParameters();
            parameters.Add("@Id", sagaId, DbType.Guid);
            parameters.Add("@Type", typeof(TSaga).FullName, DbType.String);
            
            var result = await connection.QueryFirstOrDefaultAsync(sql, parameters);
            
            if (result == null)
                return null;
                
            var sagaData = result.Data.ToString();
            var version = (long)result.Version;
            
            var saga = JsonConvert.DeserializeObject<TSaga>(sagaData, _serializerSettings);
            saga.ExpectedVersion = version;
            
            return saga;
        }
    }
    
    public async Task SaveAsync(TSaga saga)
    {
        var sagaData = JsonConvert.SerializeObject(saga, _serializerSettings);
        
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Check optimistic concurrency
                    var currentVersion = await GetCurrentVersionAsync(connection, transaction, saga.Id);
                    
                    if (currentVersion.HasValue && currentVersion.Value != saga.ExpectedVersion)
                    {
                        throw new ConcurrencyException(
                            $"Expected version {saga.ExpectedVersion} but got {currentVersion.Value}");
                    }
                    
                    // Insert or update the saga
                    var sql = currentVersion.HasValue
                        ? "UPDATE Sagas SET Data = @Data, Version = @NewVersion, LastUpdated = @LastUpdated WHERE Id = @Id AND Type = @Type"
                        : "INSERT INTO Sagas (Id, Type, Data, Version, LastUpdated) VALUES (@Id, @Type, @Data, @NewVersion, @LastUpdated)";
                    
                    var parameters = new DynamicParameters();
                    parameters.Add("@Id", saga.Id, DbType.Guid);
                    parameters.Add("@Type", typeof(TSaga).FullName, DbType.String);
                    parameters.Add("@Data", sagaData, DbType.String);
                    parameters.Add("@NewVersion", saga.ExpectedVersion + 1, DbType.Int64);
                    parameters.Add("@LastUpdated", DateTime.UtcNow, DbType.DateTime2);
                    
                    await connection.ExecuteAsync(sql, parameters, transaction);
                    
                    transaction.Commit();
                    
                    // Update the expected version
                    saga.ExpectedVersion++;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
    
    private async Task<long?> GetCurrentVersionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid sagaId)
    {
        var sql = "SELECT Version FROM Sagas WHERE Id = @Id AND Type = @Type";
        
        var parameters = new DynamicParameters();
        parameters.Add("@Id", sagaId, DbType.Guid);
        parameters.Add("@Type", typeof(TSaga).FullName, DbType.String);
        
        return await connection.QueryFirstOrDefaultAsync<long?>(sql, parameters, transaction);
    }
}

## Correlation and Tracking

Correlation and tracking are essential for managing the flow of messages in saga-based systems. They ensure that events and commands are properly associated with the correct saga instances and business processes.

### 1. Correlation IDs

Correlation IDs are unique identifiers that link related messages together across different services and components.

```csharp
public interface ICorrelatedMessage
{
    Guid CorrelationId { get; }
    Guid CausationId { get; }
    Guid MessageId { get; }
}

public abstract class CorrelatedCommand : Command, ICorrelatedMessage
{
    public Guid CorrelationId { get; private set; }
    public Guid CausationId { get; private set; }
    
    protected CorrelatedCommand()
    {
        // Default initialization
        CorrelationId = Guid.NewGuid();
        CausationId = Guid.NewGuid();
    }
    
    // Constructor for creating a correlated command from another message
    protected CorrelatedCommand(ICorrelatedMessage sourceMessage)
    {
        // Maintain correlation chain
        CorrelationId = sourceMessage.CorrelationId;
        CausationId = sourceMessage.MessageId;
    }
}

public abstract class CorrelatedEvent : Event, ICorrelatedMessage
{
    public Guid CorrelationId { get; private set; }
    public Guid CausationId { get; private set; }
    
    protected CorrelatedEvent()
    {
        // Default initialization
        CorrelationId = Guid.NewGuid();
        CausationId = Guid.NewGuid();
    }
    
    // Constructor for creating a correlated event from another message
    protected CorrelatedEvent(ICorrelatedMessage sourceMessage)
    {
        // Maintain correlation chain
        CorrelationId = sourceMessage.CorrelationId;
        CausationId = sourceMessage.MessageId;
    }
}
```

### 2. Message Builder Pattern

The Message Builder pattern simplifies the creation of correlated messages.

```csharp
public static class MessageBuilder
{
    public static TResult From<TSource, TResult>(TSource source, Func<TResult> factory)
        where TSource : ICorrelatedMessage
        where TResult : ICorrelatedMessage
    {
        var result = factory();
        
        // Set correlation properties via reflection
        typeof(TResult).GetProperty("CorrelationId")
            .SetValue(result, source.CorrelationId);
            
        typeof(TResult).GetProperty("CausationId")
            .SetValue(result, source.MessageId);
            
        return result;
    }
}

// Usage in a saga
public class OrderProcessManager : ProcessManager,
    IEventHandler<OrderPlaced>
{
    private readonly ICommandBus _commandBus;
    
    public void Handle(OrderPlaced @event)
    {
        // Create a correlated command
        var processPaymentCommand = MessageBuilder.From(@event, () => new ProcessPayment(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount));
            
        _commandBus.Send(processPaymentCommand);
    }
}
```

### 3. Saga Instance Tracking

Tracking active saga instances is important for monitoring and management purposes.

```csharp
public class SagaTracker : ISagaTracker
{
    private readonly IDocumentStore _documentStore;
    
    public SagaTracker(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }
    
    public async Task RegisterSagaStartedAsync<TSaga>(Guid sagaId, string sagaType, object correlationData)
        where TSaga : ProcessManager
    {
        var sagaInfo = new SagaInfo
        {
            Id = sagaId,
            Type = sagaType,
            Status = SagaStatus.Active,
            StartTime = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            CorrelationData = correlationData
        };
        
        await _documentStore.StoreAsync(sagaInfo);
    }
    
    public async Task UpdateSagaStatusAsync(Guid sagaId, SagaStatus status, string statusReason = null)
    {
        var sagaInfo = await _documentStore.LoadAsync<SagaInfo>(sagaId);
        
        if (sagaInfo != null)
        {
            sagaInfo.Status = status;
            sagaInfo.StatusReason = statusReason;
            sagaInfo.LastUpdated = DateTime.UtcNow;
            
            if (status == SagaStatus.Completed || status == SagaStatus.Failed)
            {
                sagaInfo.EndTime = DateTime.UtcNow;
            }
            
            await _documentStore.StoreAsync(sagaInfo);
        }
    }
    
    public async Task<IEnumerable<SagaInfo>> GetActiveSagasAsync()
    {
        return await _documentStore.Query<SagaInfo>()
            .Where(s => s.Status == SagaStatus.Active)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<SagaInfo>> GetStalledSagasAsync(TimeSpan threshold)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(threshold);
        
        return await _documentStore.Query<SagaInfo>()
            .Where(s => s.Status == SagaStatus.Active && s.LastUpdated < cutoffTime)
            .ToListAsync();
    }
}

public enum SagaStatus
{
    Active,
    Completed,
    Failed,
    Compensating
}

public class SagaInfo
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public SagaStatus Status { get; set; }
    public string StatusReason { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime LastUpdated { get; set; }
    public object CorrelationData { get; set; }
}

public interface ISagaTracker
{
    Task RegisterSagaStartedAsync<TSaga>(Guid sagaId, string sagaType, object correlationData)
        where TSaga : ProcessManager;
    Task UpdateSagaStatusAsync(Guid sagaId, SagaStatus status, string statusReason = null);
    Task<IEnumerable<SagaInfo>> GetActiveSagasAsync();
    Task<IEnumerable<SagaInfo>> GetStalledSagasAsync(TimeSpan threshold);
}
```

### 4. Correlation Context

Correlation context provides a way to track correlation information across service boundaries.

```csharp
public class CorrelationContext
{
    private static readonly AsyncLocal<CorrelationContext> _current = new AsyncLocal<CorrelationContext>();
    
    public static CorrelationContext Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
    
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    public string UserId { get; }
    
    public CorrelationContext(Guid correlationId, Guid causationId, string userId)
    {
        CorrelationId = correlationId;
        CausationId = causationId;
        UserId = userId;
    }
    
    public static void CreateFromMessage(ICorrelatedMessage message, string userId)
    {
        Current = new CorrelationContext(message.CorrelationId, message.MessageId, userId);
    }
    
    public static void Clear()
    {
        Current = null;
    }
}

// Middleware to capture correlation context
public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    
    public CorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Extract correlation IDs from headers
            var correlationId = GetHeaderGuid(context, "X-Correlation-ID") ?? Guid.NewGuid();
            var causationId = GetHeaderGuid(context, "X-Causation-ID") ?? Guid.NewGuid();
            var userId = context.User?.Identity?.Name;
            
            // Set correlation context
            CorrelationContext.Current = new CorrelationContext(correlationId, causationId, userId);
            
            // Add correlation headers to response
            context.Response.Headers["X-Correlation-ID"] = correlationId.ToString();
            
            await _next(context);
        }
        finally
        {
            // Clear correlation context
            CorrelationContext.Clear();
        }
    }
    
    private Guid? GetHeaderGuid(HttpContext context, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var value) && 
            Guid.TryParse(value, out var guid))
        {
            return guid;
        }
        
        return null;
    }
}
