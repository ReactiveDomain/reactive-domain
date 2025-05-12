# Testing Patterns for Event-Sourced Systems

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

This document outlines the key patterns and best practices for testing event-sourced systems built with Reactive Domain. Effective testing is crucial for ensuring the correctness, reliability, and maintainability of event-driven applications.

## Table of Contents

1. [Testing Principles for Event-Sourced Systems](#testing-principles-for-event-sourced-systems)
2. [Unit Testing Aggregates](#unit-testing-aggregates)
3. [Testing Event Handlers](#testing-event-handlers)
4. [Testing Sagas and Process Managers](#testing-sagas-and-process-managers)
5. [Integration Testing](#integration-testing)
6. [Event Store Testing](#event-store-testing)
7. [Snapshot Testing](#snapshot-testing)
8. [Testing Event Versioning](#testing-event-versioning)
9. [Performance Testing](#performance-testing)
10. [Best Practices](#best-practices)

## Testing Principles for Event-Sourced Systems

Testing event-sourced systems requires a different approach compared to traditional CRUD applications. Here are the key principles to guide your testing strategy:

1. **Command-Event-State**: Test that commands produce the expected events and that events correctly modify state.
2. **Historical Invariants**: Test that past events are correctly applied to rebuild the current state.
3. **Behavior Over Implementation**: Focus tests on the behavior of aggregates rather than internal implementation details.
4. **Event-Centric**: Center tests around the events that are produced, as they represent the facts about what happened.
5. **Deterministic**: Tests should be deterministic and repeatable, avoiding dependencies on external systems when possible.

### Testing Pyramid for Event-Sourced Systems

A balanced testing strategy for event-sourced systems typically includes:

1. **Unit Tests**: Testing individual aggregates, commands, and event handlers in isolation.
2. **Integration Tests**: Testing the interaction between components, such as aggregates and repositories.
3. **System Tests**: Testing the entire system, including event store, projections, and external interfaces.
4. **Acceptance Tests**: Testing from the user's perspective to ensure the system meets requirements.

## Unit Testing Aggregates

Unit testing aggregates is the foundation of testing event-sourced systems. These tests focus on ensuring that commands produce the expected events and that events correctly modify state.

### 1. Given-When-Then Pattern

The Given-When-Then pattern is particularly well-suited for testing event-sourced systems:

- **Given**: The historical events that have occurred
- **When**: The command being executed
- **Then**: The expected events that should be produced

```csharp
[Fact]
public void WithdrawFunds_WithSufficientBalance_ShouldProduceFundsWithdrawnEvent()
{
    // Given
    var accountId = Guid.NewGuid();
    var events = new List<object>
    {
        new AccountCreated(accountId, "Test Account", "12345"),
        new FundsDeposited(accountId, 1000.00m)
    };
    
    var account = new Account();
    account.LoadFromHistory(events);
    
    // When
    account.WithdrawFunds(500.00m);
    
    // Then
    var uncommittedEvents = account.GetUncommittedEvents().ToList();
    Assert.Single(uncommittedEvents);
    
    var withdrawalEvent = uncommittedEvents[0] as FundsWithdrawn;
    Assert.NotNull(withdrawalEvent);
    Assert.Equal(accountId, withdrawalEvent.AccountId);
    Assert.Equal(500.00m, withdrawalEvent.Amount);
}
```

### 2. Testing Command Validation

Test that commands are properly validated and rejected when invalid:

```csharp
[Fact]
public void WithdrawFunds_WithInsufficientBalance_ShouldThrowInsufficientFundsException()
{
    // Given
    var accountId = Guid.NewGuid();
    var events = new List<object>
    {
        new AccountCreated(accountId, "Test Account", "12345"),
        new FundsDeposited(accountId, 100.00m)
    };
    
    var account = new Account();
    account.LoadFromHistory(events);
    
    // When/Then
    var exception = Assert.Throws<InsufficientFundsException>(() => 
        account.WithdrawFunds(500.00m));
    
    Assert.Equal(accountId, exception.AccountId);
    Assert.Equal(100.00m, exception.CurrentBalance);
    Assert.Equal(500.00m, exception.WithdrawalAmount);
}
```

### 3. Testing Event Application

Test that events correctly modify the aggregate's state:

```csharp
[Fact]
public void ApplyFundsDeposited_ShouldIncreaseBalance()
{
    // Given
    var account = new Account();
    account.LoadFromHistory(new List<object>
    {
        new AccountCreated(Guid.NewGuid(), "Test Account", "12345")
    });
    
    // Initial balance should be zero
    Assert.Equal(0m, account.Balance);
    
    // When
    var depositEvent = new FundsDeposited(account.Id, 100.00m);
    account.ApplyEvent(depositEvent);
    
    // Then
    Assert.Equal(100.00m, account.Balance);
}
```

### 4. Testing Aggregate Reconstruction

Test that an aggregate can be correctly reconstructed from its event history:

```csharp
[Fact]
public void LoadFromHistory_ShouldReconstructAggregateState()
{
    // Given
    var accountId = Guid.NewGuid();
    var events = new List<object>
    {
        new AccountCreated(accountId, "Test Account", "12345"),
        new FundsDeposited(accountId, 100.00m),
        new FundsDeposited(accountId, 50.00m),
        new FundsWithdrawn(accountId, 30.00m)
    };
    
    // When
    var account = new Account();
    account.LoadFromHistory(events);
    
    // Then
    Assert.Equal(accountId, account.Id);
    Assert.Equal("Test Account", account.Name);
    Assert.Equal("12345", account.AccountNumber);
    Assert.Equal(120.00m, account.Balance);
    Assert.Empty(account.GetUncommittedEvents());
}
```

### 5. Test Fixture for Aggregates

Create a test fixture to simplify aggregate testing:

```csharp
public class AggregateTestFixture<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly TAggregate _aggregate;
    private readonly List<object> _uncommittedEvents;
    
    public AggregateTestFixture()
    {
        _aggregate = new TAggregate();
        _uncommittedEvents = new List<object>();
    }
    
    public AggregateTestFixture<TAggregate, TId> Given(params object[] events)
    {
        _aggregate.LoadFromHistory(events);
        return this;
    }
    
    public AggregateTestFixture<TAggregate, TId> When(Action<TAggregate> action)
    {
        action(_aggregate);
        _uncommittedEvents.AddRange(_aggregate.GetUncommittedEvents());
        return this;
    }
    
    public void Then(Action<IEnumerable<object>> assertion)
    {
        assertion(_uncommittedEvents);
    }
    
    public void ThenState(Action<TAggregate> assertion)
    {
        assertion(_aggregate);
    }
    
    public void ThenException<TException>(Action<TAggregate> action) where TException : Exception
    {
        Assert.Throws<TException>(() => action(_aggregate));
    }
}

// Usage
[Fact]
public void WithdrawFunds_WithSufficientBalance_ShouldProduceFundsWithdrawnEvent()
{
    var accountId = Guid.NewGuid();
    
    new AggregateTestFixture<Account, Guid>()
        .Given(
            new AccountCreated(accountId, "Test Account", "12345"),
            new FundsDeposited(accountId, 1000.00m))
        .When(account => account.WithdrawFunds(500.00m))
        .Then(events => 
        {
            Assert.Single(events);
            var withdrawalEvent = events.Single() as FundsWithdrawn;
            Assert.NotNull(withdrawalEvent);
            Assert.Equal(accountId, withdrawalEvent.AccountId);
            Assert.Equal(500.00m, withdrawalEvent.Amount);
        });
}
```

## Testing Event Handlers

Event handlers are responsible for updating read models and triggering side effects in response to events. Testing them requires verifying that they correctly process events and produce the expected outcomes.

### 1. Testing Read Model Projections

Test that event handlers correctly update read models:

```csharp
[Fact]
public void AccountSummaryProjection_ShouldUpdateReadModel()
{
    // Given
    var accountId = Guid.NewGuid();
    var readModelStore = new InMemoryReadModelStore<AccountSummary>();
    var projection = new AccountSummaryProjection(readModelStore);
    
    // When
    projection.Handle(new AccountCreated(accountId, "Test Account", "12345"));
    projection.Handle(new FundsDeposited(accountId, 100.00m));
    
    // Then
    var readModel = readModelStore.Get(accountId.ToString());
    Assert.NotNull(readModel);
    Assert.Equal("Test Account", readModel.AccountName);
    Assert.Equal("12345", readModel.AccountNumber);
    Assert.Equal(100.00m, readModel.CurrentBalance);
}
```

### 2. Testing Side Effects

Test that event handlers correctly trigger side effects:

```csharp
[Fact]
public void LargeDepositHandler_ShouldNotifyComplianceForLargeDeposits()
{
    // Given
    var mockComplianceService = new Mock<IComplianceService>();
    var handler = new LargeDepositHandler(mockComplianceService.Object);
    var accountId = Guid.NewGuid();
    
    // When
    handler.Handle(new FundsDeposited(accountId, 10000.00m));
    
    // Then
    mockComplianceService.Verify(s => 
        s.ReportLargeDeposit(accountId, 10000.00m), Times.Once);
}
```

### 3. Testing Event Processor

Test the event processor that dispatches events to handlers:

```csharp
[Fact]
public void EventProcessor_ShouldDispatchEventsToHandlers()
{
    // Given
    var mockHandler1 = new Mock<IEventHandler<AccountCreated>>();
    var mockHandler2 = new Mock<IEventHandler<FundsDeposited>>();
    
    var eventProcessor = new EventProcessor();
    eventProcessor.RegisterHandler(mockHandler1.Object);
    eventProcessor.RegisterHandler(mockHandler2.Object);
    
    var accountCreatedEvent = new AccountCreated(Guid.NewGuid(), "Test Account", "12345");
    var fundsDepositedEvent = new FundsDeposited(Guid.NewGuid(), 100.00m);
    
    // When
    eventProcessor.Process(accountCreatedEvent);
    eventProcessor.Process(fundsDepositedEvent);
    
    // Then
    mockHandler1.Verify(h => h.Handle(accountCreatedEvent), Times.Once);
    mockHandler2.Verify(h => h.Handle(fundsDepositedEvent), Times.Once);
}
```

## Testing Sagas and Process Managers

Sagas and process managers coordinate complex business processes across multiple aggregates. Testing them requires verifying that they correctly respond to events and issue the appropriate commands.

### 1. Testing Saga State Changes

Test that events correctly update the saga's state:

```csharp
[Fact]
public void OrderProcessSaga_ShouldUpdateStateWhenOrderPlaced()
{
    // Given
    var sagaId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
    var customerId = Guid.NewGuid();
    
    var saga = new OrderProcessSaga(sagaId);
    
    // When
    saga.Apply(new OrderPlaced(orderId, customerId, 100.00m));
    
    // Then
    Assert.Equal(sagaId, saga.Id);
    Assert.Equal(orderId, saga.OrderId);
    Assert.Equal(customerId, saga.CustomerId);
    Assert.Equal(100.00m, saga.OrderAmount);
    Assert.Equal(OrderProcessSagaState.OrderPlaced, saga.State);
}
```

### 2. Testing Saga Command Dispatch

Test that sagas dispatch the correct commands in response to events:

```csharp
[Fact]
public void OrderProcessSaga_ShouldDispatchPaymentCommandWhenOrderPlaced()
{
    // Given
    var mockCommandBus = new Mock<ICommandBus>();
    var sagaRepository = new InMemorySagaRepository<OrderProcessSaga>();
    
    var sagaManager = new SagaManager<OrderProcessSaga>(
        sagaRepository,
        mockCommandBus.Object);
    
    var orderId = Guid.NewGuid();
    var customerId = Guid.NewGuid();
    var orderPlacedEvent = new OrderPlaced(orderId, customerId, 100.00m);
    
    // When
    sagaManager.Handle(orderPlacedEvent);
    
    // Then
    mockCommandBus.Verify(cb => 
        cb.Send(It.Is<ProcessPayment>(cmd => 
            cmd.OrderId == orderId && 
            cmd.Amount == 100.00m)), 
        Times.Once);
}
```

### 3. Testing Saga Completion

Test that sagas complete correctly when all steps are done:

```csharp
[Fact]
public void OrderProcessSaga_ShouldCompleteWhenOrderFulfilled()
{
    // Given
    var sagaId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
    var customerId = Guid.NewGuid();
    
    var saga = new OrderProcessSaga(sagaId);
    saga.Apply(new OrderPlaced(orderId, customerId, 100.00m));
    saga.Apply(new PaymentProcessed(orderId, 100.00m));
    saga.Apply(new OrderShipped(orderId, "123456789"));
    
    // When
    saga.Apply(new OrderDelivered(orderId));
    
    // Then
    Assert.Equal(OrderProcessSagaState.Completed, saga.State);
    Assert.True(saga.IsCompleted);
}
```

## Integration Testing

Integration tests verify that components work correctly together, including the event store, repositories, and command/event handlers.

### 1. Testing Repository Operations

Test that repositories correctly store and retrieve aggregates:

```csharp
[Fact]
public async Task Repository_ShouldSaveAndLoadAggregate()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var repository = new Repository<Account, Guid>(eventStore);
    
    var accountId = Guid.NewGuid();
    var account = new Account();
    account.Create(accountId, "Test Account", "12345");
    account.DepositFunds(100.00m);
    
    // When
    await repository.SaveAsync(account);
    var loadedAccount = await repository.GetByIdAsync(accountId);
    
    // Then
    Assert.NotNull(loadedAccount);
    Assert.Equal(accountId, loadedAccount.Id);
    Assert.Equal("Test Account", loadedAccount.Name);
    Assert.Equal("12345", loadedAccount.AccountNumber);
    Assert.Equal(100.00m, loadedAccount.Balance);
}
```

### 2. Testing Command Handling Pipeline

Test the entire command handling pipeline:

```csharp
[Fact]
public async Task CommandHandlingPipeline_ShouldProcessCommandAndUpdateReadModel()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var repository = new Repository<Account, Guid>(eventStore);
    var readModelStore = new InMemoryReadModelStore<AccountSummary>();
    var projection = new AccountSummaryProjection(readModelStore);
    
    var eventBus = new EventBus();
    eventBus.Subscribe<AccountCreated>(projection.Handle);
    eventBus.Subscribe<FundsDeposited>(projection.Handle);
    
    var commandHandler = new CreateAccountHandler(repository, eventBus);
    var commandBus = new CommandBus();
    commandBus.Register<CreateAccount>(commandHandler.Handle);
    
    var accountId = Guid.NewGuid();
    var command = new CreateAccount(accountId, "Test Account", "12345");
    
    // When
    await commandBus.SendAsync(command);
    
    // Then
    var account = await repository.GetByIdAsync(accountId);
    Assert.NotNull(account);
    Assert.Equal("Test Account", account.Name);
    
    var readModel = readModelStore.Get(accountId.ToString());
    Assert.NotNull(readModel);
    Assert.Equal("Test Account", readModel.AccountName);
    Assert.Equal("12345", readModel.AccountNumber);
}
```

### 3. Testing Event Replay

Test that events can be replayed to rebuild read models:

```csharp
[Fact]
public async Task EventReplay_ShouldRebuildReadModel()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var repository = new Repository<Account, Guid>(eventStore);
    
    var accountId = Guid.NewGuid();
    var account = new Account();
    account.Create(accountId, "Test Account", "12345");
    account.DepositFunds(100.00m);
    account.WithdrawFunds(50.00m);
    await repository.SaveAsync(account);
    
    // When
    var readModelStore = new InMemoryReadModelStore<AccountSummary>();
    var projection = new AccountSummaryProjection(readModelStore);
    
    var events = await eventStore.GetEventsForAggregateAsync(accountId);
    foreach (var @event in events)
    {
        switch (@event)
        {
            case AccountCreated e: projection.Handle(e); break;
            case FundsDeposited e: projection.Handle(e); break;
            case FundsWithdrawn e: projection.Handle(e); break;
        }
    }
    
    // Then
    var readModel = readModelStore.Get(accountId.ToString());
    Assert.NotNull(readModel);
    Assert.Equal("Test Account", readModel.AccountName);
    Assert.Equal("12345", readModel.AccountNumber);
    Assert.Equal(50.00m, readModel.CurrentBalance);
}
```

## Event Store Testing

Testing the event store ensures that events are correctly persisted and retrieved.

### 1. Testing Event Persistence

Test that events are correctly persisted to the event store:

```csharp
[Fact]
public async Task EventStore_ShouldPersistEvents()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var aggregateId = Guid.NewGuid();
    var events = new List<object>
    {
        new AccountCreated(aggregateId, "Test Account", "12345"),
        new FundsDeposited(aggregateId, 100.00m)
    };
    
    // When
    await eventStore.SaveEventsAsync(aggregateId, events, 0);
    
    // Then
    var storedEvents = await eventStore.GetEventsForAggregateAsync(aggregateId);
    Assert.Equal(2, storedEvents.Count());
    Assert.IsType<AccountCreated>(storedEvents.First());
    Assert.IsType<FundsDeposited>(storedEvents.Skip(1).First());
}
```

### 2. Testing Concurrency Control

Test that the event store correctly handles concurrency conflicts:

```csharp
[Fact]
public async Task EventStore_ShouldDetectConcurrencyConflicts()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var aggregateId = Guid.NewGuid();
    var events1 = new List<object> { new AccountCreated(aggregateId, "Test Account", "12345") };
    
    // Save the first batch of events
    await eventStore.SaveEventsAsync(aggregateId, events1, 0);
    
    // When/Then
    var events2 = new List<object> { new FundsDeposited(aggregateId, 100.00m) };
    
    // Try to save with wrong expected version
    await Assert.ThrowsAsync<ConcurrencyException>(() => 
        eventStore.SaveEventsAsync(aggregateId, events2, 0));
    
    // Should succeed with correct expected version
    await eventStore.SaveEventsAsync(aggregateId, events2, 1);
}
```

### 3. Testing Event Serialization

Test that events can be correctly serialized and deserialized:

```csharp
[Fact]
public void EventSerializer_ShouldSerializeAndDeserializeEvents()
{
    // Given
    var serializer = new JsonEventSerializer();
    var aggregateId = Guid.NewGuid();
    var originalEvent = new AccountCreated(aggregateId, "Test Account", "12345");
    
    // When
    var serialized = serializer.Serialize(originalEvent);
    var deserialized = serializer.Deserialize(serialized, typeof(AccountCreated)) as AccountCreated;
    
    // Then
    Assert.NotNull(deserialized);
    Assert.Equal(aggregateId, deserialized.AccountId);
    Assert.Equal("Test Account", deserialized.AccountName);
    Assert.Equal("12345", deserialized.AccountNumber);
}
```

## Snapshot Testing

Testing snapshot functionality ensures that aggregates can be efficiently loaded from snapshots.

### 1. Testing Snapshot Creation

Test that snapshots are correctly created:

```csharp
[Fact]
public async Task SnapshotRepository_ShouldCreateSnapshot()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var snapshotStore = new InMemorySnapshotStore();
    var repository = new SnapshotRepository<Account, Guid>(
        eventStore, snapshotStore, 2);
    
    var accountId = Guid.NewGuid();
    var account = new Account();
    account.Create(accountId, "Test Account", "12345");
    account.DepositFunds(100.00m);
    account.DepositFunds(50.00m);  // This should trigger a snapshot
    
    // When
    await repository.SaveAsync(account);
    
    // Then
    var snapshot = await snapshotStore.GetSnapshotAsync(accountId);
    Assert.NotNull(snapshot);
    Assert.Equal(accountId, snapshot.AggregateId);
    Assert.Equal(3, snapshot.Version);
    
    var accountState = snapshot.State as AccountState;
    Assert.NotNull(accountState);
    Assert.Equal("Test Account", accountState.Name);
    Assert.Equal("12345", accountState.AccountNumber);
    Assert.Equal(150.00m, accountState.Balance);
}
```

### 2. Testing Aggregate Loading from Snapshot

Test that aggregates can be correctly loaded from snapshots:

```csharp
[Fact]
public async Task SnapshotRepository_ShouldLoadAggregateFromSnapshot()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var snapshotStore = new InMemorySnapshotStore();
    var repository = new SnapshotRepository<Account, Guid>(
        eventStore, snapshotStore, 2);
    
    var accountId = Guid.NewGuid();
    var account = new Account();
    account.Create(accountId, "Test Account", "12345");
    account.DepositFunds(100.00m);
    account.DepositFunds(50.00m);  // This should trigger a snapshot
    await repository.SaveAsync(account);
    
    // When
    var loadedAccount = await repository.GetByIdAsync(accountId);
    
    // Then
    Assert.NotNull(loadedAccount);
    Assert.Equal(accountId, loadedAccount.Id);
    Assert.Equal("Test Account", loadedAccount.Name);
    Assert.Equal("12345", loadedAccount.AccountNumber);
    Assert.Equal(150.00m, loadedAccount.Balance);
    Assert.Equal(3, loadedAccount.Version);
}
```

## Testing Event Versioning

Testing event versioning ensures that the system can handle changes to event schemas over time.

### 1. Testing Event Upcasting

Test that old event versions can be upcasted to new versions:

```csharp
[Fact]
public void EventUpcaster_ShouldUpcastOldEventVersions()
{
    // Given
    var upcaster = new AccountCreatedEventUpcaster();
    var oldEvent = new AccountCreatedV1
    {
        AccountId = Guid.NewGuid(),
        Name = "Test Account",
        AccountNumber = "12345"
    };
    
    // When
    var newEvent = upcaster.Upcast(oldEvent) as AccountCreatedV2;
    
    // Then
    Assert.NotNull(newEvent);
    Assert.Equal(oldEvent.AccountId, newEvent.AccountId);
    Assert.Equal(oldEvent.Name, newEvent.AccountName);
    Assert.Equal(oldEvent.AccountNumber, newEvent.AccountNumber);
    Assert.Equal(DateTime.UtcNow.Date, newEvent.CreatedDate.Date);
}
```

### 2. Testing Backward Compatibility

Test that new event handlers can process old event versions:

```csharp
[Fact]
public void EventHandler_ShouldHandleOldAndNewEventVersions()
{
    // Given
    var readModelStore = new InMemoryReadModelStore<AccountSummary>();
    var projection = new AccountSummaryProjection(readModelStore);
    
    var accountId = Guid.NewGuid();
    var oldEvent = new AccountCreatedV1
    {
        AccountId = accountId,
        Name = "Test Account",
        AccountNumber = "12345"
    };
    
    var newEvent = new AccountCreatedV2
    {
        AccountId = Guid.NewGuid(),
        AccountName = "New Account",
        AccountNumber = "67890",
        CreatedDate = DateTime.UtcNow
    };
    
    // When
    projection.Handle(oldEvent);
    projection.Handle(newEvent);
    
    // Then
    var oldAccountModel = readModelStore.Get(accountId.ToString());
    Assert.NotNull(oldAccountModel);
    Assert.Equal("Test Account", oldAccountModel.AccountName);
    
    var newAccountModel = readModelStore.Get(newEvent.AccountId.ToString());
    Assert.NotNull(newAccountModel);
    Assert.Equal("New Account", newAccountModel.AccountName);
}
```

## Performance Testing

Performance testing ensures that the event-sourced system can handle the expected load and scale appropriately.

### 1. Testing Event Store Performance

Test the performance of event store operations:

```csharp
[Fact]
public async Task EventStore_ShouldHandleHighVolumeOfEvents()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var aggregateId = Guid.NewGuid();
    const int eventCount = 1000;
    
    // When
    var stopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < eventCount; i++)
    {
        var events = new List<object> { new FundsDeposited(aggregateId, 1.00m) };
        await eventStore.SaveEventsAsync(aggregateId, events, i);
    }
    
    stopwatch.Stop();
    
    // Then
    var storedEvents = await eventStore.GetEventsForAggregateAsync(aggregateId);
    Assert.Equal(eventCount, storedEvents.Count());
    
    // Performance assertion - adjust threshold as needed
    Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
        $"Saving {eventCount} events took {stopwatch.ElapsedMilliseconds}ms");
}
```

### 2. Testing Snapshot Performance

Test the performance improvement from using snapshots:

```csharp
[Fact]
public async Task SnapshotRepository_ShouldImproveLoadPerformance()
{
    // Given
    var eventStore = new InMemoryEventStore();
    var snapshotStore = new InMemorySnapshotStore();
    var repository = new SnapshotRepository<Account, Guid>(
        eventStore, snapshotStore, 100);
    
    var accountId = Guid.NewGuid();
    var account = new Account();
    account.Create(accountId, "Test Account", "12345");
    
    // Add many events
    for (int i = 0; i < 200; i++)
    {
        account.DepositFunds(1.00m);
    }
    
    await repository.SaveAsync(account);
    
    // When - Load without using snapshot
    var regularRepository = new Repository<Account, Guid>(eventStore);
    
    var stopwatchWithoutSnapshot = Stopwatch.StartNew();
    await regularRepository.GetByIdAsync(accountId);
    stopwatchWithoutSnapshot.Stop();
    
    // When - Load using snapshot
    var stopwatchWithSnapshot = Stopwatch.StartNew();
    await repository.GetByIdAsync(accountId);
    stopwatchWithSnapshot.Stop();
    
    // Then
    Assert.True(stopwatchWithSnapshot.ElapsedMilliseconds < stopwatchWithoutSnapshot.ElapsedMilliseconds,
        $"Loading with snapshot ({stopwatchWithSnapshot.ElapsedMilliseconds}ms) should be faster than without ({stopwatchWithoutSnapshot.ElapsedMilliseconds}ms)");
}
```

## Best Practices

Here are some best practices for testing event-sourced systems:

1. **Use the Given-When-Then Pattern**: This pattern is particularly well-suited for testing event-sourced systems.

2. **Test Both Commands and Events**: Ensure that commands produce the expected events and that events correctly modify state.

3. **Create Test Fixtures**: Build reusable test fixtures to simplify testing and reduce boilerplate code.

4. **Use In-Memory Implementations**: Use in-memory implementations of the event store and repositories for unit and integration tests.

5. **Test Event Replay**: Verify that replaying events produces the correct state.

6. **Test Concurrency Handling**: Ensure that concurrent modifications are correctly handled.

7. **Test Event Versioning**: Verify that the system can handle changes to event schemas over time.

8. **Test Performance**: Ensure that the system can handle the expected load and scale appropriately.

9. **Automate Testing**: Use continuous integration to run tests automatically.

10. **Test Edge Cases**: Test boundary conditions, error scenarios, and edge cases to ensure robust behavior.
