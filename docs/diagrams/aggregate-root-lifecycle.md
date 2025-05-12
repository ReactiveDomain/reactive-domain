# AggregateRoot Lifecycle Diagram

This diagram illustrates the lifecycle of an `AggregateRoot` in Reactive Domain, showing how commands are processed and events are applied.

## Aggregate Lifecycle Overview

```mermaid
stateDiagram-v2
    [*] --> New: Create New Aggregate
    New --> Active: Apply Initial Events
    Active --> Active: Process Commands
    Active --> Active: Apply Events
    Active --> Deleted: Delete Aggregate
    Deleted --> [*]
    
    state Active {
        [*] --> ValidatingCommand
        ValidatingCommand --> GeneratingEvents: Command Valid
        ValidatingCommand --> RejectedCommand: Command Invalid
        GeneratingEvents --> ApplyingEvents
        ApplyingEvents --> [*]
        RejectedCommand --> [*]
    }
```

## Detailed Aggregate Processing Flow

```mermaid
sequenceDiagram
    participant Client
    participant Repository
    participant AggregateRoot
    participant EventStore
    
    Note over Client,EventStore: Creating a New Aggregate
    
    Client->>AggregateRoot: new Account(id)
    AggregateRoot->>AggregateRoot: Initialize with ID
    Client->>AggregateRoot: Create(name, initialBalance)
    AggregateRoot->>AggregateRoot: Validate Command
    AggregateRoot->>AggregateRoot: RaiseEvent(AccountCreated)
    AggregateRoot->>AggregateRoot: Apply(AccountCreated)
    Client->>Repository: Save(account)
    Repository->>EventStore: AppendToStream(events)
    
    Note over Client,EventStore: Loading an Existing Aggregate
    
    Client->>Repository: GetById<Account>(id)
    Repository->>EventStore: GetEvents(id)
    EventStore-->>Repository: Return Events
    Repository->>AggregateRoot: new Account(id)
    Repository->>AggregateRoot: RestoreFromEvents(events)
    loop For Each Event
        AggregateRoot->>AggregateRoot: Apply(Event)
        AggregateRoot->>AggregateRoot: Update ExpectedVersion
    end
    Repository-->>Client: Return Reconstructed Account
    
    Note over Client,EventStore: Processing a Command
    
    Client->>AggregateRoot: Deposit(amount)
    AggregateRoot->>AggregateRoot: Validate Command
    AggregateRoot->>AggregateRoot: RaiseEvent(FundsDeposited)
    AggregateRoot->>AggregateRoot: Apply(FundsDeposited)
    Client->>Repository: Save(account)
    Repository->>AggregateRoot: TakeEvents()
    AggregateRoot-->>Repository: Return New Events
    Repository->>EventStore: AppendToStream(events)
```

## Key Methods in AggregateRoot

### RaiseEvent

```mermaid
flowchart TD
    A[RaiseEvent Method] -->|1. Record Event| B[EventRecorder]
    A -->|2. Find Apply Method| C[Reflection]
    C -->|3. Call Apply Method| D[Apply Method]
    D -->|4. Update State| E[Aggregate State]
```

### RestoreFromEvents

```mermaid
flowchart TD
    A[RestoreFromEvents Method] -->|1. For Each Event| B[Event Loop]
    B -->|2. Find Apply Method| C[Reflection]
    C -->|3. Call Apply Method| D[Apply Method]
    D -->|4. Update State| E[Aggregate State]
    B -->|5. Update Version| F[ExpectedVersion]
```

### TakeEvents

```mermaid
flowchart TD
    A[TakeEvents Method] -->|1. Get Recorded Events| B[EventRecorder]
    B -->|2. Return Events| C[Event Array]
    A -->|3. Clear Recorder| B
```

## Implementation Example

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isClosed;
    
    // Constructor for new aggregate
    public Account(Guid id) : base(id)
    {
        // Initialize with default state
    }
    
    // Constructor for loading from history
    protected Account(Guid id, IEnumerable<object> events) : base(id, events)
    {
        // Base constructor will call RestoreFromEvents
    }
    
    // Command method
    public void Deposit(decimal amount)
    {
        // Validate command
        if (_isClosed)
            throw new InvalidOperationException("Account is closed");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
            
        // Generate and apply event
        RaiseEvent(new FundsDeposited(Id, amount));
    }
    
    // Event handler
    private void Apply(FundsDeposited @event)
    {
        // Update state
        _balance += @event.Amount;
    }
}
```

## Key Concepts Illustrated

### Aggregate Identity and State

- Each aggregate has a unique identifier (`Id`)
- State is maintained through private fields (`_balance`, `_isClosed`)
- State is only modified through event application

### Command Processing

1. Commands are validated against current state
2. If valid, events are generated using `RaiseEvent`
3. Events are applied to update the aggregate state
4. New events are collected for storage

### Event Sourcing

- Events are the source of truth for aggregate state
- State is reconstructed by replaying events
- `ExpectedVersion` tracks the version for optimistic concurrency

### Invariant Protection

- Business rules are enforced in command methods
- Events are only generated if commands are valid
- State consistency is maintained through careful validation
