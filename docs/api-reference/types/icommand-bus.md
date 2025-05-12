# ICommandBus Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICommandBus` interface defines the contract for a component that routes commands to their appropriate handlers in a CQRS (Command Query Responsibility Segregation) architecture. It serves as a mediator between the clients that issue commands and the handlers that process them, decoupling the command senders from the command handlers.

In Reactive Domain, the command bus is a fundamental component that ensures commands are delivered to their handlers with proper correlation tracking, error handling, and routing.

## Command Bus in CQRS

In a CQRS architecture, commands represent intentions to change the state of the system. The command bus is responsible for:

1. **Command Routing**: Directing commands to the appropriate handlers
2. **Command Validation**: Ensuring commands are valid before processing
3. **Error Handling**: Managing exceptions that occur during command processing
4. **Correlation Tracking**: Maintaining correlation information across command flows
5. **Transaction Management**: Defining transaction boundaries for command processing

The command bus helps maintain a clean separation between the command issuers (clients, UI, API endpoints) and the command handlers (domain logic).

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface ICommandBus
{
    void Send<T>(T command) where T : class, ICommand;
    void Subscribe<T>(Action<T> handler) where T : class, ICommand;
    void Unsubscribe<T>(Action<T> handler) where T : class, ICommand;
}
```

## Methods

### Send\<T\>

Sends a command to its registered handler(s).

```csharp
void Send<T>(T command) where T : class, ICommand;
```

**Type Parameters**:
- `T`: The type of command to send. Must be a class that implements `ICommand`.

**Parameters**:
- `command` (`T`): The command to send.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `command` is `null`.
- `ReactiveDomain.NoHandlerException`: Thrown when no handler is registered for the command type.
- `ReactiveDomain.MultipleHandlersException`: Thrown when multiple handlers are registered for the command type.

**Remarks**: This method sends a command to its registered handler. In a typical CQRS implementation, each command type should have exactly one handler. The command bus ensures that the command is delivered to the appropriate handler.

**Example**:
```csharp
// Create a command
var createAccountCommand = new CreateAccount(Guid.NewGuid(), "12345", 1000);

// Send the command to its handler
commandBus.Send(createAccountCommand);
```

### Subscribe\<T\>

Subscribes a handler to a specific command type.

```csharp
void Subscribe<T>(Action<T> handler) where T : class, ICommand;
```

**Type Parameters**:
- `T`: The type of command to subscribe to. Must be a class that implements `ICommand`.

**Parameters**:
- `handler` (`System.Action<T>`): The handler to register for the command type.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `handler` is `null`.
- `ReactiveDomain.MultipleHandlersException`: Thrown when attempting to register multiple handlers for a command type that already has a handler.

**Remarks**: This method registers a handler for a specific command type. In a typical CQRS implementation, each command type should have exactly one handler. The command bus enforces this constraint by throwing an exception if multiple handlers are registered for the same command type.

**Example**:
```csharp
// Subscribe a handler for the CreateAccount command
commandBus.Subscribe<CreateAccount>(cmd => 
{
    // Create a new account
    var account = new Account(cmd.AccountId);
    account.CreateAccount(cmd.AccountNumber, cmd.InitialDeposit, cmd);
    
    // Save the account
    repository.Save(account);
});
```

### Unsubscribe\<T\>

Unsubscribes a handler from a specific command type.

```csharp
void Unsubscribe<T>(Action<T> handler) where T : class, ICommand;
```

**Type Parameters**:
- `T`: The type of command to unsubscribe from. Must be a class that implements `ICommand`.

**Parameters**:
- `handler` (`System.Action<T>`): The handler to unregister for the command type.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `handler` is `null`.

**Remarks**: This method unregisters a handler for a specific command type. It is typically used when a component is being disposed or when dynamic handler registration is required.

**Example**:
```csharp
// Define a handler
Action<CreateAccount> createAccountHandler = cmd => 
{
    // Handler logic
};

// Subscribe the handler
commandBus.Subscribe(createAccountHandler);

// Later, unsubscribe the handler
commandBus.Unsubscribe(createAccountHandler);
```

## Usage

The `ICommandBus` interface is typically used in conjunction with command handlers to implement the command side of a CQRS architecture. Here's a comprehensive example of using a command bus:

### Basic Command Bus Usage

```csharp
// Create a command bus
var commandBus = new CommandBus();

// Subscribe handlers
commandBus.Subscribe<CreateAccount>(HandleCreateAccount);
commandBus.Subscribe<DepositFunds>(HandleDepositFunds);
commandBus.Subscribe<WithdrawFunds>(HandleWithdrawFunds);
commandBus.Subscribe<CloseAccount>(HandleCloseAccount);

// Create and send a command
var createAccountCommand = new CreateAccount(Guid.NewGuid(), "12345", 1000);
commandBus.Send(createAccountCommand);

// Handler methods
void HandleCreateAccount(CreateAccount command)
{
    // Create a new account
    var account = new Account(command.AccountId);
    account.CreateAccount(command.AccountNumber, command.InitialDeposit, command);
    
    // Save the account
    repository.Save(account);
}

void HandleDepositFunds(DepositFunds command)
{
    // Get the account
    var account = repository.GetById<Account>(command.AccountId);
    
    // Process the command
    account.Deposit(command.Amount, command);
    
    // Save the account
    repository.Save(account);
}

// Additional handlers...
```

### Integration with Dependency Injection

```csharp
public class CommandHandlerRegistration
{
    private readonly ICommandBus _commandBus;
    private readonly IRepository _repository;
    
