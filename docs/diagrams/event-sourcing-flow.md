# Event Sourcing Flow Diagram

This diagram illustrates the core flow of events in an event-sourced system using Reactive Domain.

## Basic Event Sourcing Flow

```mermaid
flowchart TD
    subgraph "Command Side"
        A[Client] -->|1. Send Command| B[Command Handler]
        B -->|2. Load Aggregate| C[Repository]
        C -->|3. Retrieve Events| D[Event Store]
        D -->|4. Return Events| C
        C -->|5. Reconstruct State| E[Aggregate]
        B -->|6. Execute Command| E
        E -->|7. Generate Events| E
        E -->|8. Save Events| C
        C -->|9. Append Events| D
    end
    
    subgraph "Query Side"
        D -->|10. Publish Events| F[Event Handlers/Projections]
        F -->|11. Update| G[Read Models]
        H[Client] -->|12. Query| G
        G -->|13. Return Data| H
    end
    
    style A fill:#f9f,stroke:#333,stroke-width:2px
    style H fill:#f9f,stroke:#333,stroke-width:2px
    style D fill:#bbf,stroke:#333,stroke-width:4px
    style G fill:#bfb,stroke:#333,stroke-width:4px
```

## Detailed Event Flow with State Reconstruction

```mermaid
sequenceDiagram
    participant Client
    participant CommandHandler
    participant Repository
    participant EventStore
    participant Aggregate
    participant Projections
    participant ReadModels
    
    Client->>CommandHandler: Send Command (e.g., DepositFunds)
    CommandHandler->>Repository: GetById(accountId)
    Repository->>EventStore: GetEvents(accountId)
    EventStore-->>Repository: Return Event Stream
    
    Note over Repository,Aggregate: State Reconstruction
    Repository->>Aggregate: Create Empty Aggregate
    Repository->>Aggregate: Apply Event 1: AccountCreated
    Repository->>Aggregate: Apply Event 2: FundsDeposited
    Repository->>Aggregate: Apply Event 3: FundsWithdrawn
    
    Repository-->>CommandHandler: Return Reconstructed Aggregate
    CommandHandler->>Aggregate: Execute Command (Deposit)
    Aggregate->>Aggregate: Validate Command
    Aggregate->>Aggregate: Generate Event (FundsDeposited)
    CommandHandler->>Repository: Save(aggregate)
    Repository->>EventStore: AppendToStream(accountId, events)
    
    EventStore->>Projections: Publish New Event
    Projections->>ReadModels: Update Account Balance
    
    Client->>ReadModels: Query Account Balance
    ReadModels-->>Client: Return Current Balance
```

## Key Concepts Illustrated

### Event Storage and Retrieval

- Events are the primary source of truth in the system
- All state changes are recorded as immutable events
- The event store maintains the complete history of all events

### State Reconstruction

- Aggregates don't store state directly
- State is reconstructed by replaying events in sequence
- Each event is applied to the aggregate to update its state

### Command Processing

1. Commands are received from clients
2. The appropriate aggregate is loaded from its event history
3. The command is executed on the aggregate
4. New events are generated to represent state changes
5. The events are saved to the event store

### Projections and Read Models

- Events are published to projections
- Projections transform events into read-optimized models
- Clients query read models for information
- Multiple read models can be built from the same events

## Benefits of Event Sourcing

- **Complete Audit Trail**: Every change is recorded as an event
- **Temporal Queries**: Ability to determine state at any point in time
- **Separation of Concerns**: Clear separation between write and read operations
- **Event Replay**: Ability to rebuild state or create new projections from existing events
