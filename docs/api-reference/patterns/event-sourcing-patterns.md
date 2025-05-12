# Event Sourcing Patterns

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

This document outlines the key patterns and best practices for implementing event sourcing in Reactive Domain applications. Event sourcing is a powerful architectural pattern where all changes to application state are stored as a sequence of events, providing a complete audit trail and enabling advanced capabilities like temporal queries and event replay.

## Table of Contents

1. [Event Replay and State Reconstruction](#event-replay-and-state-reconstruction)
2. [Snapshot Implementation](#snapshot-implementation)
3. [Versioning Strategies for Events](#versioning-strategies-for-events)
4. [Stream Management](#stream-management)
5. [Event Serialization](#event-serialization)
6. [Best Practices](#best-practices)
7. [Common Pitfalls](#common-pitfalls)

## Event Replay and State Reconstruction

Event replay is the process of reconstructing the state of an entity by applying all historical events in sequence. This is a fundamental concept in event sourcing and is used both when loading entities and when rebuilding projections.

### Basic Event Replay

The most straightforward approach to event replay is to apply all events in sequence:

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    private string _customerName;
    
    public Account(Guid id) : base(id)
    {
        // Initialize default state
        _isActive = false;
        _balance = 0;
    }
    
    // IEventSource implementation
    public override void RestoreFromEvents(IEnumerable<object> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));
            
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    // Event handlers
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

### Optimized Event Replay with Snapshots

For entities with long event histories, snapshots can significantly improve loading performance:

```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    private string _customerName;
    
    public long SnapshotVersion { get; set; }
    
    public Account(Guid id) : base(id)
    {
        _isActive = false;
        _balance = 0;
    }
    
    // Snapshot methods
    public object CreateSnapshot()
    {
        return new AccountSnapshot
        {
            Balance = _balance,
            IsActive = _isActive,
            AccountNumber = _accountNumber,
            CustomerName = _customerName
        };
    }
    
    public void RestoreFromSnapshot(object snapshot)
    {
        if (snapshot is AccountSnapshot accountSnapshot)
        {
            _balance = accountSnapshot.Balance;
            _isActive = accountSnapshot.IsActive;
            _accountNumber = accountSnapshot.AccountNumber;
            _customerName = accountSnapshot.CustomerName;
        }
        else
        {
            throw new ArgumentException($"Expected AccountSnapshot but got {snapshot.GetType().Name}");
        }
    }
}

// Repository implementation
public T GetById<T>(Guid id) where T : IEventSource
{
    // Try to get the latest snapshot
    var snapshot = _snapshotStore.GetLatestSnapshot(id);
    
    // Create a new instance of the aggregate
    var aggregate = Activator.CreateInstance(typeof(T), id) as T;
    
    if (snapshot != null && aggregate is ISnapshotSource snapshotSource)
    {
        // Restore from snapshot
        snapshotSource.RestoreFromSnapshot(snapshot.State);
        snapshotSource.SnapshotVersion = snapshot.Version;
        
        // Get events after the snapshot
        var events = _eventStore.GetEventsAfterVersion(
            id.ToString(), 
            snapshot.Version);
            
        // Apply events after the snapshot
        aggregate.RestoreFromEvents(events);
    }
    else
    {
        // No snapshot available, load all events
        var events = _eventStore.GetEvents(id.ToString());
        aggregate.RestoreFromEvents(events);
    }
    
    return aggregate;
}
```

### Parallel Event Replay for Projections

For read model projections that process large numbers of events, parallel processing can improve performance:

```csharp
public class ParallelProjectionEngine
{
    private readonly IEventStore _eventStore;
    private readonly IReadModelRepository<AccountSummary> _repository;
    private readonly int _batchSize;
    private readonly int _maxDegreeOfParallelism;
    
    public ParallelProjectionEngine(
        IEventStore eventStore,
        IReadModelRepository<AccountSummary> repository,
        int batchSize = 1000,
        int maxDegreeOfParallelism = 4)
    {
        _eventStore = eventStore;
        _repository = repository;
        _batchSize = batchSize;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }
    
    public async Task RebuildProjectionAsync(CancellationToken cancellationToken = default)
    {
        // Clear existing projection data
        await _repository.ClearAllAsync(cancellationToken);
        
        // Get all event streams (one per aggregate)
        var streams = await _eventStore.GetAllStreamIdsAsync(cancellationToken);
        
        // Process streams in parallel
        await Parallel.ForEachAsync(
            streams,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (streamId, ct) =>
            {
                // Process each stream
                await ProcessStreamAsync(streamId, ct);
            });
    }
    
    private async Task ProcessStreamAsync(string streamId, CancellationToken cancellationToken)
    {
        long position = 0;
        bool hasMoreEvents = true;
        
        while (hasMoreEvents && !cancellationToken.IsCancellationRequested)
        {
            // Get events in batches
            var events = await _eventStore.GetEventsAsync(
                streamId, 
                position, 
                _batchSize, 
                cancellationToken);
                
            if (events.Count == 0)
            {
                hasMoreEvents = false;
                continue;
            }
            
            // Process events
            foreach (var @event in events)
            {
                await ProcessEventAsync(@event, cancellationToken);
                position = @event.Position + 1;
            }
        }
    }
    
    private async Task ProcessEventAsync(EventData eventData, CancellationToken cancellationToken)
    {
        // Deserialize and process the event based on its type
        switch (eventData.EventType)
        {
            case "AccountCreated":
                var accountCreated = _serializer.Deserialize<AccountCreated>(eventData.Data);
                await HandleAccountCreatedAsync(accountCreated, cancellationToken);
                break;
                
            case "FundsDeposited":
                var fundsDeposited = _serializer.Deserialize<FundsDeposited>(eventData.Data);
                await HandleFundsDepositedAsync(fundsDeposited, cancellationToken);
                break;
                
            case "FundsWithdrawn":
                var fundsWithdrawn = _serializer.Deserialize<FundsWithdrawn>(eventData.Data);
                await HandleFundsWithdrawnAsync(fundsWithdrawn, cancellationToken);
                break;
        }
    }
    
    private async Task HandleAccountCreatedAsync(
        AccountCreated @event, 
        CancellationToken cancellationToken)
    {
        var accountSummary = new AccountSummary(@event.AccountId)
        {
            AccountNumber = @event.AccountNumber,
            CustomerName = @event.CustomerName,
            Balance = 0,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
        
        await _repository.SaveAsync(accountSummary, cancellationToken);
    }
    
    private async Task HandleFundsDepositedAsync(
        FundsDeposited @event, 
        CancellationToken cancellationToken)
    {
        var accountSummary = await _repository.GetByIdAsync(@event.AccountId, cancellationToken);
        if (accountSummary != null)
        {
            accountSummary.Balance += @event.Amount;
            accountSummary.LastUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(accountSummary, cancellationToken);
        }
    }
    
    private async Task HandleFundsWithdrawnAsync(
        FundsWithdrawn @event, 
        CancellationToken cancellationToken)
    {
        var accountSummary = await _repository.GetByIdAsync(@event.AccountId, cancellationToken);
        if (accountSummary != null)
        {
            accountSummary.Balance -= @event.Amount;
            accountSummary.LastUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(accountSummary, cancellationToken);
        }
    }
}
```

### Temporal Queries

Event sourcing enables temporal queries, allowing you to determine the state of an entity at any point in time:

```csharp
public T GetByIdAtVersion<T>(Guid id, long version) where T : IEventSource
{
    // Create a new instance of the aggregate
    var aggregate = Activator.CreateInstance(typeof(T), id) as T;
    
    // Get events up to the specified version
    var events = _eventStore.GetEventsUpToVersion(id.ToString(), version);
    
    // Apply events
    aggregate.RestoreFromEvents(events);
    
    return aggregate;
}

public T GetByIdAtTimestamp<T>(Guid id, DateTime timestamp) where T : IEventSource
{
    // Create a new instance of the aggregate
    var aggregate = Activator.CreateInstance(typeof(T), id) as T;
    
    // Get events up to the specified timestamp
    var events = _eventStore.GetEventsUpToTimestamp(id.ToString(), timestamp);
    
    // Apply events
    aggregate.RestoreFromEvents(events);
    
    return aggregate;
}
```

## Snapshot Implementation

Snapshots are point-in-time captures of an entity's state that can be used to optimize loading performance for entities with long event histories. Instead of replaying all events from the beginning, the entity can be restored from the most recent snapshot and then only apply events that occurred after the snapshot was taken.

### Snapshot Interface

In Reactive Domain, snapshots are implemented through the `ISnapshotSource` interface:

```csharp
public interface ISnapshotSource : IEventSource
{
    object CreateSnapshot();
    void RestoreFromSnapshot(object snapshot);
    long SnapshotVersion { get; set; }
}
```

### Snapshot Creation Strategy

Snapshots should be created at strategic points to balance storage and performance. A common approach is to create snapshots based on the number of events since the last snapshot:

```csharp
public void Save<T>(T aggregate) where T : IEventSource
{
    // Get new events
    var newEvents = aggregate.TakeEvents();
    
    if (newEvents.Length > 0)
    {
        // Save events
        _connection.AppendToStream(
            aggregate.Id.ToString(),
            aggregate.ExpectedVersion,
            newEvents);
            
        // Update expected version
        aggregate.ExpectedVersion += newEvents.Length;
        
        // Check if a snapshot should be created
        if (aggregate is ISnapshotSource snapshotSource)
        {
            // Calculate the number of events since the last snapshot
            var eventsSinceSnapshot = aggregate.ExpectedVersion - snapshotSource.SnapshotVersion;
            
            // Create a snapshot if there are enough new events
            if (eventsSinceSnapshot >= _snapshotFrequency)
            {
                var snapshot = snapshotSource.CreateSnapshot();
                _snapshotStore.SaveSnapshot(
                    aggregate.Id,
                    aggregate.ExpectedVersion,
                    snapshot);
                    
                snapshotSource.SnapshotVersion = aggregate.ExpectedVersion;
            }
        }
    }
}
```

### Snapshot Storage

Snapshots need to be stored in a way that allows efficient retrieval by aggregate ID and version. Here's an example of a snapshot store implementation using a document database:

```csharp
public class DocumentDbSnapshotStore : ISnapshotStore
{
    private readonly IMongoCollection<SnapshotDocument> _snapshots;
    private readonly ILogger<DocumentDbSnapshotStore> _logger;
    
    public DocumentDbSnapshotStore(
        IMongoDatabase database,
        ILogger<DocumentDbSnapshotStore> logger)
    {
        _snapshots = database.GetCollection<SnapshotDocument>("snapshots");
        _logger = logger;
        
        // Create indexes for efficient retrieval
        var indexKeysDefinition = Builders<SnapshotDocument>.IndexKeys
            .Ascending(s => s.AggregateId)
            .Descending(s => s.Version);
            
        _snapshots.Indexes.CreateOne(new CreateIndexModel<SnapshotDocument>(indexKeysDefinition));
    }
    
    public async Task<SnapshotInfo> GetLatestSnapshotAsync(
        Guid aggregateId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateId, aggregateId);
            var sort = Builders<SnapshotDocument>.Sort.Descending(s => s.Version);
            
            var snapshot = await _snapshots
                .Find(filter)
                .Sort(sort)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (snapshot == null)
                return null;
                
            return new SnapshotInfo
            {
                AggregateId = snapshot.AggregateId,
                Version = snapshot.Version,
                Timestamp = snapshot.Timestamp,
                State = snapshot.State
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving latest snapshot for aggregate {AggregateId}",
                aggregateId);
            throw;
        }
    }
    
    public async Task SaveSnapshotAsync(
        Guid aggregateId, 
        long version, 
        object state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = new SnapshotDocument
            {
                AggregateId = aggregateId,
                Version = version,
                Timestamp = DateTime.UtcNow,
                State = state
            };
            
            await _snapshots.InsertOneAsync(document, cancellationToken: cancellationToken);
            
            _logger.LogInformation(
                "Saved snapshot for aggregate {AggregateId} at version {Version}",
                aggregateId,
                version);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error saving snapshot for aggregate {AggregateId} at version {Version}",
                aggregateId,
                version);
            throw;
        }
    }
    
    // Optional: Cleanup old snapshots
    public async Task CleanupSnapshotsAsync(
        Guid aggregateId, 
        int keepLatest = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateId, aggregateId);
            var sort = Builders<SnapshotDocument>.Sort.Descending(s => s.Version);
            
            // Get the versions of the snapshots to keep
            var versionsToKeep = await _snapshots
                .Find(filter)
                .Sort(sort)
                .Limit(keepLatest)
                .Project(s => s.Version)
                .ToListAsync(cancellationToken);
                
            if (versionsToKeep.Count < keepLatest)
                return; // Not enough snapshots to clean up
                
            // Delete older snapshots
            var deleteFilter = Builders<SnapshotDocument>.Filter.And(
                Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateId, aggregateId),
                Builders<SnapshotDocument>.Filter.Lt(s => s.Version, versionsToKeep.Min()));
                
            var result = await _snapshots.DeleteManyAsync(deleteFilter, cancellationToken);
            
            _logger.LogInformation(
                "Cleaned up {DeletedCount} old snapshots for aggregate {AggregateId}",
                result.DeletedCount,
                aggregateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error cleaning up snapshots for aggregate {AggregateId}",
                aggregateId);
            throw;
        }
    }
}

public class SnapshotDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    public Guid AggregateId { get; set; }
    
    public long Version { get; set; }
    
    public DateTime Timestamp { get; set; }
    
    public object State { get; set; }
}
```

### Snapshot Serialization

Snapshots need to be serializable for storage. It's important to design snapshot classes with serialization in mind:

```csharp
[Serializable]
public class AccountSnapshot
{
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public List<TransactionSummary> RecentTransactions { get; set; } = new List<TransactionSummary>();
}

[Serializable]
public class TransactionSummary
{
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum TransactionType
{
    Deposit,
    Withdrawal
}
```

## Versioning Strategies for Events

As systems evolve, event schemas may need to change. Proper versioning strategies ensure that older events can still be processed by newer versions of the system.

### Event Versioning Approaches

#### 1. Explicit Versioning

Include a version number in the event class:

```csharp
public class AccountCreated : Event
{
    public int Version { get; } = 2;
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public string CustomerName { get; }
    public string Email { get; } // Added in version 2
    
    public AccountCreated(Guid accountId, string accountNumber, string customerName, string email = null)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        Email = email; // Optional in version 2, not present in version 1
    }
}
```

#### 2. Event Upcasting

Transform older event versions to newer versions during deserialization:

```csharp
public class EventUpcastingSerializer : IEventSerializer
{
    private readonly IEventSerializer _innerSerializer;
    private readonly Dictionary<Type, List<Func<object, object>>> _upcasters = 
        new Dictionary<Type, List<Func<object, object>>>();
    
    public EventUpcastingSerializer(IEventSerializer innerSerializer)
    {
        _innerSerializer = innerSerializer;
    }
    
    public void RegisterUpcaster<TEvent>(Func<TEvent, object> upcaster)
    {
        var eventType = typeof(TEvent);
        
        if (!_upcasters.ContainsKey(eventType))
        {
            _upcasters[eventType] = new List<Func<object, object>>();
        }
        
        _upcasters[eventType].Add(e => upcaster((TEvent)e));
    }
    
    public byte[] Serialize<T>(T @event)
    {
        return _innerSerializer.Serialize(@event);
    }
    
    public T Deserialize<T>(byte[] data)
    {
        var deserialized = _innerSerializer.Deserialize<T>(data);
        
        if (_upcasters.TryGetValue(typeof(T), out var upcasters))
        {
            var result = deserialized;
            
            foreach (var upcaster in upcasters)
            {
                result = (T)upcaster(result);
            }
            
            return (T)result;
        }
        
        return deserialized;
    }
}

// Usage
var serializer = new EventUpcastingSerializer(new JsonEventSerializer());

// Register upcasters
serializer.RegisterUpcaster<AccountCreatedV1>(oldEvent => 
    new AccountCreatedV2
    {
        AccountId = oldEvent.AccountId,
        AccountNumber = oldEvent.AccountNumber,
        CustomerName = oldEvent.CustomerName,
        Email = null // Default value for new field
    });
```

#### 3. Polymorphic Event Handlers

Implement event handlers that can handle multiple versions of an event:

```csharp
public class Account : AggregateRoot
{
    private void Apply(object @event)
    {
        switch (@event)
        {
            case AccountCreatedV1 e:
                ApplyAccountCreated(e.AccountId, e.AccountNumber, e.CustomerName, null);
                break;
                
            case AccountCreatedV2 e:
                ApplyAccountCreated(e.AccountId, e.AccountNumber, e.CustomerName, e.Email);
                break;
                
            // Other event handlers...
        }
    }
    
    private void ApplyAccountCreated(Guid accountId, string accountNumber, string customerName, string email)
    {
        _accountNumber = accountNumber;
        _customerName = customerName;
        _email = email ?? "unknown@example.com"; // Default value if email is null
        _isActive = true;
    }
}
```

### Handling Breaking Changes

For more significant changes, consider these strategies:

#### 1. Side-by-Side Versioning

Maintain multiple versions of event handlers:

```csharp
public class AccountProjection : 
    IEventHandler<AccountCreatedV1>,
    IEventHandler<AccountCreatedV2>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountProjection(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreatedV1 @event)
    {
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(
            @event.AccountNumber,
            @event.CustomerName,
            "unknown@example.com", // Default value for missing field
            0,
            true);
            
        _repository.Save(accountSummary);
    }
    
    public void Handle(AccountCreatedV2 @event)
    {
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(
            @event.AccountNumber,
            @event.CustomerName,
            @event.Email,
            0,
            true);
            
        _repository.Save(accountSummary);
    }
}
```

#### 2. Event Transformation

Transform events during replay:

```csharp
public class EventTransformationPipeline
{
    private readonly List<IEventTransformer> _transformers = new List<IEventTransformer>();
    
    public void RegisterTransformer(IEventTransformer transformer)
    {
        _transformers.Add(transformer);
    }
    
    public IEnumerable<object> TransformEvents(IEnumerable<object> events)
    {
        var result = events;
        
        foreach (var transformer in _transformers)
        {
            result = transformer.Transform(result);
        }
        
        return result;
    }
}

public interface IEventTransformer
{
    IEnumerable<object> Transform(IEnumerable<object> events);
}

public class AccountCreatedTransformer : IEventTransformer
{
    public IEnumerable<object> Transform(IEnumerable<object> events)
    {
        foreach (var @event in events)
        {
            if (@event is AccountCreatedV1 oldEvent)
            {
                yield return new AccountCreatedV2
                {
                    AccountId = oldEvent.AccountId,
                    AccountNumber = oldEvent.AccountNumber,
                    CustomerName = oldEvent.CustomerName,
                    Email = "unknown@example.com" // Default value
                };
            }
            else
            {
                yield return @event;
            }
        }
    }
}
```

## Stream Management

Event streams are the core data structure in event sourcing. Proper stream management is essential for scalability and performance.

### Stream Naming Conventions

Consistent stream naming conventions make it easier to organize and query events:

```csharp
public static class StreamNamingConventions
{
    // Individual aggregate streams
    public static string GetAggregateStreamName<T>(Guid aggregateId) where T : IEventSource
    {
        return $"{typeof(T).Name}-{aggregateId}";
    }
    
    // Category streams (all events of a specific type)
    public static string GetCategoryStreamName<T>() where T : IEventSource
    {
        return $"$ce-{typeof(T).Name}";
    }
    
    // All events stream
    public static string GetAllEventsStreamName()
    {
        return "$all";
    }
}
```

### Stream Partitioning

For high-throughput systems, partitioning streams can improve scalability:

```csharp
public static class PartitionedStreamNamingConventions
{
    public static string GetPartitionedStreamName<T>(Guid aggregateId, int partitionCount) where T : IEventSource
    {
        // Use the least significant bits of the GUID to determine the partition
        var partition = Math.Abs(aggregateId.GetHashCode()) % partitionCount;
        return $"{typeof(T).Name}-P{partition}-{aggregateId}";
    }
    
    public static string GetPartitionCategoryStreamName<T>(int partition) where T : IEventSource
    {
        return $"$ce-{typeof(T).Name}-P{partition}";
    }
}
```

### Stream Metadata

Stream metadata can store additional information about a stream:

```csharp
public class StreamMetadata
{
    public string AggregateType { get; set; }
    public int MaxCount { get; set; }
    public TimeSpan MaxAge { get; set; }
    public bool Truncated { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}

public class EventStoreExtensions
{
    public static async Task SetStreamMetadataAsync(
        this IEventStoreConnection connection,
        string streamName,
        StreamMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var metadataJson = JsonConvert.SerializeObject(metadata);
        var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
        
        await connection.SetStreamMetadataAsync(
            streamName,
            ExpectedVersion.Any,
            metadataBytes,
            cancellationToken);
    }
    
    public static async Task<StreamMetadata> GetStreamMetadataAsync(
        this IEventStoreConnection connection,
        string streamName,
        CancellationToken cancellationToken = default)
    {
        var metadata = await connection.GetStreamMetadataAsync(streamName, cancellationToken);
        
        if (metadata.StreamMetadata.Length == 0)
            return new StreamMetadata
            {
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };
            
        var metadataJson = Encoding.UTF8.GetString(metadata.StreamMetadata);
        return JsonConvert.DeserializeObject<StreamMetadata>(metadataJson);
    }
}
```

### Stream Lifecycle Management

Managing the lifecycle of streams is important for long-lived systems:

```csharp
public class StreamLifecycleManager
{
    private readonly IEventStoreConnection _connection;
    private readonly ILogger<StreamLifecycleManager> _logger;
    
    public StreamLifecycleManager(
        IEventStoreConnection connection,
        ILogger<StreamLifecycleManager> logger)
    {
        _connection = connection;
        _logger = logger;
    }
    
    public async Task SetStreamTruncationPolicyAsync(
        string streamName,
        int? maxCount = null,
        TimeSpan? maxAge = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _connection.GetStreamMetadataAsync(streamName, cancellationToken);
            var streamMetadata = new StreamMetadata();
            
            if (metadata.StreamMetadata.Length > 0)
            {
                var metadataJson = Encoding.UTF8.GetString(metadata.StreamMetadata);
                streamMetadata = JsonConvert.DeserializeObject<StreamMetadata>(metadataJson);
            }
            else
            {
                streamMetadata.CreatedUtc = DateTime.UtcNow;
            }
            
            if (maxCount.HasValue)
                streamMetadata.MaxCount = maxCount.Value;
                
            if (maxAge.HasValue)
                streamMetadata.MaxAge = maxAge.Value;
                
            streamMetadata.LastModifiedUtc = DateTime.UtcNow;
            
            await _connection.SetStreamMetadataAsync(
                streamName,
                metadata.MetastreamVersion,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(streamMetadata)),
                cancellationToken);
                
            _logger.LogInformation(
                "Set truncation policy for stream {StreamName}: MaxCount={MaxCount}, MaxAge={MaxAge}",
                streamName,
                maxCount,
                maxAge);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error setting truncation policy for stream {StreamName}",
                streamName);
            throw;
        }
    }
    
    public async Task ArchiveStreamAsync(
        string streamName,
        string archivePrefix = "Archive-",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read all events from the source stream
            var events = await _connection.ReadStreamEventsForwardAsync(
                streamName,
                0,
                1000,
                false,
                cancellationToken);
                
            if (events.Status == SliceReadStatus.StreamNotFound)
            {
                _logger.LogWarning(
                    "Stream {StreamName} not found for archiving",
                    streamName);
                return;
            }
            
            // Create archive stream name
            var archiveStreamName = $"{archivePrefix}{streamName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            
            // Write events to archive stream
            var eventsToWrite = events.Events.Select(e => new EventData(
                e.Event.EventId,
                e.Event.EventType,
                e.Event.IsJson,
                e.Event.Data,
                e.Event.Metadata));
                
            await _connection.AppendToStreamAsync(
                archiveStreamName,
                ExpectedVersion.NoStream,
                eventsToWrite,
                cancellationToken);
                
            // Mark original stream as archived in metadata
            var metadata = await _connection.GetStreamMetadataAsync(streamName, cancellationToken);
            var streamMetadata = new StreamMetadata();
            
            if (metadata.StreamMetadata.Length > 0)
            {
                var metadataJson = Encoding.UTF8.GetString(metadata.StreamMetadata);
                streamMetadata = JsonConvert.DeserializeObject<StreamMetadata>(metadataJson);
            }
            
            streamMetadata.Truncated = true;
            streamMetadata.LastModifiedUtc = DateTime.UtcNow;
            
            await _connection.SetStreamMetadataAsync(
                streamName,
                metadata.MetastreamVersion,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(streamMetadata)),
                cancellationToken);
                
            _logger.LogInformation(
                "Archived stream {StreamName} to {ArchiveStreamName}",
                streamName,
                archiveStreamName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error archiving stream {StreamName}",
                streamName);
            throw;
        }
    }
}
```

## Event Serialization

Event serialization is a critical aspect of event sourcing, as it determines how events are stored and retrieved. The serialization strategy must ensure that events can be correctly deserialized even as the system evolves over time.

### JSON Serialization

JSON is a common format for event serialization due to its readability and flexibility:

```csharp
public class JsonEventSerializer : IEventSerializer
{
    private readonly JsonSerializerSettings _serializerSettings;
    
    public JsonEventSerializer()
    {
        _serializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }
    
    public byte[] Serialize<T>(T @event)
    {
        var json = JsonConvert.SerializeObject(@event, _serializerSettings);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public T Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json, _serializerSettings);
    }
    
    public object Deserialize(byte[] data, Type type)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject(json, type, _serializerSettings);
    }
}
```

### Event Type Resolution

To correctly deserialize events, the system needs to know the event type. There are several approaches to this:

#### 1. Type Information in the Event Data

```csharp
public class EventData
{
    public Guid EventId { get; }
    public string EventType { get; }
    public byte[] Data { get; }
    public byte[] Metadata { get; }
    
    public EventData(Guid eventId, string eventType, byte[] data, byte[] metadata)
    {
        EventId = eventId;
        EventType = eventType;
        Data = data;
        Metadata = metadata;
    }
}

public class EventTypeResolver
{
    private readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>();
    
    public void RegisterType<T>(string typeName = null)
    {
        var type = typeof(T);
        var name = typeName ?? type.Name;
        
        _typeMap[name] = type;
    }
    
    public Type ResolveType(string typeName)
    {
        if (_typeMap.TryGetValue(typeName, out var type))
            return type;
            
        throw new InvalidOperationException($"Unknown event type: {typeName}");
    }
}
```

#### 2. Type Information in the Serialized Data

```csharp
public class TypeAwareJsonEventSerializer : IEventSerializer
{
    private readonly JsonSerializerSettings _serializerSettings;
    
    public TypeAwareJsonEventSerializer()
    {
        _serializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
    }
    
    public byte[] Serialize<T>(T @event)
    {
        var json = JsonConvert.SerializeObject(@event, _serializerSettings);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public T Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json, _serializerSettings);
    }
    
    public object Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject(json, _serializerSettings);
    }
}
```

### Event Metadata

Metadata provides additional context for events without affecting the event data itself:

```csharp
public class EventMetadata
{
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
    public string UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; }
    public int EventVersion { get; set; }
    public Dictionary<string, string> AdditionalData { get; set; } = new Dictionary<string, string>();
}

public class EventWithMetadata<T>
{
    public T Event { get; }
    public EventMetadata Metadata { get; }
    
    public EventWithMetadata(T @event, EventMetadata metadata)
    {
        Event = @event;
        Metadata = metadata;
    }
}

public class MetadataSerializer
{
    private readonly JsonSerializerSettings _serializerSettings;
    
    public MetadataSerializer()
    {
        _serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
    }
    
    public byte[] SerializeMetadata(EventMetadata metadata)
    {
        var json = JsonConvert.SerializeObject(metadata, _serializerSettings);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public EventMetadata DeserializeMetadata(byte[] data)
    {
        if (data == null || data.Length == 0)
            return new EventMetadata();
            
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<EventMetadata>(json, _serializerSettings);
    }
}
```

### Serialization Compatibility

To ensure backward and forward compatibility, consider these strategies:

#### 1. Schema Evolution

```csharp
// Version 1
public class AccountCreatedV1
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
}

// Version 2 - Added Email field
public class AccountCreatedV2
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public string Email { get; set; }
}

// Version 3 - Added Phone field, removed AccountNumber
public class AccountCreatedV3
{
    public Guid AccountId { get; set; }
    public string CustomerName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
}
```

#### 2. Serialization Versioning

```csharp
public class VersionedJsonEventSerializer : IEventSerializer
{
    private readonly Dictionary<Type, Dictionary<int, Func<object, object>>> _upcasters = 
        new Dictionary<Type, Dictionary<int, Func<object, object>>>();
    private readonly Dictionary<Type, int> _currentVersions = new Dictionary<Type, int>();
    
    public void RegisterEventType<T>(int currentVersion)
    {
        _currentVersions[typeof(T)] = currentVersion;
    }
    
    public void RegisterUpcaster<T>(int fromVersion, int toVersion, Func<object, object> upcaster)
    {
        var type = typeof(T);
        
        if (!_upcasters.ContainsKey(type))
            _upcasters[type] = new Dictionary<int, Func<object, object>>();
            
        _upcasters[type][fromVersion] = upcaster;
    }
    
    public byte[] Serialize<T>(T @event)
    {
        var type = typeof(T);
        var version = _currentVersions.TryGetValue(type, out var v) ? v : 1;
        
        var wrapper = new EventWrapper
        {
            EventType = type.AssemblyQualifiedName,
            EventVersion = version,
            EventData = JsonConvert.SerializeObject(@event)
        };
        
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(wrapper));
    }
    
    public T Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        var wrapper = JsonConvert.DeserializeObject<EventWrapper>(json);
        
        var eventType = Type.GetType(wrapper.EventType);
        var eventVersion = wrapper.EventVersion;
        var eventData = wrapper.EventData;
        
        var deserialized = JsonConvert.DeserializeObject(eventData, eventType);
        
        // Apply upcasters if needed
        if (_upcasters.TryGetValue(eventType, out var typeUpcasters))
        {
            var currentVersion = _currentVersions.TryGetValue(eventType, out var v) ? v : 1;
            
            for (int version = eventVersion; version < currentVersion; version++)
            {
                if (typeUpcasters.TryGetValue(version, out var upcaster))
                {
                    deserialized = upcaster(deserialized);
                }
            }
        }
        
        return (T)deserialized;
    }
}

public class EventWrapper
{
    public string EventType { get; set; }
    public int EventVersion { get; set; }
    public string EventData { get; set; }
}

## Best Practices

### 1. Design Events Carefully

Events should be designed to capture business intent and be as self-contained as possible:

```csharp
// Good: Captures business intent clearly
public class AccountCreated
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public string CustomerName { get; }
    public decimal InitialBalance { get; }
    public DateTime CreatedAt { get; }
    
    public AccountCreated(Guid accountId, string accountNumber, string customerName, decimal initialBalance, DateTime createdAt)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        InitialBalance = initialBalance;
        CreatedAt = createdAt;
    }
}

// Bad: Missing important context
public class AccountCreated
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
}
```

### 2. Immutable Events

Events should be immutable to maintain the integrity of the event log:

```csharp
// Good: Immutable event with readonly properties
public class FundsDeposited
{
    public Guid AccountId { get; }
    public decimal Amount { get; }
    public string Description { get; }
    public DateTime DepositedAt { get; }
    
    public FundsDeposited(Guid accountId, decimal amount, string description, DateTime depositedAt)
    {
        AccountId = accountId;
        Amount = amount;
        Description = description;
        DepositedAt = depositedAt;
    }
}

// Bad: Mutable event
public class FundsDeposited
{
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public DateTime DepositedAt { get; set; }
}
```

### 3. Consistent Naming

Use consistent naming conventions for events, typically past tense to indicate something that has happened:

```csharp
// Good: Past tense naming
public class AccountCreated { /* ... */ }
public class FundsDeposited { /* ... */ }
public class CustomerAddressChanged { /* ... */ }
public class AccountClosed { /* ... */ }

// Bad: Inconsistent naming
public class CreateAccount { /* ... */ }
public class DepositingFunds { /* ... */ }
public class ChangeAddress { /* ... */ }
public class AccountWasClosed { /* ... */ }
```

### 4. Use Event Metadata

Capture important metadata with each event:

```csharp
public interface IEventStore
{
    Task<IEnumerable<EventData>> GetEventsAsync(Guid aggregateId, long fromVersion = 0);
    Task SaveEventsAsync(Guid aggregateId, IEnumerable<object> events, long expectedVersion, EventMetadata metadata);
}

public class EventStoreRepository<T> where T : AggregateRoot, new()
{
    private readonly IEventStore _eventStore;
    private readonly IEventPublisher _eventPublisher;
    
    public EventStoreRepository(IEventStore eventStore, IEventPublisher eventPublisher)
    {
        _eventStore = eventStore;
        _eventPublisher = eventPublisher;
    }
    
    public async Task<T> GetByIdAsync(Guid id)
    {
        var events = await _eventStore.GetEventsAsync(id);
        
        if (!events.Any())
            return null;
            
        var aggregate = new T();
        aggregate.LoadFromHistory(events.Select(e => e.EventData));
        
        return aggregate;
    }
    
    public async Task SaveAsync(T aggregate, EventMetadata metadata)
    {
        var uncommittedEvents = aggregate.GetUncommittedEvents().ToList();
        
        if (!uncommittedEvents.Any())
            return;
            
        await _eventStore.SaveEventsAsync(
            aggregate.Id,
            uncommittedEvents,
            aggregate.Version,
            metadata);
            
        foreach (var @event in uncommittedEvents)
        {
            await _eventPublisher.PublishAsync(@event);
        }
        
        aggregate.ClearUncommittedEvents();
    }
}
```

### 5. Optimize Read Models

Build specialized read models for different query needs:

```csharp
public class AccountSummaryReadModel : ReadModelBase,
    IHandle<AccountCreated>,
    IHandle<FundsDeposited>,
    IHandle<FundsWithdrawn>
{
    public Guid AccountId { get; private set; }
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public DateTime LastActivityDate { get; private set; }
    
    public void Handle(AccountCreated @event)
    {
        AccountId = @event.AccountId;
        AccountNumber = @event.AccountNumber;
        CustomerName = @event.CustomerName;
        CurrentBalance = @event.InitialBalance;
        LastActivityDate = @event.CreatedAt;
    }
    
    public void Handle(FundsDeposited @event)
    {
        CurrentBalance += @event.Amount;
        LastActivityDate = @event.DepositedAt;
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        CurrentBalance -= @event.Amount;
        LastActivityDate = @event.WithdrawnAt;
    }
}

public class AccountTransactionReadModel : ReadModelBase,
    IHandle<AccountCreated>,
    IHandle<FundsDeposited>,
    IHandle<FundsWithdrawn>
{
    private readonly List<TransactionItem> _transactions = new List<TransactionItem>();
    
    public Guid AccountId { get; private set; }
    public string AccountNumber { get; private set; }
    public IReadOnlyList<TransactionItem> Transactions => _transactions.AsReadOnly();
    
    public void Handle(AccountCreated @event)
    {
        AccountId = @event.AccountId;
        AccountNumber = @event.AccountNumber;
        
        if (@event.InitialBalance > 0)
        {
            _transactions.Add(new TransactionItem
            {
                TransactionId = Guid.NewGuid(),
                Amount = @event.InitialBalance,
                Balance = @event.InitialBalance,
                Description = "Initial deposit",
                TransactionDate = @event.CreatedAt,
                Type = TransactionType.Credit
            });
        }
    }
    
    public void Handle(FundsDeposited @event)
    {
        var currentBalance = _transactions.Any() 
            ? _transactions.Last().Balance 
            : 0;
            
        var newBalance = currentBalance + @event.Amount;
        
        _transactions.Add(new TransactionItem
        {
            TransactionId = Guid.NewGuid(),
            Amount = @event.Amount,
            Balance = newBalance,
            Description = @event.Description,
            TransactionDate = @event.DepositedAt,
            Type = TransactionType.Credit
        });
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        var currentBalance = _transactions.Any() 
            ? _transactions.Last().Balance 
            : 0;
            
        var newBalance = currentBalance - @event.Amount;
        
        _transactions.Add(new TransactionItem
        {
            TransactionId = Guid.NewGuid(),
            Amount = @event.Amount,
            Balance = newBalance,
            Description = @event.Description,
            TransactionDate = @event.WithdrawnAt,
            Type = TransactionType.Debit
        });
    }
}

public class TransactionItem
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Description { get; set; }
    public DateTime TransactionDate { get; set; }
    public TransactionType Type { get; set; }
}

public enum TransactionType
{
    Credit,
    Debit
}
```

### 6. Use Correlation and Causation IDs

Track event relationships with correlation and causation IDs:

```csharp
public class EventMetadata
{
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    public string UserId { get; }
    public DateTime Timestamp { get; }
    
    public EventMetadata(Guid correlationId, Guid causationId, string userId)
    {
        CorrelationId = correlationId;
        CausationId = causationId;
        UserId = userId;
        Timestamp = DateTime.UtcNow;
    }
}

public class CommandHandler
{
    private readonly IRepository<Account> _repository;
    private readonly IEventBus _eventBus;
    
    public CommandHandler(IRepository<Account> repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public async Task Handle(DepositFunds command, IMessageContext context)
    {
        var account = await _repository.GetByIdAsync(command.AccountId);
        
        if (account == null)
            throw new AccountNotFoundException(command.AccountId);
            
        account.Deposit(command.Amount, command.Description);
        
        var metadata = new EventMetadata(
            correlationId: context.CorrelationId,
            causationId: context.MessageId,
            userId: context.UserId);
            
        await _repository.SaveAsync(account, metadata);
    }
}
```

### 7. Implement Idempotent Event Handlers

Ensure event handlers can safely process the same event multiple times:

```csharp
public class AccountTransactionProjection : IProjection,
    IHandle<FundsDeposited>,
    IHandle<FundsWithdrawn>
{
    private readonly ITransactionRepository _repository;
    
    public AccountTransactionProjection(ITransactionRepository repository)
    {
        _repository = repository;
    }
    
    public async Task Handle(FundsDeposited @event, EventMetadata metadata)
    {
        // Check if we've already processed this event
        if (await _repository.HasProcessedEventAsync(metadata.EventId))
            return;
            
        var transaction = new TransactionRecord
        {
            TransactionId = Guid.NewGuid(),
            AccountId = @event.AccountId,
            Amount = @event.Amount,
            Description = @event.Description,
            TransactionDate = @event.DepositedAt,
            Type = TransactionType.Credit,
            EventId = metadata.EventId
        };
        
        await _repository.SaveTransactionAsync(transaction);
        await _repository.MarkEventAsProcessedAsync(metadata.EventId);
    }
    
    public async Task Handle(FundsWithdrawn @event, EventMetadata metadata)
    {
        // Check if we've already processed this event
        if (await _repository.HasProcessedEventAsync(metadata.EventId))
            return;
            
        var transaction = new TransactionRecord
        {
            TransactionId = Guid.NewGuid(),
            AccountId = @event.AccountId,
            Amount = @event.Amount,
            Description = @event.Description,
            TransactionDate = @event.WithdrawnAt,
            Type = TransactionType.Debit,
            EventId = metadata.EventId
        };
        
        await _repository.SaveTransactionAsync(transaction);
        await _repository.MarkEventAsProcessedAsync(metadata.EventId);
    }
}

public interface ITransactionRepository
{
    Task<bool> HasProcessedEventAsync(Guid eventId);
    Task MarkEventAsProcessedAsync(Guid eventId);
    Task SaveTransactionAsync(TransactionRecord transaction);
}
```

## Common Pitfalls

### 1. Mutable Events

One of the most common mistakes in event sourcing is creating mutable events, which can lead to data inconsistency and loss of audit trail integrity.

```csharp
// Problematic: Mutable event
public class AccountCreated
{
    public Guid AccountId { get; set; }  // Setter allows modification after creation
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
}

// Solution: Immutable event
public class AccountCreated
{
    public Guid AccountId { get; }  // Read-only property
    public string AccountNumber { get; }
    public string CustomerName { get; }
    
    public AccountCreated(Guid accountId, string accountNumber, string customerName)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
}
```

### 2. Large Events

Creating excessively large events can impact performance and scalability.

```csharp
// Problematic: Large event with unnecessary data
public class OrderPlaced
{
    public Guid OrderId { get; }
    public Customer Customer { get; }  // Entire customer object
    public List<OrderItem> Items { get; }  // All order items with full details
    public ShippingDetails ShippingDetails { get; }
    public BillingDetails BillingDetails { get; }
    public List<Discount> AppliedDiscounts { get; }
    public byte[] CustomerProfilePicture { get; }  // Unnecessary binary data
    // ... many more properties
}

// Solution: Focused event with essential data
public class OrderPlaced
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }  // Reference instead of embedding
    public List<OrderItemSummary> Items { get; }  // Simplified summary
    public decimal TotalAmount { get; }
    public string ShippingAddress { get; }
    public DateTime OrderDate { get; }
}

// Additional events for specific aspects if needed
public class OrderDiscountsApplied
{
    public Guid OrderId { get; }
    public List<AppliedDiscount> Discounts { get; }
    public decimal TotalDiscountAmount { get; }
}
```

### 3. Missing Version Control

Not implementing proper version control for events can make it difficult to evolve your system over time.

```csharp
// Problematic: No versioning strategy
public class CustomerAddressChanged
{
    public Guid CustomerId { get; }
    public string Address { get; }  // What if we need to split into multiple fields later?
}

// Solution: Explicit versioning
public class CustomerAddressChangedV1
{
    public Guid CustomerId { get; }
    public string Address { get; }
}

public class CustomerAddressChangedV2
{
    public Guid CustomerId { get; }
    public string StreetAddress { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }
}

// Event upcaster
public class CustomerAddressChangedUpcaster
{
    public CustomerAddressChangedV2 Upcast(CustomerAddressChangedV1 oldEvent)
    {
        // Parse the old address format and extract components
        var addressParts = ParseAddress(oldEvent.Address);
        
        return new CustomerAddressChangedV2
        {
            CustomerId = oldEvent.CustomerId,
            StreetAddress = addressParts.StreetAddress,
            City = addressParts.City,
            State = addressParts.State,
            PostalCode = addressParts.PostalCode,
            Country = addressParts.Country
        };
    }
    
    private AddressParts ParseAddress(string address)
    {
        // Logic to parse a single address string into components
        // ...
    }
}
```

### 4. Inefficient Event Replay

Not optimizing event replay can lead to performance issues as your event store grows.

```csharp
// Problematic: Loading all events for every query
public class AccountRepository
{
    private readonly IEventStore _eventStore;
    
    public AccountRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }
    
    public Account GetById(Guid id)
    {
        // Always load all events from the beginning
        var events = _eventStore.GetEvents(id);
        var account = new Account(id);
        account.RestoreFromEvents(events);
        return account;
    }
}

