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

### 5. Advanced Test Fixtures for Aggregates

Test fixtures provide a powerful way to simplify and standardize your aggregate tests. Here are several approaches to creating effective test fixtures for event-sourced systems.

#### 5.1 Basic Given-When-Then Test Fixture

A fluent test fixture that follows the Given-When-Then pattern:

```csharp
public class AggregateTestFixture<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly TAggregate _aggregate;
    private readonly List<object> _uncommittedEvents;
    private Exception _caughtException;
    
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
        try
        {
            action(_aggregate);
            _uncommittedEvents.AddRange(_aggregate.GetUncommittedEvents());
        }
        catch (Exception ex)
        {
            _caughtException = ex;
        }
        return this;
    }
    
    public void Then(Action<IEnumerable<object>> assertion)
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected events but got exception: {_caughtException}");
        }
        assertion(_uncommittedEvents);
    }
    
    public void ThenState(Action<TAggregate> assertion)
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected state check but got exception: {_caughtException}");
        }
        assertion(_aggregate);
    }
    
    public void ThenException<TException>(Action<TException> assertion = null) where TException : Exception
    {
        Assert.NotNull(_caughtException);
        Assert.IsType<TException>(_caughtException);
        
        if (assertion != null)
        {
            assertion((TException)_caughtException);
        }
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

// Testing exceptions with detailed assertions
[Fact]
public void WithdrawFunds_WithInsufficientBalance_ShouldThrowInsufficientFundsException()
{
    var accountId = Guid.NewGuid();
    
    new AggregateTestFixture<Account, Guid>()
        .Given(
            new AccountCreated(accountId, "Test Account", "12345"),
            new FundsDeposited(accountId, 100.00m))
        .When(account => account.WithdrawFunds(500.00m))
        .ThenException<InsufficientFundsException>(ex => 
        {
            Assert.Equal(accountId, ex.AccountId);
            Assert.Equal(100.00m, ex.CurrentBalance);
            Assert.Equal(500.00m, ex.WithdrawalAmount);
        });
}
```

#### 5.2 Event-Specific Test Fixture

A more specialized test fixture that provides type-safe event assertions:

```csharp
public class EventTestFixture<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly TAggregate _aggregate;
    private readonly List<object> _uncommittedEvents;
    private Exception _caughtException;
    
    public EventTestFixture()
    {
        _aggregate = new TAggregate();
        _uncommittedEvents = new List<object>();
    }
    
    public EventTestFixture<TAggregate, TId> Given(params object[] events)
    {
        _aggregate.LoadFromHistory(events);
        return this;
    }
    
    public EventTestFixture<TAggregate, TId> When(Action<TAggregate> action)
    {
        try
        {
            action(_aggregate);
            _uncommittedEvents.AddRange(_aggregate.GetUncommittedEvents());
        }
        catch (Exception ex)
        {
            _caughtException = ex;
        }
        return this;
    }
    
    public EventTestFixture<TAggregate, TId> ThenEventCount(int expectedCount)
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected {expectedCount} events but got exception: {_caughtException}");
        }
        
        Assert.Equal(expectedCount, _uncommittedEvents.Count);
        return this;
    }
    
    public EventTestFixture<TAggregate, TId> ThenContainsEvent<TEvent>(Action<TEvent> assertion = null)
        where TEvent : class
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected event of type {typeof(TEvent).Name} but got exception: {_caughtException}");
        }
        
        var matchingEvent = _uncommittedEvents.OfType<TEvent>().FirstOrDefault();
        Assert.NotNull(matchingEvent);
        
        assertion?.Invoke(matchingEvent);
        return this;
    }
    
    public EventTestFixture<TAggregate, TId> ThenNoEvents()
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected no events but got exception: {_caughtException}");
        }
        
        Assert.Empty(_uncommittedEvents);
        return this;
    }
    
    public EventTestFixture<TAggregate, TId> ThenState(Action<TAggregate> assertion)
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected state check but got exception: {_caughtException}");
        }
        
        assertion(_aggregate);
        return this;
    }
    
    public void ThenException<TException>(Action<TException> assertion = null) where TException : Exception
    {
        Assert.NotNull(_caughtException);
        Assert.IsType<TException>(_caughtException);
        
        assertion?.Invoke((TException)_caughtException);
    }
}

// Usage with fluent assertions
[Fact]
public void Account_WithMultipleOperations_ShouldHaveCorrectEvents()
{
    var accountId = Guid.NewGuid();
    
    new EventTestFixture<Account, Guid>()
        .Given(
            new AccountCreated(accountId, "Test Account", "12345"))
        .When(account => 
        {
            account.DepositFunds(1000.00m);
            account.WithdrawFunds(300.00m);
            account.UpdateAccountName("Updated Account");
        })
        .ThenEventCount(3)
        .ThenContainsEvent<FundsDeposited>(e => 
        {
            Assert.Equal(accountId, e.AccountId);
            Assert.Equal(1000.00m, e.Amount);
        })
        .ThenContainsEvent<FundsWithdrawn>(e => 
        {
            Assert.Equal(accountId, e.AccountId);
            Assert.Equal(300.00m, e.Amount);
        })
        .ThenContainsEvent<AccountNameUpdated>(e => 
        {
            Assert.Equal(accountId, e.AccountId);
            Assert.Equal("Updated Account", e.NewName);
        })
        .ThenState(account => 
        {
            Assert.Equal("Updated Account", account.Name);
            Assert.Equal(700.00m, account.Balance);
        });
}
```

