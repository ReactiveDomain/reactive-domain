# ICommandHandler Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICommandHandler<T>` interface defines the contract for components that handle commands in Reactive Domain. Command handlers are responsible for processing commands, validating business rules, and applying the requested changes to the domain model.

In a CQRS (Command Query Responsibility Segregation) architecture, command handlers play a crucial role in the write side of the system, processing commands that express the intent to change the system's state.

## Command Handlers in CQRS

In a CQRS architecture, command handlers serve several important purposes:

1. **Command Processing**: Executing the business logic associated with commands
2. **Business Rule Validation**: Enforcing business rules and validations
3. **Domain Model Interaction**: Interacting with domain models to apply changes
4. **Transaction Boundaries**: Defining transaction boundaries for command processing
5. **Error Handling**: Managing exceptions and errors during command processing

Command handlers are typically registered with a command bus, which routes commands to their appropriate handlers.

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface ICommandHandler<in T> where T : class, ICommand
{
    void Handle(T command);
}
```

## Type Parameters

- `T`: The type of command to handle. Must be a class that implements `ICommand`.

## Methods

### Handle

Handles a command of the specified type.

```csharp
void Handle(T command);
```

**Parameters**:
- `command` (`T`): The command to handle.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `command` is `null`.
- `System.InvalidOperationException`: Thrown when the command cannot be processed due to a business rule violation.
- `System.UnauthorizedAccessException`: Thrown when the current user is not authorized to execute the command.

**Remarks**: This method processes a command of the specified type. It typically validates the command, applies the requested changes to the domain model, and persists the changes.

**Example**:
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
        // Validate the command
        if (string.IsNullOrEmpty(command.AccountNumber))
            throw new ArgumentException("Account number is required");
            
        if (command.InitialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative");
            
        // Check if account already exists
        Account account;
        if (_repository.TryGetById(command.AccountId, out account))
            throw new InvalidOperationException($"Account {command.AccountId} already exists");
            
        // Create new account
        account = new Account(command.AccountId);
        account.CreateAccount(command.AccountNumber, command.InitialDeposit, command);
        
        // Save the account
        _repository.Save(account);
    }
}
```

## Usage

The `ICommandHandler<T>` interface is typically used to implement handlers for specific command types. Here's a comprehensive example of using command handlers in a CQRS architecture:

### Basic Command Handler Implementation

```csharp
// Define a command
public class CreateAccount : Command
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public decimal InitialDeposit { get; }
    
    public CreateAccount(Guid accountId, string accountNumber, decimal initialDeposit)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        InitialDeposit = initialDeposit;
    }
}

// Implement a command handler
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly IRepository _repository;
    private readonly IEventBus _eventBus;
    
    public CreateAccountHandler(IRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(CreateAccount command)
    {
        // Validate the command
        if (string.IsNullOrEmpty(command.AccountNumber))
            throw new ArgumentException("Account number is required");
            
        if (command.InitialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative");
            
        // Check if account already exists
        Account account;
        if (_repository.TryGetById(command.AccountId, out account))
            throw new InvalidOperationException($"Account {command.AccountId} already exists");
            
        // Create new account
        account = new Account(command.AccountId);
        account.CreateAccount(command.AccountNumber, command.InitialDeposit, command);
        
        // Save the account
        _repository.Save(account);
        
        // Publish events
        foreach (var @event in account.TakeEvents())
        {
            _eventBus.Publish(@event);
        }
    }
}
```

### Integration with Command Bus

```csharp
// Create a command bus
var commandBus = new CommandBus();

// Create a command handler
var createAccountHandler = new CreateAccountHandler(repository, eventBus);

// Register the handler with the command bus
commandBus.Subscribe<CreateAccount>(createAccountHandler.Handle);

// Create and send a command
var createAccountCommand = new CreateAccount(Guid.NewGuid(), "12345", 1000);
commandBus.Send(createAccountCommand);
```

### Multiple Command Handlers in a Single Class

