# ICheckpointStore Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICheckpointStore` interface defines the contract for components that store and retrieve checkpoints for event processors. Checkpoints are crucial in event-sourced systems as they track the position of event processors in event streams, enabling reliable and resumable event processing.

In Reactive Domain, checkpoint stores provide the persistence mechanism that allows event processors to resume from where they left off after restarts or failures, ensuring that events are processed exactly once even in the face of system outages.

## Checkpoints in Event Processing

In event-sourced systems, checkpoints serve several critical purposes:

1. **Resumable Processing**: Enabling event processors to resume from their last position after restarts
2. **Exactly-Once Processing**: Ensuring events are processed exactly once, even after failures
3. **Progress Tracking**: Monitoring the progress of event processors through event streams
4. **Gap Detection**: Identifying gaps in event processing
5. **Performance Optimization**: Avoiding reprocessing of events that have already been handled

Checkpoint stores are essential infrastructure components that support the reliability and resilience of event-driven systems.

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface ICheckpointStore
{
    Task<long> GetCheckpointAsync(string processorName);
    Task StoreCheckpointAsync(string processorName, long position);
}
```

## Methods

### GetCheckpointAsync

Retrieves the checkpoint position for a specified event processor.

```csharp
Task<long> GetCheckpointAsync(string processorName);
```

**Parameters**:
- `processorName` (`System.String`): The name of the event processor.

**Returns**: `System.Threading.Tasks.Task<System.Int64>` - A task that represents the asynchronous operation. The task result contains the checkpoint position for the specified processor, or -1 if no checkpoint exists.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `processorName` is `null` or empty.
- `System.InvalidOperationException`: Thrown when the checkpoint store is not accessible.

**Remarks**: This method retrieves the checkpoint position for a specified event processor. The position represents the sequence number of the last event that was successfully processed by the event processor. If no checkpoint exists for the specified processor, the method returns -1, indicating that the processor should start from the beginning of the event stream.

**Example**:
```csharp
// Get the checkpoint for a read model updater
var checkpointStore = new SqlCheckpointStore(connectionString);
var position = await checkpointStore.GetCheckpointAsync("AccountReadModelUpdater");

// Use the position to determine where to start processing
var startPosition = position >= 0 ? position + 1 : 0;
Console.WriteLine($"Starting event processing from position {startPosition}");
```

### StoreCheckpointAsync

Stores the checkpoint position for a specified event processor.

```csharp
Task StoreCheckpointAsync(string processorName, long position);
```

**Parameters**:
- `processorName` (`System.String`): The name of the event processor.
- `position` (`System.Int64`): The checkpoint position to store.

**Returns**: `System.Threading.Tasks.Task` - A task that represents the asynchronous operation.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `processorName` is `null` or empty.
- `System.ArgumentOutOfRangeException`: Thrown when `position` is less than -1.
- `System.InvalidOperationException`: Thrown when the checkpoint store is not accessible.

**Remarks**: This method stores the checkpoint position for a specified event processor. The position represents the sequence number of the last event that was successfully processed by the event processor. This position will be used to resume processing from the correct point after a restart or failure.

**Example**:
```csharp
// Store the checkpoint after processing a batch of events
var checkpointStore = new SqlCheckpointStore(connectionString);
await checkpointStore.StoreCheckpointAsync("AccountReadModelUpdater", 1000);
Console.WriteLine("Checkpoint stored at position 1000");
```

## Usage

The `ICheckpointStore` interface is typically used in conjunction with event processors to enable reliable and resumable event processing. Here's a comprehensive example of using a checkpoint store:

### Basic Checkpoint Store Usage

```csharp
// Create a checkpoint store
var checkpointStore = new SqlCheckpointStore(connectionString);

// Create an event processor with the checkpoint store
var eventProcessor = new EventStoreProcessor(
    eventStoreConnection,
    "AccountReadModelUpdater",
    checkpointStore);

// Subscribe handlers
eventProcessor.Subscribe<AccountCreated>(HandleAccountCreated);
eventProcessor.Subscribe<FundsDeposited>(HandleFundsDeposited);

// Start the processor - it will automatically use the checkpoint
await eventProcessor.StartAsync();

// Process events...

// When shutting down, the processor will automatically store the checkpoint
await eventProcessor.StopAsync();
```

### Custom Checkpoint Store Implementation

```csharp
public class SqlCheckpointStore : ICheckpointStore
{
    private readonly string _connectionString;
    
    public SqlCheckpointStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }
    
    public async Task<long> GetCheckpointAsync(string processorName)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Position FROM Checkpoints WHERE ProcessorName = @ProcessorName";
                command.Parameters.AddWithValue("@ProcessorName", processorName);
                
                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt64(result) : -1;
            }
        }
    }
    
    public async Task StoreCheckpointAsync(string processorName, long position)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        if (position < -1)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be greater than or equal to -1");
            
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
                            IF EXISTS (SELECT 1 FROM Checkpoints WHERE ProcessorName = @ProcessorName)
                                UPDATE Checkpoints SET Position = @Position WHERE ProcessorName = @ProcessorName
                            ELSE
                                INSERT INTO Checkpoints (ProcessorName, Position) VALUES (@ProcessorName, @Position)";
                        command.Parameters.AddWithValue("@ProcessorName", processorName);
                        command.Parameters.AddWithValue("@Position", position);
                        
                        await command.ExecuteNonQueryAsync();
                    }
                    
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
```

### Integration with Event Processor

```csharp
public class ReadModelUpdater : IDisposable
{
    private readonly IEventProcessor _eventProcessor;
    private readonly ICheckpointStore _checkpointStore;
    private readonly IReadModelRepository<AccountReadModel> _readModelRepository;
    private readonly ILogger<ReadModelUpdater> _logger;
    