#### 5.3 Scenario-Based Test Fixture

A test fixture that supports complex test scenarios with multiple commands and events:

```csharp
public class ScenarioTestFixture<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly TAggregate _aggregate;
    private readonly List<object> _historicalEvents;
    private readonly List<(string Description, Action<TAggregate> Command, List<object> Events)> _steps;
    private Exception _lastException;
    
    public ScenarioTestFixture()
    {
        _aggregate = new TAggregate();
        _historicalEvents = new List<object>();
        _steps = new List<(string, Action<TAggregate>, List<object>)>();
    }
    
    public ScenarioTestFixture<TAggregate, TId> Given(params object[] events)
    {
        _historicalEvents.AddRange(events);
        return this;
    }
    
    public ScenarioTestFixture<TAggregate, TId> WhenCommand(string description, Action<TAggregate> command)
    {
        _steps.Add((description, command, new List<object>()));
        return this;
    }
    
    public void Execute()
    {
        // Load historical events
        _aggregate.LoadFromHistory(_historicalEvents);
        
        // Execute each command and collect events
        foreach (var step in _steps)
        {
            try
            {
                // Clear uncommitted events before executing command
                _aggregate.ClearUncommittedEvents();
                
                // Execute command
                step.Command(_aggregate);
                
                // Collect events
                step.Events.AddRange(_aggregate.GetUncommittedEvents());
            }
            catch (Exception ex)
            {
                _lastException = ex;
                break;
            }
        }
    }
    
    public void AssertEventsAt(int stepIndex, Action<IEnumerable<object>> assertion)
    {
        if (_lastException != null)
        {
            Assert.Fail($"Expected events at step {stepIndex} but got exception: {_lastException}");
        }
        
        if (stepIndex >= _steps.Count)
        {
            Assert.Fail($"Step index {stepIndex} is out of range. Only {_steps.Count} steps were executed.");
        }
        
        assertion(_steps[stepIndex].Events);
    }
    
    public void AssertFinalState(Action<TAggregate> assertion)
    {
        if (_lastException != null)
        {
            Assert.Fail($"Expected final state check but got exception: {_lastException}");
        }
        
        assertion(_aggregate);
    }
    
    public void AssertException<TException>(Action<TException> assertion = null) where TException : Exception
    {
        Assert.NotNull(_lastException);
        Assert.IsType<TException>(_lastException);
        
        assertion?.Invoke((TException)_lastException);
    }
    
    public void PrintScenario()
    {
        Console.WriteLine("Scenario:");
        Console.WriteLine("Given the following events:");
        foreach (var evt in _historicalEvents)
        {
            Console.WriteLine($"  - {evt.GetType().Name}");
        }
        
        Console.WriteLine("When the following commands are executed:");
        for (int i = 0; i < _steps.Count; i++)
        {
            Console.WriteLine($"  {i+1}. {_steps[i].Description}");
            Console.WriteLine($"     Resulting in events:");
            foreach (var evt in _steps[i].Events)
            {
                Console.WriteLine($"     - {evt.GetType().Name}");
            }
        }
    }
}

// Usage for complex scenarios
[Fact]
public void Account_ComplexScenario_ShouldBehaveCorrectly()
{
    var accountId = Guid.NewGuid();
    var fixture = new ScenarioTestFixture<Account, Guid>()
        .Given(
            new AccountCreated(accountId, "Initial Account", "12345"))
        .WhenCommand("Deposit $1000", account => account.DepositFunds(1000.00m))
        .WhenCommand("Withdraw $300", account => account.WithdrawFunds(300.00m))
        .WhenCommand("Update account name", account => account.UpdateAccountName("Updated Account"))
        .WhenCommand("Deposit $200", account => account.DepositFunds(200.00m));
    
    fixture.Execute();
    
    // Assert events from specific steps
    fixture.AssertEventsAt(0, events => 
    {
        Assert.Single(events);
        var depositEvent = events.Single() as FundsDeposited;
        Assert.Equal(1000.00m, depositEvent.Amount);
    });
    
    fixture.AssertEventsAt(2, events => 
    {
        Assert.Single(events);
        var nameUpdateEvent = events.Single() as AccountNameUpdated;
        Assert.Equal("Updated Account", nameUpdateEvent.NewName);
    });
    
    // Assert final state
    fixture.AssertFinalState(account => 
    {
        Assert.Equal("Updated Account", account.Name);
        Assert.Equal(900.00m, account.Balance);
    });
    
    // Print scenario for documentation
    fixture.PrintScenario();
}
```

