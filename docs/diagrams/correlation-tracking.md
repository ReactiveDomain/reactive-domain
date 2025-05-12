# Correlation and Causation Tracking Diagram

This diagram illustrates how correlation and causation IDs are propagated through a message chain in Reactive Domain.

## Message Flow with Correlation and Causation IDs

```mermaid
sequenceDiagram
    participant Client
    participant CommandHandler
    participant Aggregate
    participant Repository
    participant EventHandler
    participant EmailService
    
    Note over Client,EmailService: Each message has 3 IDs: MsgId, CorrelationId, CausationId

    Client->>CommandHandler: CreateAccount Command<br/>(MsgId: A, CorrelationId: A, CausationId: A)
    Note right of Client: New command starts a correlation chain<br/>All IDs are the same
    
    CommandHandler->>Aggregate: Create Account
    Aggregate->>Repository: Save with AccountCreated Event<br/>(MsgId: B, CorrelationId: A, CausationId: A)
    Note right of Aggregate: Event keeps the same CorrelationId<br/>CausationId points to the command
    
    Repository-->>EventHandler: AccountCreated Event<br/>(MsgId: B, CorrelationId: A, CausationId: A)
    EventHandler->>EmailService: SendWelcomeEmail Command<br/>(MsgId: C, CorrelationId: A, CausationId: B)
    Note right of EventHandler: New command keeps the same CorrelationId<br/>CausationId points to the event
    
    EmailService->>EmailService: Process Email
    EmailService-->>EventHandler: EmailSent Event<br/>(MsgId: D, CorrelationId: A, CausationId: C)
    Note right of EmailService: Event keeps the same CorrelationId<br/>CausationId points to the command
```

## Explanation

### Message IDs

Each message in Reactive Domain has three important identifiers:

1. **MsgId**: A unique identifier for the message itself
2. **CorrelationId**: An identifier that groups related messages together
3. **CausationId**: The identifier of the message that directly caused this message

### Correlation Chain

In the diagram above:

- All messages share the same **CorrelationId** (A), indicating they are part of the same business transaction
- Each message's **CausationId** points to the **MsgId** of the message that caused it, creating a chain of causality
- This chain allows for complete tracing of the transaction from initiation to completion

### Implementation with MessageBuilder

The recommended way to create correlated messages is to use the `MessageBuilder` factory:

```csharp
// Create a new message that starts a correlation chain
var createCommand = MessageBuilder.New(() => new CreateAccount(accountId));

// Create an event from the command (maintains correlation)
var createdEvent = MessageBuilder.From(createCommand, () => 
    new AccountCreated(accountId, "ACC-123", "John Doe"));

// Create another command from the event (maintains correlation)
var sendEmailCommand = MessageBuilder.From(createdEvent, () => 
    new SendWelcomeEmail(accountId, "john.doe@example.com"));
```

## Benefits

- **Debugging**: Easily trace related messages across system boundaries
- **Auditing**: Track the complete flow of a business transaction
- **Monitoring**: Group related operations for performance analysis
- **Error Handling**: Understand the context in which errors occur