    public ReadModelUpdater(
        IEventStoreConnection eventStoreConnection,
        ICheckpointStore checkpointStore,
        IReadModelRepository<AccountReadModel> readModelRepository,
        ILogger<ReadModelUpdater> logger)
    {
        _checkpointStore = checkpointStore;
        _readModelRepository = readModelRepository;
        _logger = logger;
        
        // Create an event processor with the checkpoint store
        _eventProcessor = new EventStoreProcessor(
            eventStoreConnection,
            "AccountReadModelUpdater",
            _checkpointStore);
            
        // Register handlers
        _eventProcessor.Subscribe<AccountCreated>(HandleAccountCreated);
        _eventProcessor.Subscribe<FundsDeposited>(HandleFundsDeposited);
        _eventProcessor.Subscribe<FundsWithdrawn>(HandleFundsWithdrawn);
        _eventProcessor.Subscribe<AccountClosed>(HandleAccountClosed);
    }
    
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting read model updater");
        
        // Get the current checkpoint
        var checkpoint = await _checkpointStore.GetCheckpointAsync("AccountReadModelUpdater");
        _logger.LogInformation("Starting from checkpoint: {Checkpoint}", checkpoint);
        
        // Start the processor
        await _eventProcessor.StartAsync();
    }
    
    private void HandleAccountCreated(AccountCreated @event)
    {
        _logger.LogInformation("Processing AccountCreated event: {@AccountId}", @event.AccountId);
        
        // Update the read model
        var readModel = new AccountReadModel
        {
            Id = @event.AccountId,
            AccountNumber = @event.AccountNumber,
            Balance = @event.InitialDeposit,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
        
        // Save the read model
        _readModelRepository.Save(readModel);
    }
    
    // Additional handlers...
    
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping read model updater");
        
        // Stop the processor - it will automatically store the checkpoint
        await _eventProcessor.StopAsync();
    }
    
    public void Dispose()
    {
        _eventProcessor?.Dispose();
    }
}
```

## Best Practices

1. **Transactional Updates**: Store checkpoints in the same transaction as any read model updates to ensure consistency
2. **Regular Checkpointing**: Store checkpoints at regular intervals to balance performance and resilience
3. **Idempotent Processing**: Design event handlers to be idempotent to handle duplicate events safely
4. **Error Handling**: Implement proper error handling to prevent checkpoint advancement on failures
5. **Monitoring**: Monitor checkpoint progress to detect stalled processors
6. **Backup and Recovery**: Implement backup and recovery procedures for checkpoint stores
7. **Performance Tuning**: Balance checkpoint frequency with performance requirements
8. **Versioning**: Consider versioning checkpoints to handle schema evolution
9. **Security**: Secure checkpoint stores to prevent unauthorized access
10. **Testing**: Test checkpoint recovery scenarios to ensure proper resumption after failures

## Common Pitfalls

1. **Checkpoint Races**: Race conditions when multiple instances of the same processor try to update checkpoints
2. **Checkpoint Lag**: Excessive lag between event processing and checkpoint updates
3. **Checkpoint Corruption**: Corruption of checkpoint data leading to event reprocessing or skipping
4. **Checkpoint Frequency**: Checkpointing too frequently (performance impact) or too infrequently (potential for duplicate processing)
5. **Transaction Boundaries**: Not aligning checkpoint updates with read model update transactions
6. **Recovery Testing**: Inadequate testing of recovery scenarios
7. **Monitoring Gaps**: Not monitoring checkpoint progress to detect stalled processors

## Advanced Scenarios

### Distributed Checkpoint Store

Implementing a distributed checkpoint store for high availability:

```csharp
public class DistributedCheckpointStore : ICheckpointStore
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<DistributedCheckpointStore> _logger;
    
    public DistributedCheckpointStore(IDistributedCache distributedCache, ILogger<DistributedCheckpointStore> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }
    
    public async Task<long> GetCheckpointAsync(string processorName)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        try
        {
            var cacheKey = $"checkpoint:{processorName}";
            var cachedValue = await _distributedCache.GetStringAsync(cacheKey);
            
            if (string.IsNullOrEmpty(cachedValue))
                return -1;
                
            if (long.TryParse(cachedValue, out var position))
                return position;
                
            _logger.LogWarning("Invalid checkpoint value for processor {ProcessorName}: {Value}", processorName, cachedValue);
            return -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkpoint for processor {ProcessorName}", processorName);
            throw;
        }
    }
    
    public async Task StoreCheckpointAsync(string processorName, long position)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        if (position < -1)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be greater than or equal to -1");
            
        try
        {
            var cacheKey = $"checkpoint:{processorName}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            };
            
            await _distributedCache.SetStringAsync(cacheKey, position.ToString(), cacheOptions);
            _logger.LogDebug("Stored checkpoint for processor {ProcessorName} at position {Position}", processorName, position);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing checkpoint for processor {ProcessorName} at position {Position}", processorName, position);
            throw;
        }
    }
}
```

### Checkpoint Store with Optimistic Concurrency

Implementing optimistic concurrency control for checkpoint updates:

```csharp
public class ConcurrentCheckpointStore : ICheckpointStore
{
    private readonly string _connectionString;
    