#### 5.4 Factory for Test Fixtures

A factory approach for creating test fixtures with common setup:

```csharp
public class TestFixtureFactory
{
    public static AggregateTestFixture<Account, Guid> CreateAccountFixture(Guid? accountId = null)
    {
        var id = accountId ?? Guid.NewGuid();
        return new AggregateTestFixture<Account, Guid>()
            .Given(new AccountCreated(id, "Test Account", "12345"));
    }
    
    public static AggregateTestFixture<Account, Guid> CreateAccountWithBalanceFixture(
        decimal initialBalance, 
        Guid? accountId = null)
    {
        var id = accountId ?? Guid.NewGuid();
        return new AggregateTestFixture<Account, Guid>()
            .Given(
                new AccountCreated(id, "Test Account", "12345"),
                new FundsDeposited(id, initialBalance));
    }
    
    public static AggregateTestFixture<Order, Guid> CreateOrderFixture(
        Guid? orderId = null,
        Guid? customerId = null)
    {
        var id = orderId ?? Guid.NewGuid();
        var custId = customerId ?? Guid.NewGuid();
        return new AggregateTestFixture<Order, Guid>()
            .Given(new OrderCreated(id, custId, DateTime.UtcNow));
    }
}

// Usage
[Fact]
public void WithdrawFunds_WithSufficientBalance_ShouldProduceFundsWithdrawnEvent()
{
    TestFixtureFactory
        .CreateAccountWithBalanceFixture(1000.00m)
        .When(account => account.WithdrawFunds(500.00m))
        .Then(events => 
        {
            Assert.Single(events);
            var withdrawalEvent = events.Single() as FundsWithdrawn;
            Assert.Equal(500.00m, withdrawalEvent.Amount);
        });
}
```

#### 5.5 Integration with Reactive Domain Test Framework

Integrating with Reactive Domain's built-in test framework:

```csharp
public class ReactiveDomainTestFixture<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly TestRepository<TAggregate, TId> _repository;
    private readonly TestEventBus _eventBus;
    private TAggregate _aggregate;
    private Exception _caughtException;
    
    public ReactiveDomainTestFixture()
    {
        _eventBus = new TestEventBus();
        _repository = new TestRepository<TAggregate, TId>(_eventBus);
    }
    
    public ReactiveDomainTestFixture<TAggregate, TId> Given(TId id, params object[] events)
    {
        _repository.AddEvents(id, events);
        return this;
    }
    
    public async Task<ReactiveDomainTestFixture<TAggregate, TId>> WhenAsync(Func<TAggregate, Task> action)
    {
        try
        {
            _aggregate = await _repository.GetByIdAsync((dynamic)_repository.LastId);
            await action(_aggregate);
            await _repository.SaveAsync(_aggregate);
        }
        catch (Exception ex)
        {
            _caughtException = ex;
        }
        return this;
    }
    
    public void ThenEvents(Action<IEnumerable<object>> assertion)
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected events but got exception: {_caughtException}");
        }
        
        assertion(_eventBus.PublishedEvents);
    }
    
    public void ThenState(Action<TAggregate> assertion)
    {
        if (_caughtException != null)
        {
            Assert.Fail($"Expected state check but got exception: {_caughtException}");
        }
        
        assertion(_aggregate);
    }
    
    public void ThenException<TException>(Action<TException> assertion = null) where TException : Exception
    {
        Assert.NotNull(_caughtException);
        Assert.IsType<TException>(_caughtException);
        
        assertion?.Invoke((TException)_caughtException);
    }
}

// Usage with async operations
[Fact]
public async Task Account_AsyncOperations_ShouldWorkCorrectly()
{
    var accountId = Guid.NewGuid();
    var fixture = new ReactiveDomainTestFixture<Account, Guid>();
    
    await fixture
        .Given(accountId, 
            new AccountCreated(accountId, "Test Account", "12345"),
            new FundsDeposited(accountId, 1000.00m))
        .WhenAsync(async account => 
        {
            // Simulate async operation
            await Task.Delay(10);
            account.WithdrawFunds(500.00m);
        });
    
    fixture.ThenEvents(events => 
    {
        var withdrawalEvent = events.OfType<FundsWithdrawn>().Single();
        Assert.Equal(accountId, withdrawalEvent.AccountId);
        Assert.Equal(500.00m, withdrawalEvent.Amount);
    });
}
```
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