// Solution: Using snapshots
public class OptimizedAccountRepository
{
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    
    public OptimizedAccountRepository(
        IEventStore eventStore,
        ISnapshotStore snapshotStore)
    {
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
    }
    
    public Account GetById(Guid id)
    {
        // Try to get the latest snapshot
        var snapshot = _snapshotStore.GetLatestSnapshot(id);
        var account = new Account(id);
        
        if (snapshot != null)
        {
            // Restore from snapshot
            account.RestoreFromSnapshot(snapshot.State);
            
            // Only load events after the snapshot version
            var events = _eventStore.GetEventsAfterVersion(id, snapshot.Version);
            account.RestoreFromEvents(events);
        }
        else
        {
            // No snapshot, load all events
            var events = _eventStore.GetEvents(id);
            account.RestoreFromEvents(events);
        }
        
        return account;
    }
}
```

### 5. Ignoring Concurrency Control

Not implementing proper concurrency control can lead to lost events and data inconsistency.

```csharp
// Problematic: No concurrency control
public class EventStore
{
    public void SaveEvents(Guid aggregateId, IEnumerable<object> events)
    {
        // Directly save events without version check
        foreach (var @event in events)
        {
            // Save event to database
        }
    }
}

// Solution: Optimistic concurrency control
public class ConcurrencyAwareEventStore
{
    public void SaveEvents(Guid aggregateId, IEnumerable<object> events, long expectedVersion)
    {
        // Get the current version from the database
        var currentVersion = GetCurrentVersion(aggregateId);
        
        // Check if the expected version matches the current version
        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(
                $"Expected version {expectedVersion} but got {currentVersion}");
        }
        
