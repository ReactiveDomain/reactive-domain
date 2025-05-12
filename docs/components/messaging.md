# ReactiveDomain.Messaging

[← Back to Components](README.md) | [← Back to Table of Contents](../README.md)

**Component Navigation**: [← ReactiveDomain.Foundation](foundation.md) | [Next: ReactiveDomain.Persistence →](persistence.md)

The `ReactiveDomain.Messaging` component provides a comprehensive messaging framework for handling commands, events, and queries in Reactive Domain applications. It implements the messaging infrastructure that connects different parts of your application.

## Table of Contents

- [Purpose and Responsibility](#purpose-and-responsibility)
- [Key Interfaces and Classes](#key-interfaces-and-classes)
  - [IMessage, ICommand, IEvent](#imessage-icommand-ievent)
  - [IMessageHandler, ICommandHandler, IEventHandler](#imessagehandler-icommandhandler-ieventhandler)
  - [IMessageBus, ICommandBus, IEventBus](#imessagebus-icommandbus-ieventbus)
- [Implementation Details](#implementation-details)
- [Usage Examples](#usage-examples)
  - [Defining Messages](#defining-messages)
  - [Implementing Handlers](#implementing-handlers)
  - [Using Message Buses](#using-message-buses)
- [Integration with Other Components](#integration-with-other-components)
- [Best Practices](#best-practices)
- [Common Pitfalls](#common-pitfalls)

## Purpose and Responsibility

The primary purpose of the `ReactiveDomain.Messaging` component is to provide a messaging infrastructure for event-sourced applications, including:

- Message definitions for commands, events, and queries
- Message handlers for processing different types of messages
- Message buses for routing messages to handlers
- Support for correlation and causation tracking
- Message serialization and deserialization

This component enables the implementation of the Command Query Responsibility Segregation (CQRS) pattern by providing separate channels for commands (write operations) and queries (read operations).

## Key Interfaces and Classes

### IMessage, ICommand, IEvent

These interfaces define the contract for different types of messages in the system:

```csharp
public interface IMessage
{
    Guid MsgId { get; }
}

public interface ICommand : IMessage
{
}

public interface IEvent : IMessage
{
}

public interface IQuery : IMessage
{
}
```

**Key Features:**

- **IMessage**: Base interface for all messages with a unique identifier
- **ICommand**: Represents a request to perform an action
- **IEvent**: Represents something that has happened
- **IQuery**: Represents a request for information

### IMessageHandler, ICommandHandler, IEventHandler

These interfaces define the contract for message handlers:

```csharp
public interface IMessageHandler<in TMessage> where TMessage : IMessage
{
    void Handle(TMessage message);
}

public interface ICommandHandler<in TCommand> : IMessageHandler<TCommand> where TCommand : ICommand
{
}

public interface IEventHandler<in TEvent> : IMessageHandler<TEvent> where TEvent : IEvent
{
}

public interface IQueryHandler<in TQuery, out TResult> where TQuery : IQuery
{
    TResult Handle(TQuery query);
}
```

**Key Features:**

- **IMessageHandler**: Generic handler for any message type
- **ICommandHandler**: Specialized handler for commands
- **IEventHandler**: Specialized handler for events
- **IQueryHandler**: Specialized handler for queries that returns a result

### IMessageBus, ICommandBus, IEventBus

These interfaces define the contract for message buses:

```csharp
public interface IMessageBus
{
    void Subscribe<TMessage>(IMessageHandler<TMessage> handler) where TMessage : IMessage;
    void Unsubscribe<TMessage>(IMessageHandler<TMessage> handler) where TMessage : IMessage;
    void Publish<TMessage>(TMessage message) where TMessage : IMessage;
}

public interface ICommandBus
{
    void Subscribe<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;
    void Unsubscribe<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;
    void Send<TCommand>(TCommand command) where TCommand : ICommand;
}

public interface IEventBus
{
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Publish<TEvent>(TEvent @event) where TEvent : IEvent;
}
```

**Key Features:**

- **IMessageBus**: Generic bus for any message type
- **ICommandBus**: Specialized bus for commands
- **IEventBus**: Specialized bus for events
- **Subscribe/Unsubscribe**: Register and unregister handlers
- **Send/Publish**: Route messages to appropriate handlers

## Implementation Details

The `ReactiveDomain.Messaging` component is built on several key design principles:

- **Message-Based Communication**: All communication is done through messages
- **Strong Typing**: Messages and handlers are strongly typed
- **Loose Coupling**: Components communicate through message buses without direct dependencies
- **Single Responsibility**: Each handler has a single responsibility

The component provides several implementations of the message bus interfaces:

- **InProcessMessageBus**: Routes messages within a single process
- **DistributedMessageBus**: Routes messages across process boundaries
- **CorrelatedMessageBus**: Adds correlation and causation tracking to messages

## Usage Examples

### Defining Messages

```csharp
public class CreateAccount : ICommand
{
    public Guid MsgId { get; }
    public readonly Guid AccountId;
    
    public CreateAccount(Guid accountId)
    {
        MsgId = Guid.NewGuid();
        AccountId = accountId;
    }
}

public class AccountCreated : IEvent
{
    public Guid MsgId { get; }
    public readonly Guid AccountId;
    
    public AccountCreated(Guid accountId)
    {
        MsgId = Guid.NewGuid();
        AccountId = accountId;
    }
}

public class GetAccountBalance : IQuery
{
    public Guid MsgId { get; }
    public readonly Guid AccountId;
    
    public GetAccountBalance(Guid accountId)
    {
        MsgId = Guid.NewGuid();
        AccountId = accountId;
    }
}

public class AccountBalanceResult
{
    public readonly Guid AccountId;
    public readonly decimal Balance;
    
    public AccountBalanceResult(Guid accountId, decimal balance)
    {
        AccountId = accountId;
        Balance = balance;
    }
}
```

### Implementing Handlers

```csharp
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly IRepository _repository;
    
    public CreateAccountHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccount command)
    {
        var account = new Account(command.AccountId);
        _repository.Save(account);
    }
}

public class AccountCreatedHandler : IEventHandler<AccountCreated>
{
    private readonly IReadModelRepository<AccountBalance> _readModelRepository;
    
    public AccountCreatedHandler(IReadModelRepository<AccountBalance> readModelRepository)
    {
        _readModelRepository = readModelRepository;
    }
    
    public void Handle(AccountCreated @event)
    {
        var accountBalance = new AccountBalance(@event.AccountId, 0);
        _readModelRepository.Save(accountBalance);
    }
}

public class GetAccountBalanceHandler : IQueryHandler<GetAccountBalance, AccountBalanceResult>
{
    private readonly IReadModelRepository<AccountBalance> _readModelRepository;
    
    public GetAccountBalanceHandler(IReadModelRepository<AccountBalance> readModelRepository)
    {
        _readModelRepository = readModelRepository;
    }
    
    public AccountBalanceResult Handle(GetAccountBalance query)
    {
        var accountBalance = _readModelRepository.GetById(query.AccountId);
        if (accountBalance == null)
            throw new InvalidOperationException("Account not found");
            
        return new AccountBalanceResult(accountBalance.Id, accountBalance.Balance);
    }
}
```

### Using Message Buses

```csharp
// Create message buses
var commandBus = new InProcessCommandBus();
var eventBus = new InProcessEventBus();
var queryBus = new InProcessQueryBus();

// Register handlers
commandBus.Subscribe(new CreateAccountHandler(repository));
eventBus.Subscribe(new AccountCreatedHandler(readModelRepository));
queryBus.Subscribe(new GetAccountBalanceHandler(readModelRepository));

// Send a command
var accountId = Guid.NewGuid();
commandBus.Send(new CreateAccount(accountId));

// Publish an event
eventBus.Publish(new AccountCreated(accountId));

// Send a query
var result = queryBus.Send<GetAccountBalance, AccountBalanceResult>(new GetAccountBalance(accountId));
Console.WriteLine($"Account balance: {result.Balance}");
```

## Integration with Other Components

The `ReactiveDomain.Messaging` component integrates with several other components in the Reactive Domain library:

- **ReactiveDomain.Core**: Uses the core interfaces for event sourcing
- **ReactiveDomain.Foundation**: Integrates with aggregates and repositories
- **ReactiveDomain.Persistence**: Uses the persistence layer for event storage
- **ReactiveDomain.Transport**: Uses the transport layer for distributed messaging

## Best Practices

When working with the `ReactiveDomain.Messaging` component:

1. **Keep messages simple**: Messages should be simple data structures with no behavior
2. **Single responsibility handlers**: Each handler should have a single responsibility
3. **Use correlation**: Use correlation and causation tracking for complex workflows
4. **Handle failures gracefully**: Implement error handling and retry strategies
5. **Avoid circular dependencies**: Be careful not to create circular dependencies between handlers

## Common Pitfalls

Some common issues to avoid when working with the `ReactiveDomain.Messaging` component:

1. **Mutable messages**: Ensure messages are immutable
2. **Complex handlers**: Keep handlers simple and focused
3. **Missing error handling**: Always handle errors in handlers
4. **Tight coupling**: Avoid direct dependencies between handlers
5. **Synchronous processing bottlenecks**: Consider asynchronous processing for long-running operations

---

**Component Navigation**:
- [← Previous: ReactiveDomain.Foundation](foundation.md)
- [↑ Back to Top](#reactivedomainmessaging)
- [Next: ReactiveDomain.Persistence →](persistence.md)