    public CommandHandlerRegistration(ICommandBus commandBus, IRepository repository)
    {
        _commandBus = commandBus;
        _repository = repository;
        
        // Register handlers
        RegisterHandlers();
    }
    
    private void RegisterHandlers()
    {
        _commandBus.Subscribe<CreateAccount>(HandleCreateAccount);
        _commandBus.Subscribe<DepositFunds>(HandleDepositFunds);
        _commandBus.Subscribe<WithdrawFunds>(HandleWithdrawFunds);
        _commandBus.Subscribe<CloseAccount>(HandleCloseAccount);
    }
    
    private void HandleCreateAccount(CreateAccount command)
    {
        // Implementation...
    }
    
    // Additional handlers...
    
    public void Dispose()
    {
        // Unregister handlers
        _commandBus.Unsubscribe<CreateAccount>(HandleCreateAccount);
        _commandBus.Unsubscribe<DepositFunds>(HandleDepositFunds);
        _commandBus.Unsubscribe<WithdrawFunds>(HandleWithdrawFunds);
        _commandBus.Unsubscribe<CloseAccount>(HandleCloseAccount);
    }
}
```

### Using Command Bus with Correlation

```csharp
public class CorrelatedCommandHandler
{
    private readonly ICommandBus _commandBus;
    private readonly ICorrelatedRepository _repository;
    
    public CorrelatedCommandHandler(ICommandBus commandBus, ICorrelatedRepository repository)
    {
        _commandBus = commandBus;
        _repository = repository;
    }
    
    public void HandleTransferFunds(TransferFunds command)
    {
        // Load accounts with correlation
        var sourceAccount = _repository.GetById<Account>(command.SourceAccountId, command);
        var targetAccount = _repository.GetById<Account>(command.TargetAccountId, command);
        
        // Perform transfer
        sourceAccount.Withdraw(command.Amount, command);
        targetAccount.Deposit(command.Amount, command);
        
        // Save accounts
        _repository.Save(sourceAccount);
        _repository.Save(targetAccount);
        
        // Send notification command with correlation
        var notificationCommand = MessageBuilder.From(command, () => 
            new SendTransferNotification(command.SourceAccountId, command.TargetAccountId, command.Amount));
            
        _commandBus.Send(notificationCommand);
    }
}
```

## Best Practices

1. **Single Handler Per Command**: Ensure each command type has exactly one handler
2. **Command Validation**: Validate commands before sending them to the command bus
3. **Error Handling**: Implement proper error handling in command handlers
4. **Correlation Tracking**: Use correlated commands to maintain traceability
5. **Command Idempotency**: Design commands to be idempotent when possible
6. **Transaction Boundaries**: Consider command handlers as transaction boundaries
7. **Command Naming**: Use verb-noun naming for commands (e.g., `CreateAccount`, `TransferFunds`)
8. **Command Immutability**: Make commands immutable to prevent unintended side effects
9. **Command Versioning**: Plan for command versioning to handle schema evolution
10. **Command Logging**: Log commands for auditing and debugging purposes

## Common Pitfalls

1. **Multiple Handlers**: Registering multiple handlers for the same command type
2. **Missing Handlers**: Sending commands that have no registered handlers
3. **Command Side Effects**: Performing side effects in command handlers that are not part of the transaction
4. **Command Coupling**: Coupling commands to their handlers, reducing flexibility
5. **Large Commands**: Creating commands with too many properties or responsibilities
6. **Missing Validation**: Not validating commands before processing them
7. **Ignoring Errors**: Not properly handling exceptions in command handlers
8. **Command Bus Overuse**: Using the command bus for queries or notifications

## Advanced Scenarios

### Command Validation

Implementing command validation before sending:

```csharp
public class ValidatingCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly ICommandValidator _validator;
    
    public ValidatingCommandBus(ICommandBus innerBus, ICommandValidator validator)
    {
        _innerBus = innerBus;
        _validator = validator;
    }
    
    public void Send<T>(T command) where T : class, ICommand
    {
        // Validate the command
        var validationResult = _validator.Validate(command);
        
        if (!validationResult.IsValid)
        {
            throw new CommandValidationException(validationResult.Errors);
        }
        
        // Send the command
        _innerBus.Send(command);
    }
    
    // Implement other methods...
}
```

### Command Logging

Adding logging to the command bus:

```csharp
public class LoggingCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly ILogger _logger;
    
    public LoggingCommandBus(ICommandBus innerBus, ILogger logger)
    {
        _innerBus = innerBus;
        _logger = logger;
    }
    
    public void Send<T>(T command) where T : class, ICommand
    {
        try
        {
            _logger.LogInformation("Sending command {CommandType} with ID {CommandId}", 
                typeof(T).Name, 
                command is ICorrelatedMessage msg ? msg.MsgId : Guid.Empty);
                
            _innerBus.Send(command);
            
            _logger.LogInformation("Command {CommandType} processed successfully", 
                typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {CommandType}", 
                typeof(T).Name);
                
            throw;
        }
    }
    
    // Implement other methods...
}
```

## Related Components

- [Command](command.md): Base class for commands in Reactive Domain
- [ICommand](icommand.md): Interface for commands in Reactive Domain
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [MessageBuilder](message-builder.md): Factory for creating correlated messages
- [IRepository](irepository.md): Interface for repositories that store and retrieve aggregates
- [ICorrelatedRepository](icorrelated-repository.md): Repository with correlation support
- [IEventBus](ievent-bus.md): Interface for publishing events

---

**Navigation**:
- [← Previous: ICorrelatedRepository](./icorrelated-repository.md)
- [↑ Back to Top](#icommandbus-interface)
- [→ Next: Command](./command.md)
