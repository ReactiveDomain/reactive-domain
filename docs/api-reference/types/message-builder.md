# MessageBuilder

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`MessageBuilder` is a factory class in Reactive Domain that facilitates the creation of correlated messages, ensuring proper tracking of correlation and causation IDs across message flows.

## Overview

In event-sourced systems, tracking the flow of messages is crucial for debugging, auditing, and understanding causal relationships. The `MessageBuilder` factory provides a consistent way to create messages with properly set correlation and causation IDs.

Correlation tracking is essential in distributed systems where a single business transaction might span multiple services, processes, or message handlers. The `MessageBuilder` ensures that all messages related to the same business transaction are properly linked, making it possible to trace the entire transaction flow.

## Class Definition

```csharp
public static class MessageBuilder
{
    /// <summary>
    /// Creates a new message with a new correlation chain.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to create.</typeparam>
    /// <param name="messageFactory">A factory function that creates the message.</param>
    /// <returns>A new message with a new correlation chain.</returns>
    public static TMessage New<TMessage>(Func<TMessage> messageFactory) 
        where TMessage : ICorrelatedMessage;
        
    /// <summary>
    /// Creates a new message that continues an existing correlation chain.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to create.</typeparam>
    /// <param name="source">The source message that this message is derived from.</param>
    /// <param name="messageFactory">A factory function that creates the message.</param>
    /// <returns>A new message that continues the correlation chain from the source message.</returns>
    public static TMessage From<TMessage>(ICorrelatedMessage source, Func<TMessage> messageFactory) 
        where TMessage : ICorrelatedMessage;
}
```

## Key Features

- **Message Creation**: Simplifies the creation of new messages with unique IDs
- **Correlation Tracking**: Automatically sets correlation IDs for tracking related messages
- **Causation Tracking**: Establishes causation links between messages
- **Type Safety**: Provides type-safe message creation through generic methods
- **Consistent ID Management**: Ensures consistent handling of message, correlation, and causation IDs
- **Debugging Support**: Makes it easier to debug complex message flows by maintaining clear relationships

## How It Works

The `MessageBuilder` class manages three important IDs for each message:

1. **Message ID (`MsgId`)**: A unique identifier for the message itself
2. **Correlation ID (`CorrelationId`)**: An identifier that links all messages in the same business transaction
3. **Causation ID (`CausationId`)**: An identifier that links a message to the message that caused it

When you create a new message using `MessageBuilder.New()`:
- A new `MsgId` is generated
- The `CorrelationId` is set to the same value as the `MsgId`
- The `CausationId` is set to the same value as the `MsgId`

When you create a message from an existing message using `MessageBuilder.From()`:
- A new `MsgId` is generated
- The `CorrelationId` is copied from the source message
- The `CausationId` is set to the `MsgId` of the source message

This approach ensures that all messages in the same business transaction share the same `CorrelationId`, and each message's `CausationId` points to the message that directly caused it.

## Usage Examples

### Creating a New Message Chain

To create a new message that starts a new correlation chain:

```csharp
// Create a new command with a new correlation ID
ICorrelatedMessage command = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid()));

// The resulting command will have:
// - A new MsgId
// - CorrelationId equal to MsgId
// - CausationId equal to MsgId
```

### Creating a Message from an Existing Message

To create a message that continues an existing correlation chain:

```csharp
// Create a command with correlation information from an existing message
ICorrelatedMessage existingCommand = // ... existing command
ICorrelatedMessage newCommand = MessageBuilder.From(existingCommand, () => new DepositFunds(accountId, amount));

// The resulting command will have:
// - A new MsgId
// - CorrelationId equal to existingCommand.CorrelationId
// - CausationId equal to existingCommand.MsgId
```

### Complete Message Flow Example

Here's a complete example showing how correlation and causation IDs flow through a system:

