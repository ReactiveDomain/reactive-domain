# Architecture Guide

[← Back to Table of Contents](README.md)

This guide provides a detailed overview of the Reactive Domain architecture, explaining how the various components work together to support event sourcing and CQRS in .NET applications.

## Table of Contents

- [High-Level Architecture](#high-level-architecture)
- [Design Principles and Patterns](#design-principles-and-patterns)
- [Component Interactions](#component-interactions)
- [Extension Points](#extension-points)
- [Integration Patterns](#integration-patterns)
- [Scaling and Performance Considerations](#scaling-and-performance-considerations)
- [Security Considerations](#security-considerations)

## High-Level Architecture

Reactive Domain is organized into several key components that work together to provide a complete event sourcing and CQRS solution. The following diagram illustrates the high-level architecture:

```mermaid
graph TD
    A[Client Application] --> B[ReactiveDomain.Messaging]
    B --> C[ReactiveDomain.Foundation]
    C --> D[ReactiveDomain.Core]
    C --> E[ReactiveDomain.Persistence]
    E --> F[EventStoreDB]
    B --> G[ReactiveDomain.Transport]
    H[ReactiveDomain.Testing] --> C
    I[ReactiveDomain.Tools] --> C
```

### Core Components

1. **ReactiveDomain.Core**
   - Contains the fundamental interfaces and base classes for event sourcing
   - Defines the `IEventSource` interface, which is the foundation of event-sourced entities
   - Provides base implementations for aggregates, events, and commands
   - Implements the core event sourcing patterns

2. **ReactiveDomain.Foundation**
   - Builds on Core to provide higher-level abstractions
   - Implements the `AggregateRoot` class, which is the base class for domain aggregates
   - Provides repository implementations for storing and retrieving aggregates
   - Implements correlation and causation tracking

3. **ReactiveDomain.Messaging**
   - Implements the messaging infrastructure for commands, events, and queries
   - Provides message routing and handling mechanisms
   - Implements the bus pattern for message distribution
   - Supports both synchronous and asynchronous message handling

4. **ReactiveDomain.Persistence**
   - Provides storage mechanisms for events and snapshots
   - Implements the EventStoreDB integration
   - Handles serialization and deserialization of events
   - Manages stream naming and event metadata

5. **ReactiveDomain.Transport**
   - Implements transport mechanisms for distributed messaging
   - Supports both in-process and cross-process communication
   - Provides reliable message delivery guarantees
   - Implements message routing across process boundaries

6. **ReactiveDomain.Testing**
   - Provides testing utilities for event-sourced applications
   - Implements in-memory event stores for testing
   - Provides test fixtures for aggregates and event handlers
   - Supports both unit and integration testing

7. **ReactiveDomain.Tools**
   - Provides utility tools for working with event-sourced systems
   - Implements event store exploration and management tools
   - Provides diagnostic utilities for troubleshooting
   - Supports development and operational workflows

### Data Flow

The following diagram illustrates the data flow in a typical Reactive Domain application:

```mermaid
sequenceDiagram
    participant Client
    participant CommandHandler
    participant Aggregate
    participant Repository
    participant EventStore
    participant Projection
    
    Client->>CommandHandler: Send Command
    CommandHandler->>Aggregate: Load via Repository
    Aggregate->>Repository: Get Events
    Repository->>EventStore: Read Events
    EventStore-->>Repository: Return Events
    Repository-->>Aggregate: Apply Events
    Aggregate->>Aggregate: Process Command
    Aggregate->>Aggregate: Generate Events
    Aggregate->>Repository: Save Events
    Repository->>EventStore: Append Events
    EventStore-->>Repository: Confirm Save
    Repository-->>CommandHandler: Return Result
    CommandHandler-->>Client: Command Result
    EventStore->>Projection: Publish Events
    Projection->>Projection: Update Read Model
```

## Design Principles and Patterns

Reactive Domain is built on several key design principles and patterns:

### 1. Event Sourcing

Event sourcing is the core pattern in Reactive Domain, where the state of an entity is determined by a sequence of events rather than by its current state alone. This approach provides a complete audit trail and enables powerful temporal queries and analytics.

#### Core Principles of Event Sourcing

- **Events as the Source of Truth**: The sequence of events is the authoritative source of truth for the system
- **Immutable Event Records**: Events are immutable facts that have occurred and cannot be changed
- **State Reconstruction**: The current state is derived by replaying all events from the beginning
- **Append-Only Store**: Events are stored in an append-only event store, ensuring a complete history
- **Temporal Queries**: The ability to determine the state of the system at any point in time

#### Event Sourcing Flow

```mermaid
sequenceDiagram
    participant Client
    participant Command
    participant Aggregate
    participant EventStore
    participant ReadModel
    
    Client->>Command: Issue Command
    Command->>Aggregate: Load Current State
    Aggregate->>EventStore: Get Event History
    EventStore-->>Aggregate: Return Events
    Aggregate->>Aggregate: Apply Events to Build State
    Aggregate->>Aggregate: Execute Command Logic
    Aggregate->>Aggregate: Generate New Event(s)
    Aggregate->>EventStore: Store New Event(s)
    EventStore->>ReadModel: Publish Event(s)
    ReadModel->>ReadModel: Update State
```

#### Benefits of Event Sourcing

1. **Complete Audit Trail**: Every change to the system is recorded as an event
2. **Temporal Queries**: Ability to reconstruct the state at any point in time
3. **Event Replay**: System can be rebuilt by replaying events
4. **Debugging**: Easier to debug by examining the sequence of events
5. **Business Intelligence**: Events provide valuable data for analytics
6. **Scalability**: Read and write operations can be scaled independently

#### Implementation in Reactive Domain

- **`IEventSource` Interface**: Defines the contract for event-sourced entities
- **`AggregateRoot` Class**: Provides a base implementation for domain aggregates
- **Event Store**: Events are stored in EventStoreDB, an optimized database for event sourcing
- **Repositories**: Handle loading and saving aggregates by reading and writing events
- **Event Handlers**: Process events to update read models and trigger side effects
- **Snapshots**: Optimize performance by periodically saving aggregate state

### 2. Command Query Responsibility Segregation (CQRS)

Command Query Responsibility Segregation (CQRS) is an architectural pattern that separates the command (write) and query (read) sides of an application. This separation allows each side to be optimized for its specific requirements, leading to better performance, scalability, and maintainability.

#### Core Principles of CQRS

- **Separation of Concerns**: Write and read operations have different responsibilities and requirements
- **Command Side**: Focuses on processing commands, validating business rules, and generating events
- **Query Side**: Optimized for efficient data retrieval with denormalized read models
- **Eventual Consistency**: Read models may be eventually consistent with the write models
- **Independent Scaling**: Write and read sides can be scaled independently based on their specific loads

#### CQRS Architecture

```mermaid
graph TD
    Client[Client Application]
    
    %% Command Side
    CommandAPI[Command API]
    CommandBus[Command Bus]
    CommandHandler[Command Handler]
    Aggregate[Aggregate Root]
    EventStore[Event Store]
    
    %% Query Side
    QueryAPI[Query API]
    ReadModel[Read Model]
    QueryHandler[Query Handler]
    ReadDB[Read Database]
    
    %% Event Flow
    EventHandler[Event Handler]
    
    %% Command Flow
    Client -->|Commands| CommandAPI
    CommandAPI -->|Routes| CommandBus
    CommandBus -->|Dispatches| CommandHandler
    CommandHandler -->|Loads/Updates| Aggregate
    Aggregate -->|Stores Events| EventStore
    
    %% Query Flow
    Client -->|Queries| QueryAPI
    QueryAPI -->|Routes| QueryHandler
    QueryHandler -->|Reads from| ReadModel
    ReadModel -->|Stored in| ReadDB
    
    %% Event Flow
    EventStore -->|Publishes Events| EventHandler
    EventHandler -->|Updates| ReadModel
    
    %% Styling
    classDef commandSide fill:#f96,stroke:#333,stroke-width:2px
    classDef querySide fill:#9cf,stroke:#333,stroke-width:2px
    classDef eventFlow fill:#fc9,stroke:#333,stroke-width:2px
    
    class CommandAPI,CommandBus,CommandHandler,Aggregate,EventStore commandSide
    class QueryAPI,ReadModel,QueryHandler,ReadDB querySide
    class EventHandler eventFlow
```

#### Benefits of CQRS

1. **Optimized Performance**: Each side can be optimized for its specific requirements
2. **Scalability**: Write and read sides can be scaled independently
3. **Flexibility**: Read models can be tailored for specific query patterns
4. **Simplified Models**: Command models focus on business rules, read models on query efficiency
5. **Maintainability**: Clearer separation of concerns makes the system easier to maintain
6. **Evolvability**: Read models can evolve independently of the write models

#### Implementation in Reactive Domain

- **Command Handlers**: Process commands and update aggregates
- **Event Handlers**: Update read models based on events from the write side
- **Read Models**: Optimized for specific query patterns using `ReadModelBase`
- **Command Bus**: Routes commands to the appropriate handlers
- **Event Bus**: Publishes events to subscribers
- **Separate Repositories**: Different repositories for command and query sides
- **Correlation Tracking**: Ensures traceability between commands, events, and read model updates

### 3. Domain-Driven Design (DDD)

Domain-Driven Design (DDD) is a software development approach that focuses on creating a rich, expressive model of the business domain. Reactive Domain provides first-class support for DDD principles, enabling developers to build software that accurately reflects the business domain and its rules.

#### Core DDD Concepts in Reactive Domain

```mermaid
graph TD
    Domain[Domain Model] --> BC[Bounded Context]
    BC --> Agg[Aggregate]
    Agg --> AR[Aggregate Root]
    Agg --> E[Entity]
    Agg --> VO[Value Object]
    Domain --> DE[Domain Event]
    Domain --> Repo[Repository]
    Domain --> Service[Domain Service]
    
    classDef core fill:#f9f,stroke:#333,stroke-width:2px
    class AR,DE,Repo core
```

#### Key DDD Elements

1. **Bounded Context**: A logical boundary within which a particular domain model is defined and applicable
   - Reactive Domain supports multiple bounded contexts with separate models
   - Each bounded context can have its own event streams and read models

2. **Aggregates**: Clusters of domain objects treated as a single unit for data changes
   - **Aggregate Root**: The entry point to the aggregate, responsible for maintaining invariants
   - **Entities**: Objects with identity and lifecycle, distinguished by their ID
   - **Value Objects**: Immutable objects defined by their attributes, with no identity

3. **Domain Events**: Record of something significant that happened in the domain
   - Represent state changes in the system
   - Provide a history of changes to aggregates
   - Enable communication between bounded contexts

4. **Repositories**: Provide access to aggregates
   - Abstract the underlying storage mechanism
   - Handle loading and saving aggregates
   - Enforce aggregate boundaries

5. **Domain Services**: Encapsulate domain operations that don't naturally fit within an entity
   - Coordinate operations across multiple aggregates
   - Implement complex business processes
   - Provide domain-specific functionality

#### DDD and Event Sourcing Integration

```mermaid
sequenceDiagram
    participant Client
    participant AggregateRoot
    participant Repository
    participant EventStore
    
    Client->>AggregateRoot: Execute Domain Operation
    AggregateRoot->>AggregateRoot: Validate Business Rules
    AggregateRoot->>AggregateRoot: Generate Domain Event
    AggregateRoot->>Repository: Save
    Repository->>EventStore: Append Event
    EventStore-->>Repository: Confirm
    Repository-->>AggregateRoot: Return
    AggregateRoot-->>Client: Operation Result
```

#### Implementation in Reactive Domain

- **`AggregateRoot` Class**: Base class for implementing aggregate roots
  - Enforces business rules and invariants
  - Manages the lifecycle of the aggregate
  - Raises domain events in response to commands
  - Applies events to update state

- **Domain Events**: Implemented as immutable classes
  - Represent significant state changes
  - Contain all data needed to understand what happened
  - Support event versioning for schema evolution

- **Repositories**: Provide access to aggregates
  - `IRepository<T>` interface for aggregate repositories
  - Event-sourced implementation using EventStoreDB
  - Support for optimistic concurrency control

- **Domain Services**: Implemented as classes that coordinate operations
  - Process managers for complex workflows
  - Saga pattern for distributed transactions
  - Domain-specific services for specialized operations

#### Benefits of DDD with Reactive Domain

1. **Alignment with Business**: Models closely reflect the business domain
2. **Expressive Models**: Rich domain models capture complex business rules
3. **Maintainability**: Clear boundaries and responsibilities
4. **Flexibility**: Ability to evolve the model as the domain changes
5. **Scalability**: Natural fit with CQRS and Event Sourcing
6. **Testability**: Easy to test business rules in isolation
- Events represent domain events
- Repositories provide aggregate persistence
- Value objects can be used as event properties

### 4. Messaging Patterns

Reactive Domain uses messaging patterns for communication:

- Commands represent intentions to change state
- Events represent state changes that have occurred
- Queries request information from read models
- Message handlers process messages and perform actions

Implementation in Reactive Domain:
- `IMessage`, `ICommand`, `IEvent` interfaces define message contracts
- Message buses route messages to handlers
- Message handlers process messages and perform actions
- Correlation and causation tracking links related messages

### 4. CQRS and Event Sourcing Integration

One of the most powerful aspects of Reactive Domain is how it seamlessly integrates Command Query Responsibility Segregation (CQRS) with Event Sourcing to create a robust, scalable, and maintainable architecture. This integration provides a comprehensive solution for building complex, event-driven systems.

#### How CQRS and Event Sourcing Complement Each Other

```mermaid
graph TD
    subgraph "Command Side (Write Model)"
        Command[Command] --> CommandHandler[Command Handler]
        CommandHandler --> AggregateRoot[Aggregate Root]
        AggregateRoot --> Event[Domain Event]
        Event --> EventStore[Event Store]
    end
    
    subgraph "Query Side (Read Model)"
        EventStore --> EventHandler[Event Handler]
        EventHandler --> ReadModel[Read Model]
        ReadModel --> QueryHandler[Query Handler]
        QueryHandler --> Query[Query Result]
    end
    
    classDef commandSide fill:#f96,stroke:#333,stroke-width:2px
    classDef querySide fill:#9cf,stroke:#333,stroke-width:2px
    classDef shared fill:#fc9,stroke:#333,stroke-width:2px
    
    class Command,CommandHandler,AggregateRoot commandSide
    class ReadModel,QueryHandler,Query querySide
    class Event,EventStore,EventHandler shared
```

#### Key Integration Points

1. **Events as the Integration Mechanism**
   - Events generated by the command side are the source of truth
   - The query side consumes these events to build read models
   - Events provide a clean, decoupled integration between the two sides

2. **Event Store as the Central Hub**
   - The event store serves as both the write-side database and the source for read-model updates
   - It provides persistence, publication, and subscription capabilities
   - It ensures that all read models eventually reflect all events

3. **Eventual Consistency Model**
   - Read models are eventually consistent with the write model
   - This allows for independent scaling and optimization of each side
   - The system can continue to function even if read models lag behind

#### Complete Flow in Reactive Domain

```mermaid
sequenceDiagram
    participant Client
    participant CommandBus
    participant CommandHandler
    participant AggregateRoot
    participant Repository
    participant EventStore
    participant EventBus
    participant EventHandler
    participant ReadModel
    participant QueryHandler
    
    Client->>CommandBus: Send Command
    CommandBus->>CommandHandler: Route Command
    CommandHandler->>Repository: Get Aggregate
    Repository->>EventStore: Load Events
    EventStore-->>Repository: Return Events
    Repository->>AggregateRoot: Apply Events
    AggregateRoot->>AggregateRoot: Build Current State
    AggregateRoot->>AggregateRoot: Execute Command Logic
    AggregateRoot->>AggregateRoot: Validate Business Rules
    AggregateRoot->>AggregateRoot: Generate Domain Event
    AggregateRoot->>Repository: Save
    Repository->>EventStore: Append Event
    EventStore-->>Repository: Confirm
    Repository-->>CommandHandler: Return Result
    CommandHandler-->>Client: Command Result
    
    EventStore->>EventBus: Publish Event
    EventBus->>EventHandler: Notify Subscribers
    EventHandler->>ReadModel: Update Read Model
    ReadModel->>ReadModel: Apply Event Data
    
    Client->>QueryHandler: Send Query
    QueryHandler->>ReadModel: Get Data
    ReadModel-->>QueryHandler: Return Data
    QueryHandler-->>Client: Query Result
```

#### Benefits of the Integrated Approach

1. **Separation with Coordination**: Clear separation of concerns while maintaining a coordinated system
2. **Complete Audit Trail**: Every state change is recorded as an event
3. **Scalability**: Each side can be scaled independently based on its specific load
4. **Flexibility**: Read models can be tailored for specific query patterns
5. **Resilience**: The system can continue to function even if parts of it fail
6. **Evolution**: The system can evolve over time without breaking existing functionality

#### Implementation Considerations

- **Correlation and Causation**: Track the relationship between commands and events
- **Idempotent Event Handlers**: Ensure that applying the same event multiple times is safe
- **Versioning Strategy**: Plan for event schema evolution
- **Consistency Boundaries**: Define clear aggregate boundaries to maintain consistency
- **Read Model Rebuilding**: Design for the ability to rebuild read models from event streams

### 5. Reactive Programming

Reactive Domain embraces reactive programming principles:

- Asynchronous message processing
- Event-driven architecture
- Non-blocking operations
- Resilience and fault tolerance

Implementation in Reactive Domain:
- Asynchronous message handling
- Event-driven workflows
- Reactive streams for event processing
- Error handling and recovery mechanisms

## Component Interactions

### Command Processing Flow

1. **Command Creation**
   - A command is created, representing an intention to change state
   - Commands include a unique ID, correlation ID, and causation ID
   - Commands are validated before processing

2. **Command Routing**
   - Commands are routed to the appropriate command handler
   - Routing is based on command type
   - Command handlers are registered with the command bus

3. **Aggregate Loading**
   - The command handler loads the target aggregate from the repository
   - The repository reads events from the event store
   - Events are applied to the aggregate to reconstruct its state

4. **Command Handling**
   - The command is passed to the aggregate for processing
   - The aggregate validates the command against its current state
   - If valid, the aggregate generates one or more events
   - Events are applied to the aggregate to update its state

5. **Event Persistence**
   - The aggregate's events are saved to the event store
   - The repository handles optimistic concurrency control
   - Events are persisted atomically with the expected version

6. **Event Publication**
   - Events are published to event handlers
   - Event handlers update read models and trigger side effects
   - Event handlers can be synchronous or asynchronous

### Query Processing Flow

1. **Query Creation**
   - A query is created, representing a request for information
   - Queries include parameters to filter and shape the results

2. **Query Routing**
   - Queries are routed to the appropriate query handler
   - Routing is based on query type
   - Query handlers are registered with the query bus

3. **Read Model Access**
   - The query handler accesses the appropriate read model
   - Read models are optimized for specific query patterns
   - Read models can be in-memory, database tables, or other storage

4. **Result Generation**
   - The query handler processes the query against the read model
   - Results are filtered and shaped according to the query parameters
   - Results are returned to the caller

### Event Processing Flow

1. **Event Generation**
   - Events are generated by aggregates in response to commands
   - Events represent state changes that have occurred
   - Events include metadata such as correlation and causation IDs

2. **Event Persistence**
   - Events are persisted to the event store
   - Events are stored in streams, typically one stream per aggregate
   - Events include metadata for tracing and debugging

3. **Event Publication**
   - Events are published to event handlers
   - Event handlers can be synchronous or asynchronous
   - Event handlers can update read models, trigger side effects, or start processes

4. **Read Model Updates**
   - Event handlers update read models based on event data
   - Read models are optimized for specific query patterns
   - Read models can be in-memory, database tables, or other storage

## Key Component Relationships

Understanding how the key components in Reactive Domain relate to each other is essential for effective implementation. This section details the relationships between core components like AggregateRoot, Command, Event, MessageBuilder, and ReadModelBase.

### Command and Event Relationship

```mermaid
graph TD
    A[Command] -->|"Handled by"| B[CommandHandler]
    B -->|"Loads/Updates"| C[AggregateRoot]
    C -->|"Raises"| D[Event]
    D -->|"Applied to"| C
    D -->|"Persisted by"| E[Repository]
    D -->|"Published to"| F[EventHandler]
    F -->|"Updates"| G[ReadModelBase]
```

- **Command and Event**: Both implement `ICorrelatedMessage` for correlation tracking
- **Command to Event Flow**: Commands are processed by aggregates to produce events
- **Event to ReadModel Flow**: Events update read models through event handlers

### MessageBuilder's Role

```mermaid
graph LR
    A[Command] -->|"Source for"| B[MessageBuilder]
    B -->|"Creates"| C[Event]
    C -->|"Preserves correlation from"| A
```

- **MessageBuilder** acts as a factory for creating correlated messages
- It ensures proper correlation and causation tracking between commands and events
- It maintains the correlation chain across the entire message flow

### Aggregate and Repository Interaction

```mermaid
sequenceDiagram
    participant A as AggregateRoot
    participant R as Repository
    participant E as EventStore
    
    R->>E: Load Events
    E-->>R: Return Events
    R->>A: Apply Events
    A->>A: Process Command
    A->>A: Raise Events
    A->>R: Save
    R->>E: Append Events
```

- **AggregateRoot** is responsible for business logic and raising events
- **Repository** loads and saves aggregates by reading/writing events
- Events are applied to aggregates to reconstruct state

### ReadModelBase and Event Handlers

```mermaid
graph TD
    A[Event] -->|"Handled by"| B[EventHandler]
    B -->|"Updates"| C[ReadModelBase]
    C -->|"Stored in"| D[ReadModelRepository]
    E[Query] -->|"Reads from"| D
```

- **ReadModelBase** provides the foundation for all read models
- **Event Handlers** subscribe to events and update read models
- Read models are optimized for specific query patterns
- The separation between write models (aggregates) and read models enables CQRS

### Correlation and Causation Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant CMD as Command
    participant AGG as Aggregate
    participant EVT as Event
    participant MB as MessageBuilder
    
    C->>CMD: Create Command (new CorrelationId)
    CMD->>AGG: Process Command
    AGG->>MB: Create Event
    MB->>EVT: Set CorrelationId from Command
    MB->>EVT: Set CausationId as Command.MsgId
```

- **ICorrelatedMessage** is implemented by both Command and Event
- **MessageBuilder** ensures proper correlation between commands and events
- Correlation IDs track related messages across the entire system
- Causation IDs establish direct cause-effect relationships

Understanding these relationships is key to implementing effective event-sourced systems with Reactive Domain. The components work together to provide a comprehensive solution for CQRS and event sourcing.

## Extension Points

Reactive Domain provides several extension points for customization:

### 1. Custom Aggregates

Create custom aggregates by extending `AggregateRoot`:

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    public Account(Guid id) : base(id)
    {
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        RaiseEvent(new AmountDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new AmountWithdrawn(Id, amount));
    }
    
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

### 2. Custom Event Serialization

Implement custom event serialization by implementing `IEventSerializer`:

```csharp
public class CustomEventSerializer : IEventSerializer
{
    public object Deserialize(RecordedEvent recordedEvent)
    {
        // Custom deserialization logic
        var eventType = Type.GetType(recordedEvent.EventType);
        var eventData = Encoding.UTF8.GetString(recordedEvent.Data);
        return JsonConvert.DeserializeObject(eventData, eventType);
    }
    
    public IEventData Serialize(object @event, Guid eventId)
    {
        // Custom serialization logic
        var eventType = @event.GetType().AssemblyQualifiedName;
        var eventData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
        var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = eventType }));
        
        return new EventData(eventId, eventType, true, eventData, metadata);
    }
}
```

### 3. Custom Stream Naming

Implement custom stream naming by implementing `IStreamNameBuilder`:

```csharp
public class CustomStreamNameBuilder : IStreamNameBuilder
{
    public string GenerateForAggregate(Type aggregateType, Guid aggregateId)
    {
        // Custom stream naming logic
        return $"custom-{aggregateType.Name.ToLower()}-{aggregateId}";
    }
}
```

### 4. Custom Message Handling

Implement custom message handling by implementing `ICommandHandler`, `IEventHandler`, or `IQueryHandler`:

```csharp
public class CustomCommandHandler : ICommandHandler<CreateAccount>
{
    private readonly IRepository _repository;
    
    public CustomCommandHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccount command)
    {
        // Custom command handling logic
        var account = new Account(command.AccountId);
        account.Initialize(command.InitialBalance);
        _repository.Save(account);
    }
}
```

### 5. Custom Event Store Integration

Implement custom event store integration by implementing `IStreamStoreConnection`:

```csharp
public class CustomStreamStoreConnection : IStreamStoreConnection
{
    public void Connect()
    {
        // Custom connection logic
    }
    
    public void Disconnect()
    {
        // Custom disconnection logic
    }
    
    public IStreamSlice ReadStreamForward(string streamName, long start, int count)
    {
        // Custom stream reading logic
        return new StreamSlice(/* ... */);
    }
    
    public void AppendToStream(string streamName, long expectedVersion, IEnumerable<IEventData> events)
    {
        // Custom stream appending logic
    }
    
    // Implement other methods
}
```

### 6. Custom Snapshots

Implement custom snapshots by implementing `ISnapshotSource`:

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

## Integration Patterns

Reactive Domain supports several integration patterns for working with external systems:

### 1. Event-Driven Integration

Use events to integrate with external systems:

```csharp
public class ExternalSystemIntegration : IEventHandler<AccountCreated>, IEventHandler<AmountDeposited>
{
    private readonly IExternalSystem _externalSystem;
    
    public ExternalSystemIntegration(IExternalSystem externalSystem)
    {
        _externalSystem = externalSystem;
    }
    
    public void Handle(AccountCreated @event)
    {
        _externalSystem.CreateAccount(@event.AccountId, @event.Owner);
    }
    
    public void Handle(AmountDeposited @event)
    {
        _externalSystem.RecordDeposit(@event.AccountId, @event.Amount);
    }
}
```

### 2. Command-Driven Integration

Use commands to integrate with external systems:

```csharp
public class ExternalSystemCommandHandler : ICommandHandler<CreateExternalAccount>
{
    private readonly IRepository _repository;
    private readonly IExternalSystem _externalSystem;
    
    public ExternalSystemCommandHandler(IRepository repository, IExternalSystem externalSystem)
    {
        _repository = repository;
        _externalSystem = externalSystem;
    }
    
    public void Handle(CreateExternalAccount command)
    {
        // Create account in local system
        var account = new Account(command.AccountId);
        account.Initialize(command.InitialBalance);
        _repository.Save(account);
        
        // Create account in external system
        _externalSystem.CreateAccount(command.AccountId, command.Owner);
    }
}
```

### 3. Saga Pattern

Use sagas to coordinate complex workflows involving multiple aggregates and external systems:

```csharp
public class AccountTransferSaga : 
    IEventHandler<TransferInitiated>,
    IEventHandler<SourceAccountDebited>,
    IEventHandler<DestinationAccountCredited>,
    IEventHandler<TransferFailed>
{
    private readonly IRepository _repository;
    private readonly ICommandBus _commandBus;
    
    public AccountTransferSaga(IRepository repository, ICommandBus commandBus)
    {
        _repository = repository;
        _commandBus = commandBus;
    }
    
    public void Handle(TransferInitiated @event)
    {
        _commandBus.Send(new DebitSourceAccount(
            @event.TransferId,
            @event.SourceAccountId,
            @event.Amount));
    }
    
    public void Handle(SourceAccountDebited @event)
    {
        _commandBus.Send(new CreditDestinationAccount(
            @event.TransferId,
            @event.DestinationAccountId,
            @event.Amount));
    }
    
    public void Handle(DestinationAccountCredited @event)
    {
        _commandBus.Send(new CompleteTransfer(@event.TransferId));
    }
    
    public void Handle(TransferFailed @event)
    {
        if (@event.Stage == TransferStage.Debit)
        {
            // No compensation needed, transfer failed before any changes
            _commandBus.Send(new CancelTransfer(@event.TransferId, @event.Reason));
        }
        else if (@event.Stage == TransferStage.Credit)
        {
            // Compensate by crediting the source account
            _commandBus.Send(new RefundSourceAccount(
                @event.TransferId,
                @event.SourceAccountId,
                @event.Amount));
        }
    }
}
```

### 4. Outbox Pattern

Use the outbox pattern to ensure reliable integration with external systems:

```csharp
public class OutboxRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly IOutboxStore _outboxStore;
    
    public OutboxRepository(IRepository innerRepository, IOutboxStore outboxStore)
    {
        _innerRepository = innerRepository;
        _outboxStore = outboxStore;
    }
    
    public void Save(IEventSource aggregate)
    {
        using (var transaction = new TransactionScope())
        {
            _innerRepository.Save(aggregate);
            
            // Store integration events in the outbox
            foreach (var @event in aggregate.TakeEvents())
            {
                _outboxStore.Add(new OutboxMessage
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

public class OutboxProcessor
{
    private readonly IOutboxStore _outboxStore;
    private readonly IExternalSystem _externalSystem;
    
    public OutboxProcessor(IOutboxStore outboxStore, IExternalSystem externalSystem)
    {
        _outboxStore = outboxStore;
        _externalSystem = externalSystem;
    }
    
    public void ProcessOutbox()
    {
        var messages = _outboxStore.GetPendingMessages();
        
        foreach (var message in messages)
        {
            try
            {
                var @event = JsonConvert.DeserializeObject(message.EventData, Type.GetType(message.EventType));
                
                // Process the event for integration
                if (@event is AccountCreated accountCreated)
                {
                    _externalSystem.CreateAccount(accountCreated.AccountId, accountCreated.Owner);
                }
                else if (@event is AmountDeposited amountDeposited)
                {
                    _externalSystem.RecordDeposit(amountDeposited.AccountId, amountDeposited.Amount);
                }
                
                // Mark as processed
                _outboxStore.MarkAsProcessed(message.Id);
            }
            catch (Exception ex)
            {
                // Log error and retry later
                _outboxStore.MarkAsFailed(message.Id, ex.Message);
            }
        }
    }
}
```

## Scaling and Performance Considerations

### 1. Event Store Scaling

EventStoreDB can be scaled in several ways:

- **Single Node**: Suitable for development and small production workloads
- **Cluster**: Multiple nodes for high availability and throughput
- **Projections**: Offload read model building to EventStoreDB projections
- **Subscriptions**: Use persistent subscriptions for reliable event processing

Recommendations:
- Use a cluster configuration in production
- Configure appropriate hardware for the event store
- Monitor event store performance and adjust resources as needed
- Use snapshots for large aggregates to improve loading performance

### 2. Read Model Scaling

Read models can be scaled independently:

- **Database Scaling**: Scale the database hosting read models
- **Caching**: Use caching to reduce database load
- **Sharding**: Shard read models for high-volume data
- **Eventual Consistency**: Accept eventual consistency for better scalability

Recommendations:
- Choose the right database for each read model
- Scale read models independently based on query patterns
- Use caching for frequently accessed data
- Consider eventual consistency trade-offs

### 3. Command Processing Scaling

Command processing can be scaled in several ways:

- **Horizontal Scaling**: Deploy multiple command processors
- **Load Balancing**: Distribute commands across processors
- **Command Queuing**: Queue commands for asynchronous processing
- **Command Batching**: Batch related commands for efficiency

Recommendations:
- Scale command processors based on throughput requirements
- Use load balancing for high-volume command processing
- Consider command queuing for peak load handling
- Batch related commands where appropriate

### 4. Event Processing Scaling

Event processing can be scaled in several ways:

- **Parallel Processing**: Process events in parallel
- **Competing Consumers**: Use competing consumers for event processing
- **Event Partitioning**: Partition events for parallel processing
- **Backpressure**: Implement backpressure for overload protection

Recommendations:
- Scale event processors based on event volume
- Use competing consumers for high-volume event processing
- Partition events by aggregate type or other criteria
- Implement backpressure mechanisms for overload protection

### 5. Performance Optimization Techniques

Several techniques can improve performance:

- **Snapshots**: Use snapshots to reduce event loading time
- **Caching**: Cache aggregates and read models
- **Batching**: Batch operations for efficiency
- **Asynchronous Processing**: Use asynchronous processing for non-critical operations
- **Read Model Optimization**: Optimize read models for specific query patterns

Recommendations:
- Use snapshots for aggregates with many events
- Implement caching for frequently accessed data
- Batch operations where appropriate
- Use asynchronous processing for non-critical operations
- Optimize read models for specific query patterns

## Security Considerations

### 1. Authentication and Authorization

- **Command Authorization**: Authorize commands before processing
- **Query Authorization**: Authorize queries before processing
- **Event Authorization**: Control access to events
- **Role-Based Access Control**: Implement RBAC for command and query authorization

Implementation:
```csharp
public class AuthorizedCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserContext _userContext;
    
    public AuthorizedCommandBus(
        ICommandBus innerBus,
        IAuthorizationService authorizationService,
        IUserContext userContext)
    {
        _innerBus = innerBus;
        _authorizationService = authorizationService;
        _userContext = userContext;
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        // Authorize the command
        if (!_authorizationService.IsAuthorized(_userContext.CurrentUser, command))
        {
            throw new UnauthorizedAccessException($"User is not authorized to execute {typeof(TCommand).Name}");
        }
        
        // Forward to inner bus
        _innerBus.Send(command);
    }
}
```

### 2. Data Protection

- **Event Data Encryption**: Encrypt sensitive event data
- **Metadata Protection**: Protect sensitive metadata
- **Secure Storage**: Secure event store and read model storage
- **Data Masking**: Mask sensitive data in logs and diagnostics

Implementation:
```csharp
public class EncryptingEventSerializer : IEventSerializer
{
    private readonly IEventSerializer _innerSerializer;
    private readonly IEncryptionService _encryptionService;
    
    public EncryptingEventSerializer(
        IEventSerializer innerSerializer,
        IEncryptionService encryptionService)
    {
        _innerSerializer = innerSerializer;
        _encryptionService = encryptionService;
    }
    
    public object Deserialize(RecordedEvent recordedEvent)
    {
        // Decrypt event data if necessary
        if (ShouldEncrypt(recordedEvent.EventType))
        {
            var decryptedData = _encryptionService.Decrypt(recordedEvent.Data);
            var decryptedEvent = new RecordedEvent(
                recordedEvent.EventStreamId,
                recordedEvent.EventNumber,
                recordedEvent.EventId,
                recordedEvent.EventType,
                decryptedData,
                recordedEvent.Metadata,
                recordedEvent.IsJson,
                recordedEvent.Created);
                
            return _innerSerializer.Deserialize(decryptedEvent);
        }
        
        return _innerSerializer.Deserialize(recordedEvent);
    }
    
    public IEventData Serialize(object @event, Guid eventId)
    {
        var eventData = _innerSerializer.Serialize(@event, eventId);
        
        // Encrypt event data if necessary
        if (ShouldEncrypt(@event.GetType().Name))
        {
            var encryptedData = _encryptionService.Encrypt(eventData.Data);
            return new EventData(
                eventData.EventId,
                eventData.Type,
                eventData.IsJson,
                encryptedData,
                eventData.Metadata);
        }
        
        return eventData;
    }
    
    private bool ShouldEncrypt(string eventType)
    {
        // Determine if the event type should be encrypted
        return eventType.Contains("Sensitive") || eventType.Contains("Personal");
    }
}
```

### 3. Audit Logging

- **Command Logging**: Log all commands with user context
- **Event Logging**: Log all events with correlation and causation
- **Access Logging**: Log all access to the system
- **Compliance Logging**: Log compliance-related activities

Implementation:
```csharp
public class AuditingCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly IAuditLogger _auditLogger;
    private readonly IUserContext _userContext;
    
    public AuditingCommandBus(
        ICommandBus innerBus,
        IAuditLogger auditLogger,
        IUserContext userContext)
    {
        _innerBus = innerBus;
        _auditLogger = auditLogger;
        _userContext = userContext;
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        // Log the command for audit purposes
        _auditLogger.LogCommand(
            command.GetType().Name,
            JsonConvert.SerializeObject(command),
            _userContext.CurrentUser.Id,
            DateTime.UtcNow);
        
        // Forward to inner bus
        _innerBus.Send(command);
    }
}
```

### 4. Secure Deployment

- **Secure Configuration**: Protect configuration settings
- **Secret Management**: Use secure secret management
- **Network Security**: Secure network communication
- **Infrastructure Security**: Secure the infrastructure

Recommendations:
- Use secure configuration management
- Store secrets in a secure vault
- Encrypt network communication
- Implement infrastructure security best practices

### 5. Threat Modeling

- **Identify Assets**: Identify valuable assets in the system
- **Identify Threats**: Identify potential threats to those assets
- **Assess Risks**: Assess the risks of each threat
- **Mitigate Risks**: Implement controls to mitigate risks

Recommendations:
- Conduct regular threat modeling exercises
- Update threat models as the system evolves
- Implement controls based on risk assessment
- Test controls for effectiveness

---

**Navigation**:
- [← Previous: API Reference](api-reference/README.md)
- [↑ Back to Top](#architecture-guide)
- [→ Next: Migration Guide](migration.md)