        // Save events with incrementing versions
        long version = expectedVersion;
        foreach (var @event in events)
        {
            SaveEvent(aggregateId, @event, ++version);
        }
    }
}
```

### 6. Non-Deterministic Event Handlers

Event handlers should be deterministic to ensure consistent state reconstruction.

```csharp
// Problematic: Non-deterministic event handler
public class Account : AggregateRoot
{
    private decimal _balance;
    
    private void Apply(FundsDeposited @event)
    {
        // Using current time makes the handler non-deterministic
        var interestRate = DateTime.Now.Hour > 12 ? 0.05m : 0.03m;
        _balance += @event.Amount * (1 + interestRate);
    }
}

// Solution: Deterministic event handler
public class Account : AggregateRoot
{
    private decimal _balance;
    
    private void Apply(FundsDeposited @event)
    {
        // Use data from the event itself
        _balance += @event.Amount;
        
        // If interest calculation is needed, it should be a separate event
        // with the interest rate determined at command handling time
    }
    
    private void Apply(InterestAccrued @event)
    {
        _balance += @event.InterestAmount;
    }
}
```

### 7. Tight Coupling Between Events and Handlers

Tightly coupling events to their handlers can make it difficult to evolve your system.

```csharp
// Problematic: Tight coupling with concrete event types
public class AccountProjection
{
    public void Handle(AccountCreated @event)
    {
        // Directly coupled to AccountCreated event
    }
    
    public void Handle(FundsDeposited @event)
    {
        // Directly coupled to FundsDeposited event
    }
}

// Solution: More flexible event handling
public class AccountProjection
{
    private readonly Dictionary<Type, Action<object>> _handlers = 
        new Dictionary<Type, Action<object>>();
    
    public AccountProjection()
    {
        RegisterHandler<AccountCreated>(HandleAccountCreated);
        RegisterHandler<FundsDeposited>(HandleFundsDeposited);
        // New event types can be registered without changing the core handler logic
    }
    
    private void RegisterHandler<T>(Action<T> handler)
    {
        _handlers[typeof(T)] = @event => handler((T)@event);
    }
    
    public void Handle(object @event)
    {
        var eventType = @event.GetType();
        
        if (_handlers.TryGetValue(eventType, out var handler))
        {
            handler(@event);
        }
    }
    
    private void HandleAccountCreated(AccountCreated @event)
    {
        // Handle account creation
    }
    
    private void HandleFundsDeposited(FundsDeposited @event)
    {
        // Handle funds deposit
    }
}
```