    public ConcurrentCheckpointStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }
    
    public async Task<long> GetCheckpointAsync(string processorName)
    {
        // Implementation similar to SqlCheckpointStore
    }
    
    public async Task StoreCheckpointAsync(string processorName, long position)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        if (position < -1)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be greater than or equal to -1");
            
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            // Get the current checkpoint with a version
            long currentPosition;
            int version;
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Position, Version FROM Checkpoints WHERE ProcessorName = @ProcessorName";
                command.Parameters.AddWithValue("@ProcessorName", processorName);
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        currentPosition = reader.GetInt64(0);
                        version = reader.GetInt32(1);
                    }
                    else
                    {
                        currentPosition = -1;
                        version = 0;
                    }
                }
            }
            
            // Only update if the new position is greater than the current position
            if (position > currentPosition)
            {
                using (var command = connection.CreateCommand())
                {
                    if (version == 0)
                    {
                        // Insert new checkpoint
                        command.CommandText = @"
                            INSERT INTO Checkpoints (ProcessorName, Position, Version)
                            VALUES (@ProcessorName, @Position, 1)";
                        command.Parameters.AddWithValue("@ProcessorName", processorName);
                        command.Parameters.AddWithValue("@Position", position);
                    }
                    else
                    {
                        // Update existing checkpoint with optimistic concurrency
                        command.CommandText = @"
                            UPDATE Checkpoints
                            SET Position = @Position, Version = Version + 1
                            WHERE ProcessorName = @ProcessorName AND Version = @Version";
                        command.Parameters.AddWithValue("@ProcessorName", processorName);
                        command.Parameters.AddWithValue("@Position", position);
                        command.Parameters.AddWithValue("@Version", version);
                    }
                    
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    
                    if (rowsAffected == 0 && version > 0)
                    {
                        throw new ConcurrencyException($"Concurrency conflict when updating checkpoint for processor {processorName}");
                    }
                }
            }
        }
    }
    
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message) : base(message) { }
    }
}
```

### Multi-Tenant Checkpoint Store

Implementing a checkpoint store that supports multiple tenants:

```csharp
public class MultiTenantCheckpointStore : ICheckpointStore
{
    private readonly string _connectionString;
    private readonly ITenantProvider _tenantProvider;
    
    public MultiTenantCheckpointStore(string connectionString, ITenantProvider tenantProvider)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    }
    
    public async Task<long> GetCheckpointAsync(string processorName)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var fullProcessorName = $"{tenantId}:{processorName}";
        
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Position FROM Checkpoints WHERE ProcessorName = @ProcessorName AND TenantId = @TenantId";
                command.Parameters.AddWithValue("@ProcessorName", processorName);
                command.Parameters.AddWithValue("@TenantId", tenantId);
                
                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt64(result) : -1;
            }
        }
    }
    
    public async Task StoreCheckpointAsync(string processorName, long position)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentNullException(nameof(processorName));
            
        if (position < -1)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be greater than or equal to -1");
            
        var tenantId = _tenantProvider.GetCurrentTenantId();
        
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
                            IF EXISTS (SELECT 1 FROM Checkpoints WHERE ProcessorName = @ProcessorName AND TenantId = @TenantId)
                                UPDATE Checkpoints SET Position = @Position WHERE ProcessorName = @ProcessorName AND TenantId = @TenantId
                            ELSE
                                INSERT INTO Checkpoints (ProcessorName, TenantId, Position) VALUES (@ProcessorName, @TenantId, @Position)";
                        command.Parameters.AddWithValue("@ProcessorName", processorName);
                        command.Parameters.AddWithValue("@TenantId", tenantId);
                        command.Parameters.AddWithValue("@Position", position);
                        
                        await command.ExecuteNonQueryAsync();
                    }
                    
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
```

## Related Components

- [IEventProcessor](ievent-processor.md): Interface for components that process events
- [Event](event.md): Base class for events in Reactive Domain
- [IEvent](ievent.md): Interface for events in Reactive Domain
- [IEventBus](ievent-bus.md): Interface for publishing events
- [IEventHandler](ievent-handler.md): Interface for event handlers
- [ReadModelBase](read-model-base.md): Base class for read models
- [IReadModelRepository](iread-model-repository.md): Interface for read model repositories

---

**Navigation**:
- [← Previous: IEventProcessor](./ievent-processor.md)
- [↑ Back to Top](#icheckpointstore-interface)
- [→ Next: ReadModelBase](./read-model-base.md)