```csharp
// 1. Client sends a command
var createAccountCommand = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid(), "John Doe", 100.00m));
// MsgId: A, CorrelationId: A, CausationId: A

// 2. Command handler processes the command and creates an aggregate
public void Handle(CreateAccount command)
{
    var account = new Account(command.AccountId, command);
    _repository.Save(account, command);
}

// 3. Aggregate applies an event
public Account(Guid id, ICorrelatedMessage source) : base(id)
{
    RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(id, source.CustomerName, source.InitialBalance)));
    // Event MsgId: B, CorrelationId: A, CausationId: A
}

// 4. Event handler processes the event and sends a notification command
public void Handle(AccountCreated @event)
{
    var notifyCommand = MessageBuilder.From(@event, () => new SendWelcomeEmail(@event.CustomerId, @event.CustomerEmail));
    // Command MsgId: C, CorrelationId: A, CausationId: B
    _commandBus.Send(notifyCommand);
    
    // Update read model
    var accountSummary = new AccountSummary(@event.AccountId);
    accountSummary.Update(@event.AccountNumber, @event.CustomerName, @event.InitialBalance);
    _readModelRepository.Save(accountSummary);
}

// 5. Notification handler processes the command and creates an event
public void Handle(SendWelcomeEmail command)
{
    // Send email...
    
    var emailSentEvent = MessageBuilder.From(command, () => new WelcomeEmailSent(command.CustomerId));
    // Event MsgId: D, CorrelationId: A, CausationId: C
    _eventBus.Publish(emailSentEvent);
}
```

In this flow:
- All messages share the same correlation ID (A)
- Each message's causation ID points to the message that directly caused it
- The complete chain of causality is: CreateAccount → AccountCreated → SendWelcomeEmail → WelcomeEmailSent

### In an Aggregate

Messages are often created within aggregates in response to commands:

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    private string _ownerName;
    
    public Account(Guid id, ICorrelatedMessage source) : base(id)
    {
        // Create a new event from the source command
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(id, ((CreateAccount)source).CustomerName)));
    }
    
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Cannot deposit to inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Create a new event from the source command
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
    
    public void Withdraw(decimal amount, ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Cannot withdraw from inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        // Create a new event from the source command
        RaiseEvent(MessageBuilder.From(source, () => new FundsWithdrawn(Id, amount)));
    }
    
    public void Close(ICorrelatedMessage source)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account already closed");
            
        // Create a new event from the source command
        RaiseEvent(MessageBuilder.From(source, () => new AccountClosed(Id)));
    }
    
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _ownerName = @event.CustomerName;
        _balance = 0;
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
    
    private void Apply(AccountClosed @event)
    {
        _isActive = false;
    }
}
```

### In a Process Manager

Process managers (also known as sagas) often need to coordinate multiple steps in a business process. The `MessageBuilder` is essential for maintaining correlation across these steps:

```csharp
public class AccountOpeningProcess : 
    IEventHandler<AccountCreated>,
    IEventHandler<CustomerVerified>,
    IEventHandler<WelcomeEmailSent>
{
    private readonly ICommandBus _commandBus;
    private readonly IProcessRepository _processRepository;
    
    public AccountOpeningProcess(ICommandBus commandBus, IProcessRepository processRepository)
    {
        _commandBus = commandBus;
        _processRepository = processRepository;
    }
    
    public void Handle(AccountCreated @event)
    {
        // Start the process
        var process = new AccountOpeningProcessState(@event.AccountId);
        process.AccountCreated = true;
        _processRepository.Save(process);
        
        // Request customer verification
        var verifyCommand = MessageBuilder.From(@event, () => new VerifyCustomer(@event.CustomerId));
        _commandBus.Send(verifyCommand);
    }
    
    public void Handle(CustomerVerified @event)
    {
        // Update process state
        var process = _processRepository.GetByCorrelationId(@event.CorrelationId);
        if (process == null || !process.AccountCreated)
            return; // Process not found or not in the right state
            
        process.CustomerVerified = true;
        _processRepository.Save(process);
        
        // Send welcome email
        var emailCommand = MessageBuilder.From(@event, () => new SendWelcomeEmail(@event.CustomerId, @event.CustomerEmail));
        _commandBus.Send(emailCommand);
    }
    
    public void Handle(WelcomeEmailSent @event)
    {
        // Update process state
        var process = _processRepository.GetByCorrelationId(@event.CorrelationId);
        if (process == null || !process.CustomerVerified)
            return; // Process not found or not in the right state
            
        process.WelcomeEmailSent = true;
        _processRepository.Save(process);
        
        // Complete the process if all steps are done
        if (process.IsComplete())
        {
            var completeCommand = MessageBuilder.From(@event, () => new CompleteAccountOpening(process.AccountId));
            _commandBus.Send(completeCommand);
        }
    }
}

