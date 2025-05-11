# ReactiveDomain.Persistence Component

[← Back to Components](README.md)

The ReactiveDomain.Persistence component provides the infrastructure for storing and retrieving events in an event store. It implements the repository pattern for event-sourced aggregates and provides integration with EventStoreDB.

## Key Features

- Event storage and retrieval
- Repository implementations
- Snapshot storage and retrieval
- Event serialization and deserialization
- Stream management

## Core Types

### Repositories

- **StreamStoreRepository**: Implementation of `IRepository` using EventStoreDB
- **CorrelatedStreamStoreRepository**: Implementation of `ICorrelatedRepository` using EventStoreDB

### Event Store Connections

- **StreamStoreConnection**: Implementation of `IStreamStoreConnection` for EventStoreDB
- **EventStoreConnectionFactory**: Factory for creating EventStoreDB connections

### Serialization

- **JsonEventSerializer**: JSON serializer for events
- **BinaryEventSerializer**: Binary serializer for events

### Snapshots

- **SnapshotStore**: Storage for aggregate snapshots
- **SnapshotStrategy**: Strategy for when to take snapshots

## Usage Examples

### Configuring the Event Store Connection

```csharp
// Create a connection to EventStoreDB
var connectionSettings = ConnectionSettings.Create()
    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
    .Build();
    
var connection = EventStoreConnection.Create(
    connectionSettings, 
    new Uri("tcp://localhost:1113"), 
    "MyApplication");
    
await connection.ConnectAsync();

// Create a stream store connection
var streamStoreConnection = new StreamStoreConnection(connection);
```

### Creating a Repository

```csharp
// Create a repository
var repository = new StreamStoreRepository(streamStoreConnection);

// Create a correlated repository
var correlatedRepository = new CorrelatedStreamStoreRepository(streamStoreConnection);
```

### Using Snapshots

```csharp
// Create a snapshot store
var snapshotStore = new SnapshotStore(streamStoreConnection);

// Create a repository with snapshot support
var repository = new StreamStoreRepository(
    streamStoreConnection,
    snapshotStore,
    new IntervalSnapshotStrategy(10)); // Take a snapshot every 10 events
```

## Integration with Other Components

The Persistence component integrates with:

- **ReactiveDomain.Core**: Uses the core interfaces like `IEventSource` and `IRepository`
- **ReactiveDomain.Foundation**: Provides storage for domain aggregates
- **ReactiveDomain.Messaging**: Stores and retrieves events as messages

## Configuration Options

### Event Store Connection Settings

- **ConnectionString**: Connection string for EventStoreDB
- **UserCredentials**: Username and password for authentication
- **ConnectionTimeout**: Timeout for connection attempts
- **OperationTimeout**: Timeout for operations
- **MaxRetries**: Maximum number of retries for failed operations
- **RetryDelay**: Delay between retries

### Repository Settings

- **EventCacheSize**: Size of the event cache
- **SnapshotFrequency**: Frequency of snapshots
- **MaxEventsPerRead**: Maximum number of events to read in a single operation

## Best Practices

1. **Use Correlation**: Always use `ICorrelatedRepository` when you need to track message flow
2. **Configure Snapshots**: Use snapshots for large aggregates to improve performance
3. **Handle Concurrency**: Use optimistic concurrency control with expected versions
4. **Batch Operations**: Batch multiple operations for better performance
5. **Monitor Performance**: Keep an eye on event store performance metrics

## Common Issues and Solutions

### Connection Issues

If you're having trouble connecting to EventStoreDB:

1. Check that EventStoreDB is running
2. Verify the connection string
3. Check network connectivity
4. Verify credentials

### Concurrency Exceptions

If you're getting concurrency exceptions:

1. Make sure you're using the correct expected version
2. Consider using optimistic concurrency control
3. Implement retry logic for concurrency conflicts

### Performance Issues

If you're experiencing performance issues:

1. Use snapshots for large aggregates
2. Batch operations where possible
3. Optimize event serialization
4. Consider scaling EventStoreDB

## Related Documentation

- [IRepository API Reference](../api-reference/types/irepository.md)
- [ICorrelatedRepository API Reference](../api-reference/types/icorrelated-repository.md)
- [StreamStoreRepository API Reference](../api-reference/types/stream-store-repository.md)
- [CorrelatedStreamStoreRepository API Reference](../api-reference/types/correlated-stream-store-repository.md)
- [ISnapshotSource API Reference](../api-reference/types/isnapshot-source.md)

## Navigation

**Section Navigation**:
- [← Previous: ReactiveDomain.Foundation](foundation.md)
- [↑ Parent: Component Documentation](README.md)
- [→ Next: ReactiveDomain.Transport](transport.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
