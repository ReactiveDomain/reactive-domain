# MessageBuilder

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`MessageBuilder` is a factory class in Reactive Domain that facilitates the creation of correlated messages, ensuring proper tracking of correlation and causation IDs across message flows.

## Overview

In event-sourced systems, tracking the flow of messages is crucial for debugging, auditing, and understanding causal relationships. The `MessageBuilder` factory provides a consistent way to create messages with properly set correlation and causation IDs.

## Class Definition

```csharp
public static class MessageBuilder
{
    public static TMessage New<TMessage>(Func<TMessage> messageFactory) 
        where TMessage : ICorrelatedMessage;
        
    public static TMessage From<TMessage>(ICorrelatedMessage source, Func<TMessage> messageFactory) 
        where TMessage : ICorrelatedMessage;
}
```

## Key Features

- **Message Creation**: Simplifies the creation of new messages with unique IDs
- **Correlation Tracking**: Automatically sets correlation IDs for tracking related messages
- **Causation Tracking**: Establishes causation links between messages
- **Type Safety**: Provides type-safe message creation through generic methods

## Usage

### Creating a New Message

To create a new message that starts a new correlation chain:

```csharp
// Create a new command with a new correlation ID
ICorrelatedMessage command = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid()));
```

### Creating a Message from an Existing Message

To create a message that continues an existing correlation chain:

```csharp
// Create a command with correlation information from an existing message
ICorrelatedMessage existingCommand = // ... existing command
ICorrelatedMessage newCommand = MessageBuilder.From(existingCommand, () => new DepositFunds(accountId, amount));
```

### In an Aggregate

Messages are often created within aggregates in response to commands:

```csharp
public class Account : AggregateRoot
{
    public Account(Guid id, ICorrelatedMessage source) : base(id)
    {
        // Create a new event from the source command
        Apply(MessageBuilder.From(source, () => new AccountCreated(id)));
    }
    
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        // Create a new event from the source command
        Apply(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
}
```

## Integration with ICorrelatedMessage

The `MessageBuilder` works with any message that implements the `ICorrelatedMessage` interface:

```csharp
public interface ICorrelatedMessage : IMessage
{
    Guid MsgId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}
```

When using `MessageBuilder.New()`, it sets:
- `MsgId` to a new GUID
- `CorrelationId` to the same value as `MsgId`
- `CausationId` to the same value as `MsgId`

When using `MessageBuilder.From()`, it sets:
- `MsgId` to a new GUID
- `CorrelationId` to the same value as the source message's `CorrelationId`
- `CausationId` to the same value as the source message's `MsgId`

## Best Practices

1. **Always Use MessageBuilder**: Consistently use `MessageBuilder` for creating correlated messages
2. **Preserve Correlation Chains**: Pass correlation information through the entire message flow
3. **Command-Event Flow**: Use `From()` to create events from commands
4. **Event-Command Flow**: Use `From()` to create commands from events in process managers

## Common Pitfalls

1. **Manual ID Setting**: Avoid manually setting correlation and causation IDs
2. **Breaking Correlation Chains**: Ensure correlation information is passed through all message flows
3. **Missing Source Messages**: Always provide a source message when continuing a correlation chain

## Related Components

- [ICorrelatedMessage](./icorrelated-message.md): Interface for messages with correlation information
- [Command](./command.md): Base class for commands that implements `ICorrelatedMessage`
- [Event](./event.md): Base class for events that implements `ICorrelatedMessage`
- [ICorrelatedRepository](./icorrelated-repository.md): Repository that preserves correlation information

---

**Navigation**:
- [← Previous: ICorrelatedMessage](./icorrelated-message.md)
- [↑ Back to Top](#messagebuilder)
- [→ Next: Command](./command.md)
