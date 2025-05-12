# ProcessManager

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ProcessManager` is a base class in Reactive Domain that provides core functionality for implementing process managers (also known as sagas), which coordinate complex business processes across multiple aggregates.

## Overview

Process managers are responsible for coordinating long-running business processes that span multiple aggregates or bounded contexts. They react to domain events and issue commands to drive the process forward. The `ProcessManager` base class provides the foundation for implementing these coordinators in a consistent way.

In event-driven architectures, process managers solve the problem of maintaining process integrity across aggregate boundaries. While aggregates enforce consistency boundaries within their own scope, process managers ensure that the overall business process completes correctly across multiple aggregates.

## Class Definition

```csharp
public abstract class ProcessManager : IEventSource
{
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    protected ProcessManager(Guid id)
    {
        Id = id;
        ExpectedVersion = -1;
    }
    
    public void RestoreFromEvents(IEnumerable<object> events)
    {
        // Implementation for restoring state from events
    }
    
    public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)
    {
        // Implementation for updating state with new events
    }
    
    public object[] TakeEvents()
    {
        // Implementation for retrieving recorded events
    }
    
    protected void RaiseEvent(object @event)
    {
        // Implementation for raising process manager events
    }
}
```

## Key Features

- **Event Sourcing**: Process managers are event-sourced entities, maintaining their state through events
- **Correlation Tracking**: Supports tracking correlation between events and commands
- **State Management**: Provides mechanisms for managing process state across multiple steps
- **Command Coordination**: Facilitates sending commands to appropriate aggregates
- **Process Completion**: Supports tracking process completion and cleanup
- **Idempotent Processing**: Enables idempotent handling of events to avoid duplicate processing
- **Timeout Management**: Supports handling timeouts for long-running processes

## Usage

### Basic Process Manager

Here's an example of a process manager that coordinates an order fulfillment process:

```csharp
public class OrderFulfillmentProcess : ProcessManager,
    IEventHandler<OrderPlaced>,
    IEventHandler<PaymentProcessed>,
    IEventHandler<InventoryReserved>,
    IEventHandler<ShipmentCreated>
{
    private readonly ICommandBus _commandBus;
    
    // Process state
    private bool _orderPlaced;
    private bool _paymentProcessed;
    private bool _inventoryReserved;
    private bool _shipmentCreated;
    private Guid _orderId;
    private Guid _customerId;
    private decimal _orderAmount;
    private List<OrderItem> _orderItems;
    
    public OrderFulfillmentProcess(Guid processId, ICommandBus commandBus) 
        : base(processId)
    {
        _commandBus = commandBus;
        _orderItems = new List<OrderItem>();
    }
    
    public void Handle(OrderPlaced @event)
    {
        // If we've already processed this event, ignore it
        if (_orderPlaced) return;
        
        // Update process state
        RaiseEvent(MessageBuilder.From(@event, () => new OrderFulfillmentStarted(
            Id,
            @event.OrderId,
            @event.CustomerId,
            @event.OrderAmount,
            @event.OrderItems
        )));
        
        // Send command to process payment
        _commandBus.Send(MessageBuilder.From(@event, () => new ProcessPayment(
            @event.CustomerId,
            @event.OrderId,
            @event.OrderAmount
        )));
    }
    
    public void Handle(PaymentProcessed @event)
    {
        // If we've already processed this event or the order hasn't been placed, ignore it
        if (_paymentProcessed || !_orderPlaced) return;
        
        // Update process state
        RaiseEvent(MessageBuilder.From(@event, () => new PaymentCompletedForOrder(
            Id,
            @event.OrderId,
            @event.PaymentId,
            @event.Amount
        )));
        
        // Send command to reserve inventory
        _commandBus.Send(MessageBuilder.From(@event, () => new ReserveInventory(
            @event.OrderId,
            _orderItems
        )));
    }
    
    public void Handle(InventoryReserved @event)
    {
        // If we've already processed this event or prerequisites aren't met, ignore it
        if (_inventoryReserved || !_paymentProcessed) return;
        
        // Update process state
        RaiseEvent(MessageBuilder.From(@event, () => new InventoryReservedForOrder(
            Id,
            @event.OrderId,
            @event.ReservationId
        )));
        
        // Send command to create shipment
        _commandBus.Send(MessageBuilder.From(@event, () => new CreateShipment(
            @event.OrderId,
            _customerId,
            _orderItems
        )));
    }
    
    public void Handle(ShipmentCreated @event)
    {
        // If we've already processed this event or prerequisites aren't met, ignore it
        if (_shipmentCreated || !_inventoryReserved) return;
        
        // Update process state
        RaiseEvent(MessageBuilder.From(@event, () => new OrderFulfillmentCompleted(
            Id,
            @event.OrderId,
            @event.ShipmentId,
            @event.TrackingNumber
        )));
        
        // Process is now complete
    }
    
    // Event handlers for the process manager's own events
    private void Apply(OrderFulfillmentStarted @event)
    {
        _orderPlaced = true;
        _orderId = @event.OrderId;
        _customerId = @event.CustomerId;
        _orderAmount = @event.OrderAmount;
        _orderItems = @event.OrderItems;
    }
    
    private void Apply(PaymentCompletedForOrder @event)
    {
        _paymentProcessed = true;
    }
    
    private void Apply(InventoryReservedForOrder @event)
    {
        _inventoryReserved = true;
    }
    
    private void Apply(OrderFulfillmentCompleted @event)
    {
        _shipmentCreated = true;
    }
}
```

### Registering Process Managers

Process managers are typically registered with an event bus during application startup:

```csharp
public void ConfigureProcessManagers(
    IEventBus eventBus, 
    ICommandBus commandBus,
    IProcessManagerRepository repository)
{
    // Create a factory for the process manager
    Func<Guid, OrderFulfillmentProcess> factory = 
        id => new OrderFulfillmentProcess(id, commandBus);
    
    // Register event handlers that will route events to the appropriate process manager instance
    eventBus.Subscribe<OrderPlaced>(new ProcessManagerRouter<OrderFulfillmentProcess, OrderPlaced>(
        repository,
        factory,
        e => e.OrderId, // Use OrderId to find or create process manager instances
        (pm, e) => pm.Handle(e)
    ));
    
    eventBus.Subscribe<PaymentProcessed>(new ProcessManagerRouter<OrderFulfillmentProcess, PaymentProcessed>(
        repository,
        factory,
        e => e.OrderId, // Use OrderId to find the process manager instance
        (pm, e) => pm.Handle(e)
    ));
    
    // Register other event handlers similarly
}
```

## Best Practices

1. **Single Responsibility**: Each process manager should handle one business process
2. **Idempotent Handling**: Make event handlers idempotent to handle duplicate events safely
3. **State Tracking**: Maintain clear state to track process progress
4. **Error Handling**: Implement proper error handling and recovery mechanisms
5. **Timeouts**: Include timeout handling for processes that might stall
6. **Correlation**: Maintain correlation IDs throughout the process
7. **Compensating Actions**: Implement compensating actions for handling failures
8. **Process Completion**: Clearly define when a process is complete and can be archived
9. **Monitoring**: Add monitoring to track process state and identify stalled processes
10. **Testing**: Write comprehensive tests that verify the entire process flow

## Common Pitfalls

1. **Complex Process Managers**: Avoid creating overly complex process managers that handle too many responsibilities
2. **Missing Error Handling**: Failing to handle errors can leave processes in an inconsistent state
3. **Ignoring Timeouts**: Long-running processes need timeout handling to avoid stalled processes
4. **Direct Aggregate Manipulation**: Process managers should send commands, not directly manipulate aggregates
5. **Tight Coupling**: Avoid tightly coupling process managers to specific aggregate implementations
6. **Missing Idempotency**: Without idempotent handling, duplicate events can cause incorrect behavior
7. **State Explosion**: Too many state variables can make process managers difficult to understand and maintain

## Related Components

- [IEventSource](./ievent-source.md): Interface implemented by process managers for event sourcing
- [IEventHandler](./ievent-handler.md): Interface for handling domain events
- [Command](./command.md): Messages sent by process managers to drive the process forward
- [Event](./event.md): Messages that trigger process manager actions
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [AggregateRoot](./aggregate-root.md): Domain entities that process managers coordinate

---

**Navigation**:
- [← Previous: IEventHandler](./ievent-handler.md)
- [↑ Back to Top](#processmanager)
- [→ Next: ICorrelatedEventSource](./icorrelated-event-source.md)
