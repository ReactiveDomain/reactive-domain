# Troubleshooting Guide

[← Back to Table of Contents](README.md)

This guide addresses common issues and challenges when working with Reactive Domain, providing solutions and workarounds for each problem.

## Table of Contents

- [Event Versioning and Schema Evolution](#event-versioning-and-schema-evolution)
- [Handling Concurrency Conflicts](#handling-concurrency-conflicts)
- [Debugging Event-Sourced Systems](#debugging-event-sourced-systems)
- [Performance Issues and Optimization](#performance-issues-and-optimization)
- [Integration Challenges](#integration-challenges)
- [Testing Strategies and Common Issues](#testing-strategies-and-common-issues)
- [Deployment Considerations](#deployment-considerations)
- [Monitoring and Observability](#monitoring-and-observability)

## Event Versioning and Schema Evolution

### Problem: Events Need to Change Over Time

As your domain model evolves, you'll need to modify your event schemas. However, you still need to be able to process historical events that were serialized with the old schema.

### Solution: Implement Event Versioning

1. **Use Event Upcasting**

```csharp
public class EventUpcastingService : IEventUpcastingService
{
    public object Upcast(object @event)
    {
        if (@event is AccountCreatedV1 v1Event)
        {
            return new AccountCreatedV2
            {
                AccountId = v1Event.AccountId,
                AccountNumber = v1Event.AccountNumber,
                CustomerName = v1Event.CustomerName,
                // New field in V2
                CreatedDate = DateTime.UtcNow
            };
        }
        
        return @event;
    }
}
```

2. **Use Event Wrappers with Version Information**

```csharp
public class EventWrapper<T>
{
    public int Version { get; set; }
    public T Data { get; set; }
}
```

3. **Handle Missing Properties Gracefully**

```csharp
public void Apply(AccountCreated @event)
{
    _accountNumber = @event.AccountNumber;
    _customerName = @event.CustomerName;
    
    // Handle optional property that might not exist in older events
    if (@event.GetType().GetProperty("CreatedDate") != null)
    {
        _createdDate = (DateTime)@event.GetType().GetProperty("CreatedDate").GetValue(@event);
    }
    else
    {
        _createdDate = DateTime.UtcNow; // Default value
    }
}
```

### Best Practices

1. **Never Delete or Modify Existing Events** - Always create new versions and implement upcasting.
2. **Use Semantic Versioning** - Follow a consistent versioning scheme for your events.
3. **Document Event Schema Changes** - Maintain a changelog of event schema modifications.
4. **Test Event Upcasting** - Ensure that historical events can be properly upcasted to current versions.

As your system evolves, you'll need to modify your event schemas to add, remove, or change properties.

### Solution: Event Versioning Strategies

1. **Backward Compatible Changes**

   When adding new fields to events, make them optional with sensible defaults:

   ```csharp
   // Original event
   public class CustomerCreated : Event
   {
       public readonly Guid CustomerId;
       public readonly string Name;
       
       public CustomerCreated(Guid customerId, string name)
       {
           CustomerId = customerId;
           Name = name;
       }
   }
   
   // Updated event with backward compatibility
   public class CustomerCreated : Event
   {
       public readonly Guid CustomerId;
       public readonly string Name;
       public readonly string Email; // New field
       
       public CustomerCreated(Guid customerId, string name, string email = null)
       {
           CustomerId = customerId;
           Name = name;
           Email = email; // Optional with null default
       }
   }
   ```

2. **Explicit Versioning**

   Create new event types with version numbers:

   ```csharp
   public class CustomerCreatedV1 : Event
   {
       public readonly Guid CustomerId;
       public readonly string Name;
       
       public CustomerCreatedV1(Guid customerId, string name)
       {
           CustomerId = customerId;
           Name = name;
       }
   }
   
   public class CustomerCreatedV2 : Event
   {
       public readonly Guid CustomerId;
       public readonly string Name;
       public readonly string Email;
       
       public CustomerCreatedV2(Guid customerId, string name, string email)
       {
           CustomerId = customerId;
           Name = name;
           Email = email;
       }
   }
   ```

3. **Event Upcasting**

   Transform old events to new versions during deserialization:

   ```csharp
   public class EventUpcastingSerializer : IEventSerializer
   {
       private readonly IEventSerializer _innerSerializer;
       
       public EventUpcastingSerializer(IEventSerializer innerSerializer)
       {
           _innerSerializer = innerSerializer;
       }
       
       public object Deserialize(RecordedEvent recordedEvent)
       {
           var deserialized = _innerSerializer.Deserialize(recordedEvent);
           
           // Upcast old event versions to new versions
           if (deserialized is CustomerCreatedV1 v1)
           {
               return new CustomerCreatedV2(v1.CustomerId, v1.Name, null);
           }
           
           return deserialized;
       }
       
       public IEventData Serialize(object @event, Guid eventId)
       {
           return _innerSerializer.Serialize(@event, eventId);
       }
   }
   ```

### Best Practices

1. **Never Delete Events**: Once events are in production, never delete them. Always maintain backward compatibility.
2. **Design for Evolution**: Anticipate changes and design your events to be extensible.
3. **Use Event Versioning**: Explicitly version your events when making breaking changes.
4. **Document Changes**: Maintain a changelog of event schema changes.
5. **Test Migration Paths**: Ensure that old events can be processed by new code.

## Handling Concurrency Conflicts

### Problem: Concurrent Modifications to the Same Aggregate

In event-sourced systems, concurrent modifications to the same aggregate can lead to concurrency conflicts when the second operation tries to save its changes.

### Solution: Implement Optimistic Concurrency Control

1. **Use Expected Version When Saving Events**

```csharp
public void Save<TAggregate>(TAggregate aggregate) where TAggregate : AggregateRoot, IEventSource
{
    var events = aggregate.GetUncommittedEvents().ToArray();
    if (!events.Any()) return;
    
    var streamName = GetStreamName(aggregate.GetType(), aggregate.Id);
    var expectedVersion = aggregate.Version - events.Length;
    
    try
    {
        _connection.AppendToStream(streamName, expectedVersion, events);
        aggregate.ClearUncommittedEvents();
    }
    catch (WrongExpectedVersionException ex)
    {
        // Handle concurrency conflict
        throw new ConcurrencyException($"Concurrency conflict when saving {aggregate.GetType().Name} with ID {aggregate.Id}", ex);
    }
}
```

2. **Implement Retry Logic with Conflict Resolution**

```csharp
public void HandleWithRetry(TransferFunds command)
{
    const int maxRetries = 3;
    int retryCount = 0;
    
    while (true)
    {
        try
        {
            var account = _repository.GetById<Account>(command.AccountId);
            account.Withdraw(command.Amount, command);
            _repository.Save(account);
            break; // Success, exit the loop
        }
        catch (ConcurrencyException)
        {
            if (++retryCount >= maxRetries)
                throw new MaxRetriesExceededException($"Failed to process command after {maxRetries} attempts");
                
            // Optional: Add exponential backoff
            Thread.Sleep(100 * (int)Math.Pow(2, retryCount));
        }
    }
}
```

3. **Implement Command Merging for Conflict Resolution**

```csharp
public void HandleWithMerge(DepositFunds command)
{
    try
    {
        var account = _repository.GetById<Account>(command.AccountId);
        account.Deposit(command.Amount, command);
        _repository.Save(account);
    }
    catch (ConcurrencyException)
    {
        // Reload the latest state
        var account = _repository.GetById<Account>(command.AccountId, true); // Force reload
        
        // Check if it's safe to apply the command to the updated state
        if (account.IsClosed)
        {
            throw new AccountClosedException("Cannot deposit to a closed account");
        }
        
        // Apply command to the updated state
        account.Deposit(command.Amount, command);
        _repository.Save(account);
    }
}
```

### Best Practices

1. **Design Commands to be Idempotent** - Commands should be safely reapplied without causing duplicate effects.
2. **Use Command IDs for Deduplication** - Assign unique IDs to commands to detect and prevent duplicate processing.
3. **Consider Domain-Specific Conflict Resolution** - Implement business rules for merging conflicting changes.
4. **Log Concurrency Conflicts** - Monitor and analyze patterns of concurrency conflicts to optimize your system.
5. **Use Appropriate Retry Strategies** - Implement exponential backoff or circuit breakers for retry logic.

### Problem: Concurrent Updates to the Same Aggregate

When multiple processes attempt to update the same aggregate simultaneously, concurrency conflicts can occur.

### Solution: Optimistic Concurrency Control

Reactive Domain uses optimistic concurrency control through the `ExpectedVersion` property:

```csharp
try
{
    var account = repository.GetById<Account>(accountId);
    account.Withdraw(100);
    repository.Save(account);
}
catch (AggregateVersionException ex)
{
    // Handle concurrency conflict
    Console.WriteLine($"Concurrency conflict: Expected version {ex.ExpectedVersion}, but was {ex.ActualVersion}");
}
```

### Retry Pattern

Implement a retry pattern for handling concurrency conflicts:

```csharp
public void WithdrawWithRetry(Guid accountId, decimal amount, int maxRetries = 3)
{
    int retries = 0;
    while (true)
    {
        try
        {
            var account = repository.GetById<Account>(accountId);
            account.Withdraw(amount);
            repository.Save(account);
            return; // Success
        }
        catch (AggregateVersionException)
        {
            if (++retries > maxRetries)
                throw; // Give up after max retries
                
            // Exponential backoff
            Thread.Sleep(100 * (int)Math.Pow(2, retries - 1));
        }
    }
}
```

### Command Queueing

For high-contention aggregates, consider using a command queue to serialize updates:

```csharp
public class CommandQueue<TAggregate> where TAggregate : AggregateRoot
{
    private readonly IRepository _repository;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new ConcurrentDictionary<Guid, SemaphoreSlim>();
    
    public CommandQueue(IRepository repository)
    {
        _repository = repository;
    }
    
    public async Task Execute(Guid aggregateId, Action<TAggregate> command)
    {
        var lockObj = _locks.GetOrAdd(aggregateId, _ => new SemaphoreSlim(1, 1));
        
        await lockObj.WaitAsync();
        try
        {
            var aggregate = _repository.GetById<TAggregate>(aggregateId);
            command(aggregate);
            _repository.Save(aggregate);
        }
        finally
        {
            lockObj.Release();
        }
    }
}
```

### Best Practices

1. **Minimize Aggregate Size**: Keep aggregates small to reduce contention.
2. **Use Retry Logic**: Implement retry logic for handling concurrency conflicts.
3. **Consider Command Queueing**: For high-contention aggregates, use command queueing.
4. **Monitor Conflicts**: Track and monitor concurrency conflicts to identify hotspots.
5. **Design for Concurrency**: Design your domain model to minimize contention.

## Debugging Event-Sourced Systems

### Problem: Difficult to Debug Event-Sourced Systems

Event-sourced systems can be challenging to debug due to their asynchronous and event-driven nature.

### Solution: Event Store Exploration

Use EventStoreDB's admin UI to explore events:

1. Navigate to `http://localhost:2113` (default EventStoreDB admin UI)
2. Log in with default credentials (admin/changeit)
3. Browse streams and inspect events

### Event Logging

Add comprehensive logging for events:

```csharp
public class LoggingEventStore : IEventStore
{
    private readonly IEventStore _innerEventStore;
    private readonly ILogger _logger;
    
    public LoggingEventStore(IEventStore innerEventStore, ILogger logger)
    {
        _innerEventStore = innerEventStore;
        _logger = logger;
    }
    
    public void AppendToStream(string streamName, long expectedVersion, IEnumerable<IEventData> events)
    {
        _logger.LogInformation($"Appending to stream {streamName} at version {expectedVersion}");
        foreach (var @event in events)
        {
            _logger.LogInformation($"Event: {JsonConvert.SerializeObject(@event)}");
        }
        
        _innerEventStore.AppendToStream(streamName, expectedVersion, events);
    }
    
    // Implement other methods similarly
}
```

### Event Replay Tool

Create a tool to replay events for debugging:

```csharp
public class EventReplayTool
{
    private readonly IStreamStoreConnection _connection;
    private readonly IEventSerializer _serializer;
    
    public EventReplayTool(IStreamStoreConnection connection, IEventSerializer serializer)
    {
        _connection = connection;
        _serializer = serializer;
    }
    
    public void ReplayEvents<TAggregate>(Guid aggregateId) where TAggregate : AggregateRoot, new()
    {
        var streamName = $"aggregate-{typeof(TAggregate).Name.ToLower()}-{aggregateId}";
        var events = ReadAllEvents(streamName);
        
        Console.WriteLine($"Replaying {events.Count} events for {typeof(TAggregate).Name} {aggregateId}");
        
        var aggregate = new TAggregate();
        foreach (var @event in events)
        {
            Console.WriteLine($"Applying event: {@event.GetType().Name}");
            try
            {
                aggregate.RestoreFromEvents(new[] { @event });
                Console.WriteLine("Event applied successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying event: {ex.Message}");
            }
        }
    }
    
    private List<object> ReadAllEvents(string streamName)
    {
        var events = new List<object>();
        var sliceStart = 0L;
        const int sliceCount = 100;
        
        while (true)
        {
            var slice = _connection.ReadStreamForward(streamName, sliceStart, sliceCount);
            
            if (slice is StreamNotFoundSlice || slice is StreamDeletedSlice)
                break;
                
            foreach (var @event in slice.Events)
            {
                events.Add(_serializer.Deserialize(@event));
            }
            
            if (slice.IsEndOfStream)
                break;
                
            sliceStart = slice.NextEventNumber;
        }
        
        return events;
    }
}
```

### Best Practices

1. **Comprehensive Logging**: Log all events and commands with correlation IDs.
2. **Use Event Store UI**: Leverage EventStoreDB's admin UI for exploring events.
3. **Create Debugging Tools**: Build tools for replaying and inspecting events.
4. **Monitor Event Flows**: Use distributed tracing to monitor event flows.
5. **Test with Real Events**: Use real event sequences in tests for debugging.

## Performance Issues and Optimization

### Problem: Slow Aggregate Loading

Loading aggregates with many events can be slow.

### Solution: Snapshots

Implement snapshots to optimize loading:

```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    private decimal _balance;
    
    // ... existing code ...
    
    public void RestoreFromSnapshot(object snapshot)
    {
        var accountSnapshot = (AccountSnapshot)snapshot;
        _balance = accountSnapshot.Balance;
        ExpectedVersion = accountSnapshot.Version;
    }
    
    public object TakeSnapshot()
    {
        return new AccountSnapshot
        {
            Balance = _balance,
            Version = ExpectedVersion
        };
    }
}

public class AccountSnapshot
{
    public decimal Balance { get; set; }
    public long Version { get; set; }
}
```

### Snapshot Repository

Create a repository that uses snapshots:

```csharp
public class SnapshotRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly ISnapshotStore _snapshotStore;
    
    public SnapshotRepository(IRepository innerRepository, ISnapshotStore snapshotStore)
    {
        _innerRepository = innerRepository;
        _snapshotStore = snapshotStore;
    }
    
    public TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource
    {
        if (typeof(ISnapshotSource).IsAssignableFrom(typeof(TAggregate)))
        {
            // Try to load from snapshot
            var snapshot = _snapshotStore.GetSnapshot(id, typeof(TAggregate));
            if (snapshot != null)
            {
                var aggregate = (TAggregate)Activator.CreateInstance(typeof(TAggregate), id);
                ((ISnapshotSource)aggregate).RestoreFromSnapshot(snapshot.Data);
                
                // Load events after the snapshot
                _innerRepository.Update(ref aggregate, version);
                return aggregate;
            }
        }
        
        // Fall back to loading all events
        return _innerRepository.GetById<TAggregate>(id, version);
    }
    
    public void Save(IEventSource aggregate)
    {
        _innerRepository.Save(aggregate);
        
        // Take a snapshot if the aggregate supports it
        if (aggregate is ISnapshotSource snapshotSource)
        {
            var snapshot = snapshotSource.TakeSnapshot();
            _snapshotStore.SaveSnapshot(aggregate.Id, aggregate.GetType(), snapshot, aggregate.ExpectedVersion);
        }
    }
    
    // Implement other methods
}
```

### Problem: Slow Queries

Read models can become slow as they grow.

### Solution: Optimized Read Models

Create specialized read models for specific query patterns:

```csharp
public class AccountBalanceReadModel
{
    private readonly Dictionary<Guid, decimal> _balances = new Dictionary<Guid, decimal>();
    
    public void Handle(AmountDeposited @event)
    {
        if (!_balances.TryGetValue(@event.AccountId, out var balance))
            balance = 0;
            
        _balances[@event.AccountId] = balance + @event.Amount;
    }
    
    public void Handle(AmountWithdrawn @event)
    {
        if (!_balances.TryGetValue(@event.AccountId, out var balance))
            throw new InvalidOperationException("Account not found");
            
        _balances[@event.AccountId] = balance - @event.Amount;
    }
    
    public decimal GetBalance(Guid accountId)
    {
        return _balances.TryGetValue(accountId, out var balance) ? balance : 0;
    }
}
```

### Best Practices

1. **Use Snapshots**: Implement snapshots for aggregates with many events.
2. **Optimize Read Models**: Create specialized read models for specific query patterns.
3. **Batch Operations**: Batch operations where possible to reduce overhead.
4. **Use Caching**: Cache aggregates and read models to reduce database load.
5. **Monitor Performance**: Use performance monitoring tools to identify bottlenecks.

## Integration Challenges

### Problem: Integrating with External Systems

Integrating event-sourced systems with external systems can be challenging.

### Solution: Integration Events

Use integration events to communicate with external systems:

```csharp
public class AccountIntegrationEventHandler : IEventHandler<AmountDeposited>, IEventHandler<AmountWithdrawn>
{
    private readonly IExternalSystem _externalSystem;
    
    public AccountIntegrationEventHandler(IExternalSystem externalSystem)
    {
        _externalSystem = externalSystem;
    }
    
    public void Handle(AmountDeposited @event)
    {
        _externalSystem.NotifyDeposit(@event.AccountId, @event.Amount);
    }
    
    public void Handle(AmountWithdrawn @event)
    {
        _externalSystem.NotifyWithdrawal(@event.AccountId, @event.Amount);
    }
}
```

### Outbox Pattern

Use the outbox pattern to ensure reliable integration:

```csharp
public class OutboxRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly IOutbox _outbox;
    
    public OutboxRepository(IRepository innerRepository, IOutbox outbox)
    {
        _innerRepository = innerRepository;
        _outbox = outbox;
    }
    
    public void Save(IEventSource aggregate)
    {
        using (var transaction = new TransactionScope())
        {
            _innerRepository.Save(aggregate);
            
            // Add integration events to the outbox
            foreach (var @event in aggregate.TakeEvents())
            {
                _outbox.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    AggregateId = aggregate.Id,
                    AggregateType = aggregate.GetType().Name,
                    EventType = @event.GetType().Name,
                    EventData = JsonConvert.SerializeObject(@event),
                    CreatedAt = DateTime.UtcNow
                });
            }
            
            transaction.Complete();
        }
    }
    
    // Implement other methods
}
```

### Best Practices

1. **Use Integration Events**: Create specific events for integration purposes.
2. **Implement the Outbox Pattern**: Use the outbox pattern for reliable integration.
3. **Decouple Systems**: Keep external systems decoupled from your domain model.
4. **Use Message Brokers**: Consider using message brokers for asynchronous integration.
5. **Implement Idempotence**: Ensure that integration operations are idempotent.

## Testing Strategies and Common Issues

### Problem: Testing Event-Sourced Systems

Testing event-sourced systems requires different approaches than traditional systems.

### Solution: Event-Based Testing

Test aggregates by verifying the events they produce:

```csharp
[Fact]
public void CanDepositMoney()
{
    // Arrange
    var accountId = Guid.NewGuid();
    var account = new Account(accountId);
    
    // Act
    account.Deposit(100);
    
    // Assert
    var events = ((IEventSource)account).TakeEvents();
    Assert.Single(events);
    var @event = Assert.IsType<AmountDeposited>(events[0]);
    Assert.Equal(accountId, @event.AccountId);
    Assert.Equal(100, @event.Amount);
}
```

### Given-When-Then Testing

Use the Given-When-Then pattern for testing:

```csharp
[Fact]
public void GivenAnAccount_WhenDepositing_ThenBalanceIncreases()
{
    // Given
    var accountId = Guid.NewGuid();
    var account = new Account(accountId);
    
    // When
    account.Deposit(100);
    
    // Then
    Assert.Equal(100, account.GetBalance());
}
```

### Testing with Mock Event Store

Use a mock event store for testing:

```csharp
[Fact]
public void CanSaveAndLoadAggregate()
{
    // Arrange
    var accountId = Guid.NewGuid();
    var mockStore = new MockStreamStoreConnection("testRepo");
    mockStore.Connect();
    var repository = new StreamStoreRepository(new PrefixedCamelCaseStreamNameBuilder(), mockStore, new JsonMessageSerializer());
    
    // Act
    var account = new Account(accountId);
    account.Deposit(100);
    repository.Save(account);
    
    var loadedAccount = repository.GetById<Account>(accountId);
    
    // Assert
    Assert.Equal(100, loadedAccount.GetBalance());
}
```

### Best Practices

1. **Test Events, Not State**: Focus on testing the events produced by aggregates.
2. **Use Given-When-Then**: Structure tests using the Given-When-Then pattern.
3. **Mock Event Store**: Use a mock event store for testing.
4. **Test Projections**: Test projections by feeding them events.
5. **Test Integration**: Test integration with external systems using mocks.

## Deployment Considerations

### Problem: Deploying Event-Sourced Systems

Deploying event-sourced systems requires careful consideration of event schema evolution.

### Solution: Backward Compatibility

Ensure backward compatibility when deploying new versions:

1. **Never Delete Events**: Once events are in production, never delete them.
2. **Add Fields, Don't Remove**: When modifying events, add new fields rather than removing existing ones.
3. **Provide Defaults**: When adding new fields, provide sensible defaults.

### Versioned Deployments

Use versioned deployments to manage event schema evolution:

1. **Deploy Read Side First**: Deploy read-side changes before write-side changes.
2. **Use Feature Flags**: Use feature flags to control the activation of new features.
3. **Implement Backward Compatibility**: Ensure that new code can process old events.

### Best Practices

1. **Automate Deployments**: Use automated deployment pipelines.
2. **Test Migrations**: Test event schema migrations before deployment.
3. **Monitor Deployments**: Monitor deployments for errors and performance issues.
4. **Have Rollback Plans**: Prepare rollback plans for failed deployments.
5. **Document Changes**: Document all changes to event schemas.

## Monitoring and Observability

### Problem: Monitoring Event-Sourced Systems

Event-sourced systems can be challenging to monitor due to their asynchronous nature.

### Solution: Comprehensive Monitoring

Implement comprehensive monitoring:

```csharp
public class MonitoredRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly IMetrics _metrics;
    
    public MonitoredRepository(IRepository innerRepository, IMetrics metrics)
    {
        _innerRepository = innerRepository;
        _metrics = metrics;
    }
    
    public TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource
    {
        var timer = _metrics.StartTimer($"repository.getbyid.{typeof(TAggregate).Name}");
        try
        {
            return _innerRepository.GetById<TAggregate>(id, version);
        }
        catch (Exception ex)
        {
            _metrics.IncrementCounter($"repository.getbyid.{typeof(TAggregate).Name}.error");
            throw;
        }
        finally
        {
            timer.Stop();
        }
    }
    
    // Implement other methods similarly
}
```

### Distributed Tracing

Use distributed tracing to monitor event flows:

```csharp
public class TracingEventHandler<TEvent> : IEventHandler<TEvent>
{
    private readonly IEventHandler<TEvent> _innerHandler;
    private readonly ITracer _tracer;
    
    public TracingEventHandler(IEventHandler<TEvent> innerHandler, ITracer tracer)
    {
        _innerHandler = innerHandler;
        _tracer = tracer;
    }
    
    public void Handle(TEvent @event)
    {
        using (var scope = _tracer.StartActiveSpan($"handle.{typeof(TEvent).Name}"))
        {
            if (@event is ICorrelatedMessage correlatedMessage)
            {
                scope.SetTag("correlation_id", correlatedMessage.CorrelationId.ToString());
                scope.SetTag("causation_id", correlatedMessage.CausationId.ToString());
            }
            
            _innerHandler.Handle(@event);
        }
    }
}
```

### Best Practices

1. **Monitor Event Flows**: Use distributed tracing to monitor event flows.
2. **Track Metrics**: Track key metrics like event processing rates and latencies.
3. **Set Up Alerts**: Set up alerts for abnormal conditions.
4. **Log Key Events**: Log key events and errors with correlation IDs.
5. **Implement Health Checks**: Implement health checks for all components.

[↑ Back to Top](#troubleshooting-guide) | [← Back to Table of Contents](README.md)
