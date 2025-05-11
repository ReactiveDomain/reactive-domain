# ICorrelatedMessage

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ICorrelatedMessage` is a core interface in Reactive Domain that extends the base `IMessage` interface to add correlation and causation tracking capabilities. This interface is fundamental to tracing message flows and understanding relationships between commands and events in an event-sourced system.

## Overview

In complex event-driven systems, tracking the flow of messages is crucial for debugging, auditing, and understanding causal relationships. The `ICorrelatedMessage` interface provides a standard way to track correlation and causation across message flows, enabling developers to trace the complete path of a business transaction through the system. This is particularly important in event-sourced systems where commands lead to events which may trigger other commands, creating a chain of related messages that should be traceable.

## Interface Definition

```csharp
public interface ICorrelatedMessage : IMessage
{
    Guid MsgId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}
```

## Key Properties

- **MsgId**: A unique identifier for the message, ensuring each message can be individually identified
- **CorrelationId**: An identifier that groups related messages together, allowing for tracing of complete business transactions
- **CausationId**: The identifier of the message that directly caused this message, establishing a clear cause-effect relationship

## Correlation and Causation Concepts

### Correlation ID

The correlation ID tracks a business transaction across multiple messages. All messages that are part of the same logical transaction share the same correlation ID, even if they are processed by different components or services. This enables end-to-end tracing of business processes, which is invaluable for debugging and auditing in distributed systems.

### Causation ID

The causation ID establishes a direct cause-and-effect relationship between messages. It contains the message ID of the message that directly caused the current message to be created. This creates a chain of causality that can be followed to understand the exact sequence of events that led to a particular state.

## Message Flow Example

Consider the following message flow in a banking application:

1. A client sends a `CreateAccount` command (ID: A, CorrelationID: A, CausationID: A)
2. The command handler processes the command and the aggregate calls `RaiseEvent()` with an `AccountCreated` event (ID: B, CorrelationID: A, CausationID: A)
3. An event handler processes the event and sends a `SendWelcomeEmail` command (ID: C, CorrelationID: A, CausationID: B)
4. The email service processes the command and creates an `EmailSent` event (ID: D, CorrelationID: A, CausationID: C)

In this flow:
- All messages share the same correlation ID (A), indicating they are part of the same business transaction
- Each message's causation ID points to the ID of the message that directly caused it, creating a chain of causality
- The `MessageBuilder` class is used with the `RaiseEvent()` method to ensure proper correlation tracking
- This chain allows for complete tracing of the transaction from initiation to completion, which is invaluable for debugging and auditing

## Usage

### Implementing the Interface

Classes that implement `ICorrelatedMessage` must provide values for all three properties:

```csharp
public class CreateAccount : ICommand, ICorrelatedMessage
{
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    public readonly Guid AccountId;
    
    // Constructor for a new command that starts a correlation chain
    public CreateAccount(Guid accountId)
    {
        MsgId = Guid.NewGuid();
        CorrelationId = MsgId;  // Start a new correlation
        CausationId = MsgId;    // No previous cause
        AccountId = accountId;
    }
    
    // Constructor for a command within an existing correlation chain
    public CreateAccount(Guid accountId, Guid correlationId, Guid causationId)
    {
        MsgId = Guid.NewGuid();
        CorrelationId = correlationId;
        CausationId = causationId;
        AccountId = accountId;
    }
}
```

### Using MessageBuilder

The recommended way to create correlated messages is to use the `MessageBuilder` factory, which handles the correlation and causation IDs automatically:

```csharp
// Create a new message that starts a correlation chain
var createCommand = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid()));

// Create a message from an existing message (maintains correlation)
var createdEvent = MessageBuilder.From(createCommand, () => new AccountCreated(
    ((CreateAccount)createCommand).AccountId,
    "ACC-123",
    "John Doe"
));

// Create another message in the same chain
var sendEmailCommand = MessageBuilder.From(createdEvent, () => new SendWelcomeEmail(
    ((AccountCreated)createdEvent).AccountId,
    "john.doe@example.com"
));
```

### Propagating Correlation in Repositories

The `ICorrelatedRepository` interface extends the standard repository pattern to propagate correlation information, ensuring that all operations maintain the correlation chain:

```csharp
public interface ICorrelatedRepository
{
    // Save an aggregate, maintaining correlation from the source message
    void Save<T>(T aggregate, ICorrelatedMessage source) where T : AggregateRoot;
    
    // Retrieve an aggregate by ID, maintaining correlation from the source message
    T GetById<T>(Guid id, ICorrelatedMessage source) where T : AggregateRoot;
}

// Example usage
public class AccountService
{
    private readonly ICorrelatedRepository _repository;
    
    public AccountService(ICorrelatedRepository repository)
    {
        _repository = repository;
    }
    
    public void CreateAccount(CreateAccount command)
    {
        // Create a new account, passing the command for correlation
        var account = new Account(command.AccountId, command);
        
        // Save the account, maintaining correlation from the command
        _repository.Save(account, command);
    }
}
```

## Best Practices

1. **Always Use MessageBuilder**: Use the `MessageBuilder` factory to ensure proper correlation and avoid manual errors
2. **Preserve Correlation Chains**: Pass correlation information through the entire message flow to maintain traceability
3. **Log Correlation IDs**: Include correlation IDs in logs for easier debugging and troubleshooting
4. **Query by Correlation**: Support querying messages by correlation ID for auditing and analysis
5. **Consistent Implementation**: Ensure all messages in your system implement `ICorrelatedMessage` consistently
6. **Documentation**: Document the correlation flow in your system for better understanding
7. **Testing**: Test correlation chains to ensure they are maintained correctly
8. **Use with RaiseEvent()**: Always use `MessageBuilder` when creating events to be raised with the `RaiseEvent()` method
9. **Distributed Tracing**: Integrate with distributed tracing systems by including correlation IDs in trace contexts
10. **Cross-Service Correlation**: Ensure correlation IDs are preserved when messages cross service boundaries

## Common Pitfalls

1. **Manual ID Setting**: Avoid manually setting correlation and causation IDs as this is error-prone
2. **Breaking Correlation Chains**: Ensure correlation information is passed through all message flows, including external systems
3. **Reusing Message IDs**: Always generate new message IDs for each message to maintain uniqueness
4. **Ignoring Causation**: Track both correlation and causation for complete traceability
5. **Inconsistent Implementation**: Ensure all parts of your system handle correlation consistently
6. **Missing Correlation in Logs**: Without correlation IDs in logs, debugging becomes much harder

## Related Components

- [Command](./command.md): Base class for commands that implements `ICorrelatedMessage`
- [Event](./event.md): Base class for events that implements `ICorrelatedMessage`
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [ICorrelatedRepository](./icorrelated-repository.md): Repository that preserves correlation information
- [AggregateRoot](./aggregate-root.md): Domain entities that work with correlated messages
- [ReadModelBase](./read-model-base.md): Read models that are updated by event handlers processing correlated events

For a comprehensive view of how correlation works across components, see the [Key Component Relationships](../../architecture.md#key-component-relationships) section in the Architecture Guide, particularly the [Correlation and Causation Flow](../../architecture.md#correlation-and-causation-flow) diagram.

---

**Navigation**:
- [← Previous: Event](./event.md)
- [↑ Back to Top](#icorrelatedmessage)
- [→ Next: MessageBuilder](./message-builder.md)
