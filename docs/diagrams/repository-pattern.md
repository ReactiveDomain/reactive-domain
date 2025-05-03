# Repository Pattern Diagram

This diagram illustrates how repositories interact with aggregates and the event store in Reactive Domain.

## Repository Pattern Overview

```mermaid
classDiagram
    class IRepository {
        +GetById<T>(Guid id) T
        +TryGetById<T>(Guid id, out T aggregate) bool
        +Update<T>(ref T aggregate) void
        +Save(IEventSource aggregate) void
        +Delete(IEventSource aggregate) void
        +HardDelete(IEventSource aggregate) void
    }
    
    class IEventSource {
        +Guid Id
        +long ExpectedVersion
        +RestoreFromEvents(IEnumerable<object> events) void
        +UpdateWithEvents(IEnumerable<object> events, long expectedVersion) void
        +object[] TakeEvents()
    }
    
    class StreamStoreRepository {
        -IStreamNameBuilder _streamNameBuilder
        -IStreamStoreConnection _connection
        -IEventSerializer _serializer
        +GetById<T>(Guid id) T
        +TryGetById<T>(Guid id, out T aggregate) bool
        +Update<T>(ref T aggregate) void
        +Save(IEventSource aggregate) void
        +Delete(IEventSource aggregate) void
        +HardDelete(IEventSource aggregate) void
    }
    
    class AggregateRoot {
        +Guid Id
        +long ExpectedVersion
        #RaiseEvent(object event) void
        +RestoreFromEvents(IEnumerable<object> events) void
        +UpdateWithEvents(IEnumerable<object> events, long expectedVersion) void
        +object[] TakeEvents()
    }
    
    IRepository <|.. StreamStoreRepository : implements
    IEventSource <|.. AggregateRoot : implements
    IRepository --> IEventSource : operates on
    StreamStoreRepository --> AggregateRoot : operates on
```

## Repository Operations Flow

```mermaid
flowchart TD
    subgraph "GetById Operation"
        A1[Repository.GetById] -->|1. Generate Stream Name| B1[Stream Name Builder]
        B1 -->|2. Get Events| C1[Event Store]
        C1 -->|3. Return Events| D1[Events]
        D1 -->|4. Create Aggregate| E1[Aggregate Constructor]
        E1 -->|5. Restore From Events| F1[Aggregate]
        F1 -->|6. Return Aggregate| G1[Client]
    end
    
    subgraph "Save Operation"
        A2[Repository.Save] -->|1. Take Events| B2[Aggregate.TakeEvents]
        B2 -->|2. Return New Events| C2[Events]
        A2 -->|3. Generate Stream Name| D2[Stream Name Builder]
        D2 -->|4. Append To Stream| E2[Event Store]
        A2 -->|5. Update Expected Version| F2[Aggregate]
    end
    
    subgraph "Delete Operation"
        A3[Repository.Delete] -->|1. Create Delete Event| B3[Delete Event]
        A3 -->|2. Append Delete Event| C3[Event Store]
    end
    
    subgraph "HardDelete Operation"
        A4[Repository.HardDelete] -->|1. Generate Stream Name| B4[Stream Name Builder]
        B4 -->|2. Delete Stream| C4[Event Store]
    end
```

## Detailed Repository Operations

### GetById Operation

```mermaid
sequenceDiagram
    participant Client
    participant Repository
    participant StreamNameBuilder
    participant EventStore
    participant EventSerializer
    participant Aggregate
    
    Client->>Repository: GetById<Account>(accountId)
    Repository->>StreamNameBuilder: Build(typeof(Account), accountId)
    StreamNameBuilder-->>Repository: Return Stream Name
    Repository->>EventStore: ReadStreamEventsForward(streamName)
    EventStore-->>Repository: Return Event Data
    
    loop For Each Event Data
        Repository->>EventSerializer: Deserialize(eventData)
        EventSerializer-->>Repository: Return Event
    end
    
    Repository->>Aggregate: Create Instance (Reflection)
    Repository->>Aggregate: RestoreFromEvents(events)
    
    loop For Each Event
        Aggregate->>Aggregate: Apply(event)
        Aggregate->>Aggregate: Update ExpectedVersion
    end
    
    Repository-->>Client: Return Reconstructed Aggregate
```

### Save Operation

```mermaid
sequenceDiagram
    participant Client
    participant Repository
    participant Aggregate
    participant StreamNameBuilder
    participant EventSerializer
    participant EventStore
    
    Client->>Repository: Save(account)
    Repository->>Aggregate: TakeEvents()
    Aggregate-->>Repository: Return New Events
    
    alt Has New Events
        Repository->>StreamNameBuilder: Build(aggregate.GetType(), aggregate.Id)
        StreamNameBuilder-->>Repository: Return Stream Name
        
        loop For Each Event
            Repository->>EventSerializer: Serialize(event)
            EventSerializer-->>Repository: Return Event Data
        end
        
        Repository->>EventStore: AppendToStream(streamName, expectedVersion, eventData)
        Repository->>Aggregate: Update ExpectedVersion
    end
    
    Repository-->>Client: Return
```

## Correlated Repository Extension

```mermaid
classDiagram
    class ICorrelatedRepository {
        +GetById<T>(Guid id, ICorrelatedMessage source) T
        +Save<T>(T aggregate, ICorrelatedMessage source) void
    }
    
    class IRepository {
        +GetById<T>(Guid id) T
        +Save(IEventSource aggregate) void
    }
    
    class CorrelatedStreamStoreRepository {
        -IStreamStoreRepository _repository
        +GetById<T>(Guid id, ICorrelatedMessage source) T
        +Save<T>(T aggregate, ICorrelatedMessage source) void
    }
    
    ICorrelatedRepository <|.. CorrelatedStreamStoreRepository : implements
    ICorrelatedRepository --> IRepository : extends
    CorrelatedStreamStoreRepository --> IRepository : decorates
```

## Implementation Example

```csharp
// Creating a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("MyApp");
var connection = new StreamStoreConnection(connectionSettings, "localhost", 1113);
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, connection, serializer);

// Using the repository
try {
    // Load an aggregate
    var account = repository.GetById<Account>(accountId);
    
    // Modify the aggregate
    account.Deposit(100);
    
    // Save the aggregate
    repository.Save(account);
    
    // Delete the aggregate (soft delete)
    repository.Delete(account);
    
    // Hard delete the aggregate (permanent deletion)
    repository.HardDelete(account);
}
catch (AggregateNotFoundException ex) {
    // Handle not found
}
catch (AggregateDeletedException ex) {
    // Handle deleted
}
catch (AggregateVersionException ex) {
    // Handle concurrency conflict
}
```

## Key Concepts

### Repository Abstraction

- Repositories abstract the details of event storage and retrieval
- They provide a collection-like interface for working with aggregates
- They handle the complexities of event sourcing infrastructure

### Optimistic Concurrency

- `ExpectedVersion` is used to detect concurrent modifications
- Version conflicts throw `AggregateVersionException`
- This ensures data consistency without locking

### Aggregate Lifecycle Management

- Repositories handle the complete lifecycle of aggregates
- Creation, loading, updating, and deletion operations
- Both soft delete (logical) and hard delete (physical) options

### Event Serialization

- Events are serialized for storage and deserialized for loading
- The serialization format is abstracted through the `IEventSerializer` interface
- This allows for different serialization strategies (JSON, Protocol Buffers, etc.)
