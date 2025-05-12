# CQRS Architecture Diagram

This diagram illustrates the Command Query Responsibility Segregation (CQRS) pattern as implemented in Reactive Domain.

## CQRS Overview

```mermaid
flowchart TD
    subgraph "Command Side (Write Model)"
        A[Client] -->|Commands| B[Command Bus]
        B -->|Dispatch| C[Command Handlers]
        C -->|Modify| D[Domain Model/Aggregates]
        D -->|Generate| E[Events]
        E -->|Store| F[Event Store]
    end
    
    subgraph "Query Side (Read Model)"
        F -->|Subscribe| G[Event Handlers/Projections]
        G -->|Update| H[Read Models]
        I[Client] -->|Queries| J[Query Bus]
        J -->|Dispatch| K[Query Handlers]
        K -->|Read| H
        H -->|Results| I
    end
    
    style A fill:#f9f,stroke:#333,stroke-width:2px
    style I fill:#f9f,stroke:#333,stroke-width:2px
    style F fill:#bbf,stroke:#333,stroke-width:4px
    style D fill:#fbb,stroke:#333,stroke-width:2px
    style H fill:#bfb,stroke:#333,stroke-width:2px
```

## Detailed CQRS Implementation

```mermaid
sequenceDiagram
    participant Client
    participant CommandBus
    participant CommandHandler
    participant Aggregate
    participant EventStore
    participant Projection
    participant ReadModel
    participant QueryHandler
    
    Note over Client,QueryHandler: Command Flow (Write Operations)
    
    Client->>CommandBus: Send Command (CreateAccount)
    CommandBus->>CommandHandler: Dispatch to Handler
    CommandHandler->>Aggregate: Create New Aggregate
    Aggregate->>Aggregate: Apply Business Rules
    Aggregate->>Aggregate: Generate Event (AccountCreated)
    CommandHandler->>EventStore: Save Events
    
    Note over EventStore,QueryHandler: Query Flow (Read Operations)
    
    EventStore->>Projection: Notify of New Event
    Projection->>ReadModel: Update Read Model
    
    Client->>QueryHandler: Send Query (GetAccountDetails)
    QueryHandler->>ReadModel: Retrieve Data
    ReadModel->>QueryHandler: Return Data
    QueryHandler->>Client: Return Query Result
```

## Key Components

### Command Side (Write Model)

- **Commands**: Represent intentions to change the system state
- **Command Bus**: Routes commands to appropriate handlers
- **Command Handlers**: Process commands and coordinate with aggregates
- **Domain Model/Aggregates**: Encapsulate business rules and state changes
- **Events**: Represent facts that have occurred in the system
- **Event Store**: Persists events as the source of truth

### Query Side (Read Model)

- **Event Handlers/Projections**: Transform events into read models
- **Read Models**: Optimized for querying and reporting
- **Queries**: Requests for information from the system
- **Query Bus**: Routes queries to appropriate handlers
- **Query Handlers**: Process queries against read models

## Benefits of CQRS in Reactive Domain

- **Separation of Concerns**: Write and read operations are handled separately
- **Scalability**: Read and write sides can be scaled independently
- **Optimization**: Read models can be optimized for specific query patterns
- **Flexibility**: Multiple read models can be created from the same events
- **Performance**: Read operations don't block write operations and vice versa

## Implementation in Reactive Domain

Reactive Domain provides infrastructure for implementing CQRS:

- **Command and Event Base Classes**: Provide structure for messages
- **Repository Pattern**: Abstracts event storage and retrieval
- **MessageBus**: Routes commands and events to handlers
- **ReadModelBase**: Base class for creating read models
- **Projection Framework**: Tools for transforming events into read models