```csharp
public class AccountCommandHandler : 
    ICommandHandler<CreateAccount>,
    ICommandHandler<DepositFunds>,
    ICommandHandler<WithdrawFunds>,
    ICommandHandler<CloseAccount>
{
    private readonly IRepository _repository;
    private readonly IEventBus _eventBus;
    
    public AccountCommandHandler(IRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(CreateAccount command)
    {
        // Implementation for CreateAccount command
    }
    
    public void Handle(DepositFunds command)
    {
        // Validate the command
        if (command.Amount <= 0)
            throw new ArgumentException("Deposit amount must be greater than zero");
            
        // Get the account
        var account = _repository.GetById<Account>(command.AccountId);
        
        // Process the command
        account.Deposit(command.Amount, command);
        
        // Save the account
        _repository.Save(account);
        
        // Publish events
        foreach (var @event in account.TakeEvents())
        {
            _eventBus.Publish(@event);
        }
    }
    
    public void Handle(WithdrawFunds command)
    {
        // Implementation for WithdrawFunds command
    }
    
    public void Handle(CloseAccount command)
    {
        // Implementation for CloseAccount command
    }
}
```

### Dependency Injection Registration

```csharp
// Register command handlers with the dependency injection container
services.AddTransient<ICommandHandler<CreateAccount>, CreateAccountHandler>();
services.AddTransient<ICommandHandler<DepositFunds>, DepositFundsHandler>();
services.AddTransient<ICommandHandler<WithdrawFunds>, WithdrawFundsHandler>();
services.AddTransient<ICommandHandler<CloseAccount>, CloseAccountHandler>();

// Register the command bus
services.AddSingleton<ICommandBus>(provider => 
{
    var commandBus = new CommandBus();
    
    // Register handlers with the command bus
    commandBus.Subscribe<CreateAccount>(provider.GetRequiredService<ICommandHandler<CreateAccount>>().Handle);
    commandBus.Subscribe<DepositFunds>(provider.GetRequiredService<ICommandHandler<DepositFunds>>().Handle);
    commandBus.Subscribe<WithdrawFunds>(provider.GetRequiredService<ICommandHandler<WithdrawFunds>>().Handle);
    commandBus.Subscribe<CloseAccount>(provider.GetRequiredService<ICommandHandler<CloseAccount>>().Handle);
    
    return commandBus;
});
```

## Command Handler Patterns

### Transaction Script Pattern

The Transaction Script pattern implements the command handling logic directly in the handler:

```csharp
public class TransferFundsHandler : ICommandHandler<TransferFunds>
{
    private readonly IRepository _repository;
    private readonly IEventBus _eventBus;
    
    public TransferFundsHandler(IRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(TransferFunds command)
    {
        // Validate the command
        if (command.Amount <= 0)
            throw new ArgumentException("Transfer amount must be greater than zero");
            
        if (command.SourceAccountId == command.TargetAccountId)
            throw new ArgumentException("Source and target accounts cannot be the same");
            
        // Get the accounts
        var sourceAccount = _repository.GetById<Account>(command.SourceAccountId);
        var targetAccount = _repository.GetById<Account>(command.TargetAccountId);
        
        // Check if the source account has sufficient funds
        if (sourceAccount.GetBalance() < command.Amount)
            throw new InvalidOperationException("Insufficient funds for transfer");
            
        // Perform the transfer
        sourceAccount.Withdraw(command.Amount, command);
        targetAccount.Deposit(command.Amount, command);
        
        // Save the accounts
        _repository.Save(sourceAccount);
        _repository.Save(targetAccount);
        
        // Publish events
        foreach (var @event in sourceAccount.TakeEvents())
        {
            _eventBus.Publish(@event);
        }
        
        foreach (var @event in targetAccount.TakeEvents())
        {
            _eventBus.Publish(@event);
        }
    }
}
```

### Domain Model Pattern

The Domain Model pattern delegates the business logic to the domain model:

```csharp
public class DepositFundsHandler : ICommandHandler<DepositFunds>
{
    private readonly IRepository _repository;
    private readonly IEventBus _eventBus;
    
    public DepositFundsHandler(IRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(DepositFunds command)
    {
        // Get the account
        var account = _repository.GetById<Account>(command.AccountId);
        
        // Delegate to the domain model
        account.Deposit(command.Amount, command);
        
        // Save the account
        _repository.Save(account);
        
        // Publish events
        foreach (var @event in account.TakeEvents())
        {
            _eventBus.Publish(@event);
        }
    }
}
```

## Best Practices

1. **Single Responsibility**: Each command handler should handle a single command type
2. **Command Validation**: Validate commands before processing them
3. **Error Handling**: Implement proper error handling and provide meaningful error messages
4. **Transaction Management**: Ensure that command processing is transactional
5. **Event Publishing**: Publish domain events after processing commands
6. **Idempotent Operations**: Design command handlers to be idempotent when possible
7. **Dependency Injection**: Use dependency injection to provide dependencies to command handlers
8. **Logging**: Log command processing for debugging and auditing purposes
9. **Authorization**: Implement proper authorization checks in command handlers
10. **Testing**: Write unit tests for command handlers to verify business logic

