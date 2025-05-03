# ICorrelatedMessage

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ICorrelatedMessage` is a core interface in Reactive Domain that extends the base `IMessage` interface to add correlation and causation tracking capabilities.

## Overview

In complex event-driven systems, tracking the flow of messages is crucial for debugging, auditing, and understanding causal relationships. The `ICorrelatedMessage` interface provides a standard way to track correlation and causation across message flows.

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

- **MsgId**: A unique identifier for the message
- **CorrelationId**: An identifier that groups related messages together
- **CausationId**: The identifier of the message that caused this message

## Correlation and Causation Concepts

### Correlation ID

The correlation ID tracks a business transaction across multiple messages. All messages that are part of the same logical transaction share the same correlation ID, even if they are processed by different components or services.

### Causation ID

The causation ID establishes a direct cause-and-effect relationship between messages. It contains the message ID of the message that directly caused the current message to be created.

## Message Flow Example

Consider the following message flow:

1. A client sends a `CreateAccount` command (ID: A, CorrelationID: A, CausationID: A)
2. The command handler processes the command and creates an `AccountCreated` event (ID: B, CorrelationID: A, CausationID: A)
3. An event handler processes the event and sends a `SendWelcomeEmail` command (ID: C, CorrelationID: A, CausationID: B)
4. The email service processes the command and creates an `EmailSent` event (ID: D, CorrelationID: A, CausationID: C)

In this flow:
- All messages share the same correlation ID (A), indicating they are part of the same business transaction
- Each message's causation ID points to the ID of the message that caused it, creating a chain of causality

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
    
    public CreateAccount(Guid accountId)
    {
        MsgId = Guid.NewGuid();
        CorrelationId = MsgId;  // Start a new correlation
        CausationId = MsgId;    // No previous cause
        AccountId = accountId;
    }
    
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

The recommended way to create correlated messages is to use the `MessageBuilder` factory:

```csharp
// Create a new message that starts a correlation chain
var createCommand = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid()));

// Create a message from an existing message
var createdEvent = MessageBuilder.From(createCommand, () => new AccountCreated(
    ((CreateAccount)createCommand).AccountId
));
```

### Propagating Correlation in Repositories

The `ICorrelatedRepository` interface extends the standard repository pattern to propagate correlation information:

```csharp
public interface ICorrelatedRepository
{
    void Save<T>(T aggregate, ICorrelatedMessage source) where T : AggregateRoot;
    T GetById<T>(Guid id, ICorrelatedMessage source) where T : AggregateRoot;
}
```

## Best Practices

1. **Always Use MessageBuilder**: Use the `MessageBuilder` factory to ensure proper correlation
2. **Preserve Correlation Chains**: Pass correlation information through the entire message flow
3. **Log Correlation IDs**: Include correlation IDs in logs for easier debugging
4. **Query by Correlation**: Support querying messages by correlation ID for auditing

## Common Pitfalls

1. **Manual ID Setting**: Avoid manually setting correlation and causation IDs
2. **Breaking Correlation Chains**: Ensure correlation information is passed through all message flows
3. **Reusing Message IDs**: Always generate new message IDs for each message
4. **Ignoring Causation**: Track both correlation and causation for complete traceability

## Related Components

- [Command](./command.md): Base class for commands that implements `ICorrelatedMessage`
- [Event](./event.md): Base class for events that implements `ICorrelatedMessage`
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [ICorrelatedRepository](./icorrelated-repository.md): Repository that preserves correlation information

---

**Navigation**:
- [← Previous: Event](./event.md)
- [↑ Back to Top](#icorrelatedmessage)
- [→ Next: MessageBuilder](./message-builder.md)