public class AccountOpeningProcessState
{
    public Guid AccountId { get; }
    public Guid CorrelationId { get; }
    public bool AccountCreated { get; set; }
    public bool CustomerVerified { get; set; }
    public bool WelcomeEmailSent { get; set; }
    
    public AccountOpeningProcessState(Guid accountId, Guid correlationId)
    {
        AccountId = accountId;
        CorrelationId = correlationId;
    }
    
    public bool IsComplete()
    {
        return AccountCreated && CustomerVerified && WelcomeEmailSent;
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

## Debugging with Correlation IDs

Correlation IDs are particularly valuable for debugging distributed systems. By including correlation IDs in logs, you can trace the entire flow of a business transaction:

```csharp
public class CorrelatedLogger : ILogger
{
    private readonly ILogger _innerLogger;
    
    public CorrelatedLogger(ILogger innerLogger)
    {
        _innerLogger = innerLogger;
    }
    
    public void Log(LogLevel level, string message, ICorrelatedMessage correlatedMessage)
    {
        var correlatedMessage = $"[CorrelationId: {correlatedMessage.CorrelationId}, CausationId: {correlatedMessage.CausationId}] {message}";
        _innerLogger.Log(level, correlatedMessage);
    }
}

// Usage in a command handler
public void Handle(CreateAccount command)
{
    _logger.Log(LogLevel.Info, "Handling CreateAccount command", command);
    
    try
    {
        var account = new Account(command.AccountId, command);
        _repository.Save(account, command);
        _logger.Log(LogLevel.Info, "Account created successfully", command);
    }
    catch (Exception ex)
    {
        _logger.Log(LogLevel.Error, $"Error creating account: {ex.Message}", command);
        throw;
    }
}
```

## Best Practices

1. **Always Use MessageBuilder**: Consistently use `MessageBuilder` for creating correlated messages
2. **Preserve Correlation Chains**: Pass correlation information through the entire message flow
3. **Command-Event Flow**: Use `From()` to create events from commands
4. **Event-Command Flow**: Use `From()` to create commands from events in process managers
5. **Include Correlation IDs in Logs**: Add correlation and causation IDs to log messages
6. **Correlation-Aware Repositories**: Use repositories that preserve correlation information
7. **Consistent Naming**: Use consistent naming for correlation-related concepts
8. **Documentation**: Document the correlation flow in complex business processes
9. **Testing**: Test correlation chains to ensure they're maintained correctly
10. **Monitoring**: Monitor correlation chains for breaks or inconsistencies

## Common Pitfalls

1. **Manual ID Setting**: Avoid manually setting correlation and causation IDs
2. **Breaking Correlation Chains**: Ensure correlation information is passed through all message flows
3. **Missing Source Messages**: Always provide a source message when continuing a correlation chain
4. **Correlation Leakage**: Be careful not to mix correlation chains from different business transactions
5. **Overloading Correlation**: Don't use correlation IDs for purposes other than tracking message flow
6. **Ignoring Causation**: Track both correlation and causation for complete traceability
7. **Performance Concerns**: Be aware of the overhead of tracking correlation in high-throughput systems
8. **Missing Correlation in Logs**: Ensure logs include correlation information for effective debugging
9. **Inconsistent Implementation**: Use `MessageBuilder` consistently throughout the system
10. **Lack of Documentation**: Document correlation flows for complex business processes

## Related Components

- [ICorrelatedMessage](./icorrelated-message.md): Interface for messages with correlation information
- [Command](./command.md): Base class for commands that implements `ICorrelatedMessage`
- [Event](./event.md): Base class for events that implements `ICorrelatedMessage`
- [ICorrelatedRepository](./icorrelated-repository.md): Repository that preserves correlation information
- [AggregateRoot](./aggregate-root.md): Base class for domain aggregates that work with correlated messages
- [ReadModelBase](./read-model-base.md): Base class for read models that are updated by correlated events

For a comprehensive view of how these components interact, see the [Key Component Relationships](../../architecture.md#key-component-relationships) section in the Architecture Guide, particularly the [MessageBuilder's Role](../../architecture.md#messagebuilders-role) and [Correlation and Causation Flow](../../architecture.md#correlation-and-causation-flow) diagrams.

---

**Navigation**:
- [← Previous: ICorrelatedMessage](./icorrelated-message.md)
- [↑ Back to Top](#messagebuilder)
- [→ Next: Command](./command.md)