## Common Pitfalls

1. **Missing Validation**: Not validating commands before processing them
2. **Business Logic in Handlers**: Putting too much business logic in handlers instead of the domain model
3. **Missing Error Handling**: Not properly handling exceptions in command handlers
4. **Transaction Boundaries**: Not properly defining transaction boundaries
5. **Event Publishing**: Forgetting to publish domain events after processing commands
6. **Dependency Leakage**: Allowing infrastructure concerns to leak into command handlers
7. **Command Handler Bloat**: Creating command handlers that do too many things
8. **Missing Authorization**: Not implementing proper authorization checks

## Advanced Scenarios

### Command Handler Decorators

Using the decorator pattern to add cross-cutting concerns to command handlers:

```csharp
public class LoggingCommandHandler<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _innerHandler;
    private readonly ILogger _logger;
    
    public LoggingCommandHandler(ICommandHandler<T> innerHandler, ILogger logger)
    {
        _innerHandler = innerHandler;
        _logger = logger;
    }
    
    public void Handle(T command)
    {
        try
        {
            _logger.LogInformation("Handling command {CommandType} with ID {CommandId}", 
                typeof(T).Name, 
                command is ICorrelatedMessage msg ? msg.MsgId : Guid.Empty);
                
            _innerHandler.Handle(command);
            
            _logger.LogInformation("Command {CommandType} handled successfully", 
                typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command {CommandType}", 
                typeof(T).Name);
                
            throw;
        }
    }
}
```

### Validation Decorator

```csharp
public class ValidatingCommandHandler<T> : ICommandHandler<T> where T : class, ICommand, IValidatable
{
    private readonly ICommandHandler<T> _innerHandler;
    
    public ValidatingCommandHandler(ICommandHandler<T> innerHandler)
    {
        _innerHandler = innerHandler;
    }
    
    public void Handle(T command)
    {
        // Validate the command
        var validationResult = command.Validate();
        
        if (!validationResult.IsValid)
        {
            throw new CommandValidationException(validationResult.Errors);
        }
        
        // Process the command
        _innerHandler.Handle(command);
    }
}
```

### Authorization Decorator

```csharp
public class AuthorizingCommandHandler<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _innerHandler;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserContext _userContext;
    
    public AuthorizingCommandHandler(
        ICommandHandler<T> innerHandler,
        IAuthorizationService authorizationService,
        IUserContext userContext)
    {
        _innerHandler = innerHandler;
        _authorizationService = authorizationService;
        _userContext = userContext;
    }
    
    public void Handle(T command)
    {
        // Get the current user
        var user = _userContext.CurrentUser;
        
        // Check if the user is authorized to execute the command
        if (!_authorizationService.IsAuthorized(user, command))
        {
            throw new UnauthorizedAccessException($"User {user.Id} is not authorized to execute command {typeof(T).Name}");
        }
        
        // Process the command
        _innerHandler.Handle(command);
    }
}
```

### Transaction Decorator

```csharp
public class TransactionalCommandHandler<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _innerHandler;
    private readonly IUnitOfWork _unitOfWork;
    
    public TransactionalCommandHandler(ICommandHandler<T> innerHandler, IUnitOfWork unitOfWork)
    {
        _innerHandler = innerHandler;
        _unitOfWork = unitOfWork;
    }
    
    public void Handle(T command)
    {
        try
        {
            // Begin a transaction
            _unitOfWork.Begin();
            
            // Process the command
            _innerHandler.Handle(command);
            
            // Commit the transaction
            _unitOfWork.Commit();
        }
        catch
        {
            // Rollback the transaction on error
            _unitOfWork.Rollback();
            throw;
        }
    }
}
```

## Related Components

- [ICommand](icommand.md): Interface for commands in Reactive Domain
- [Command](command.md): Base class for commands in Reactive Domain
- [ICommandBus](icommand-bus.md): Interface for sending commands
- [IEventBus](ievent-bus.md): Interface for publishing events
- [IRepository](irepository.md): Interface for repositories that store and retrieve aggregates
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [MessageBuilder](message-builder.md): Factory for creating correlated messages

---

**Navigation**:
- [← Previous: ICommand](./icommand.md)
- [↑ Back to Top](#icommandhandler-interface)
- [→ Next: IEventBus](./ievent-bus.md)
