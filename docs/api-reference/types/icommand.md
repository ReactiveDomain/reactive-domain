# ICommand Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICommand` interface is a marker interface that defines the contract for commands in Reactive Domain. Commands represent intentions to change the state of the system and are a fundamental building block in the Command Query Responsibility Segregation (CQRS) pattern.

In Reactive Domain, commands are immutable messages that encapsulate a request for the system to perform an action or change its state. They are typically handled by a single handler that validates the command and applies the requested changes to the domain model.

## Commands in CQRS

In a CQRS architecture, commands play a crucial role:

1. **Intent Expression**: Commands express the intent to change the system's state
2. **Business Rules**: Commands encapsulate business rules and validation logic
3. **Single Handler**: Each command type typically has exactly one handler
4. **Write Operations**: Commands represent write operations in the system
5. **Immutability**: Commands are immutable once created

Commands are distinct from queries (which retrieve data) and events (which represent facts that have occurred). This separation is a key aspect of the CQRS pattern.

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface ICommand : IMessage
{
}
```

## Inheritance

The `ICommand` interface inherits from the `IMessage` interface, which provides the base contract for all messages in Reactive Domain.

```
IMessage
  ↑
ICommand
```

## Usage

The `ICommand` interface is typically used as a marker interface to identify command messages in the system. Commands are usually implemented as concrete classes that inherit from the `Command` base class, which provides common functionality for commands.

### Basic Command Implementation

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

// Define a command handler
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly IRepository _repository;
    
    public CreateAccountHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccount command)
    {
        // Create a new account
        var account = new Account(command.AccountId);
        account.CreateAccount(command.AccountNumber, command.InitialDeposit, command);
        
        // Save the account
        _repository.Save(account);
    }
}
```

### Command with Validation

```csharp
public class DepositFunds : Command, IValidatable
{
    public Guid AccountId { get; }
    public decimal Amount { get; }
    
    public DepositFunds(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
    
    public ValidationResult Validate()
    {
        var result = new ValidationResult();
        
        if (AccountId == Guid.Empty)
            result.AddError("AccountId is required");
            
        if (Amount <= 0)
            result.AddError("Amount must be greater than zero");
            
        return result;
    }
}
```

### Correlated Command

```csharp
public class TransferFunds : Command, ICorrelatedMessage
{
    public Guid SourceAccountId { get; }
    public Guid TargetAccountId { get; }
    public decimal Amount { get; }
    
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public TransferFunds(
        Guid sourceAccountId, 
        Guid targetAccountId, 
        decimal amount,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        SourceAccountId = sourceAccountId;
        TargetAccountId = targetAccountId;
        Amount = amount;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
    
    // Alternative constructor using MessageBuilder
    public static TransferFunds Create(
        Guid sourceAccountId,
        Guid targetAccountId,
        decimal amount,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new TransferFunds(
            sourceAccountId,
            targetAccountId,
            amount,
            Guid.NewGuid(),
            source.CorrelationId,
            source.MsgId));
    }
}
```

## Command Bus Integration

Commands are typically sent through a command bus, which routes them to their appropriate handlers:

```csharp
// Create a command bus
var commandBus = new CommandBus();

// Register a command handler
commandBus.Subscribe<CreateAccount>(cmd => 
{
    // Create a new account
    var account = new Account(cmd.AccountId);
    account.CreateAccount(cmd.AccountNumber, cmd.InitialDeposit, cmd);
    
    // Save the account
    repository.Save(account);
});

// Create and send a command
var createAccountCommand = new CreateAccount(Guid.NewGuid(), "12345", 1000);
commandBus.Send(createAccountCommand);
```

## Command Handling Patterns

### Transaction Script Pattern

```csharp
public class AccountCommandHandler : 
    ICommandHandler<CreateAccount>,
    ICommandHandler<DepositFunds>,
    ICommandHandler<WithdrawFunds>,
    ICommandHandler<CloseAccount>
{
    private readonly IRepository _repository;
    
    public AccountCommandHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccount command)
    {
        // Validate command
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
    
    public void Handle(DepositFunds command)
    {
        // Validate command
        if (command.Amount <= 0)
            throw new ArgumentException("Deposit amount must be greater than zero");
            
        // Get the account
        var account = _repository.GetById<Account>(command.AccountId);
        
        // Process the command
        account.Deposit(command.Amount, command);
        
        // Save the account
        _repository.Save(account);
    }
    
    // Additional handlers...
}
```

### Domain Model Pattern

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Command handlers
    
    public void CreateAccount(string accountNumber, decimal initialDeposit, ICorrelatedMessage source)
    {
        // Validate command
        if (string.IsNullOrEmpty(accountNumber))
            throw new ArgumentException("Account number is required");
            
        if (initialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative");
            
        if (_isActive)
            throw new InvalidOperationException("Account already exists");
            
        // Raise event
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(Id, accountNumber, initialDeposit)));
    }
    
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        // Validate command
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be greater than zero");
            
        // Raise event
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(Id, amount)));
    }
    
    // Event handlers
    
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _accountNumber = @event.AccountNumber;
        _balance = @event.InitialDeposit;
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    // Additional methods and event handlers...
}
```

## Best Practices

1. **Command Naming**: Use verb-noun naming for commands (e.g., `CreateAccount`, `TransferFunds`)
2. **Command Immutability**: Make commands immutable to prevent unintended side effects
3. **Command Validation**: Validate commands before processing them
4. **Single Responsibility**: Each command should represent a single action or intent
5. **Command Properties**: Include all necessary information in the command properties
6. **Command Versioning**: Plan for command versioning to handle schema evolution
7. **Error Handling**: Implement proper error handling in command handlers
8. **Idempotent Operations**: Design commands to be idempotent when possible
9. **Correlation**: Use correlation IDs to track command flows through the system
10. **Security**: Implement proper authorization checks for command handlers

## Common Pitfalls

1. **Mutable Commands**: Creating commands that can be modified after creation
2. **Missing Validation**: Not validating commands before processing them
3. **Command Overloading**: Creating commands that do too many things
4. **Missing Properties**: Not including all necessary information in commands
5. **Direct State Modification**: Modifying state directly in command handlers instead of through events
6. **Command Coupling**: Coupling commands to their handlers, reducing flexibility
7. **Missing Error Handling**: Not properly handling exceptions in command handlers
8. **Command Bus Overuse**: Using the command bus for queries or notifications

## Advanced Scenarios

### Command Validation

Implementing command validation using a decorator pattern:

```csharp
public class ValidatingCommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand, IValidatable
{
    private readonly ICommandHandler<TCommand> _innerHandler;
    
    public ValidatingCommandHandler(ICommandHandler<TCommand> innerHandler)
    {
        _innerHandler = innerHandler;
    }
    
    public void Handle(TCommand command)
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

### Command Logging

Adding logging to command handlers:

```csharp
public class LoggingCommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly ICommandHandler<TCommand> _innerHandler;
    private readonly ILogger _logger;
    
    public LoggingCommandHandler(ICommandHandler<TCommand> innerHandler, ILogger logger)
    {
        _innerHandler = innerHandler;
        _logger = logger;
    }
    
    public void Handle(TCommand command)
    {
        try
        {
            _logger.LogInformation("Handling command {CommandType} with ID {CommandId}", 
                typeof(TCommand).Name, 
                command is ICorrelatedMessage msg ? msg.MsgId : Guid.Empty);
                
            _innerHandler.Handle(command);
            
            _logger.LogInformation("Command {CommandType} handled successfully", 
                typeof(TCommand).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command {CommandType}", 
                typeof(TCommand).Name);
                
            throw;
        }
    }
}
```

### Command Authorization

Implementing command authorization:

```csharp
public class AuthorizingCommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly ICommandHandler<TCommand> _innerHandler;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserContext _userContext;
    
    public AuthorizingCommandHandler(
        ICommandHandler<TCommand> innerHandler,
        IAuthorizationService authorizationService,
        IUserContext userContext)
    {
        _innerHandler = innerHandler;
        _authorizationService = authorizationService;
        _userContext = userContext;
    }
    
    public void Handle(TCommand command)
    {
        // Get the current user
        var user = _userContext.CurrentUser;
        
        // Check if the user is authorized to execute the command
        if (!_authorizationService.IsAuthorized(user, command))
        {
            throw new UnauthorizedAccessException($"User {user.Id} is not authorized to execute command {typeof(TCommand).Name}");
        }
        
        // Process the command
        _innerHandler.Handle(command);
    }
}
```

## Related Components

- [Command](command.md): Base class for commands in Reactive Domain
- [ICommandBus](icommand-bus.md): Interface for sending commands
- [ICommandHandler](icommand-handler.md): Interface for command handlers
- [IMessage](imessage.md): Base interface for all messages
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [MessageBuilder](message-builder.md): Factory for creating correlated messages
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [IRepository](irepository.md): Interface for repositories that store and retrieve aggregates

---

**Navigation**:
- [← Previous: ICheckpointStore](./icheckpoint-store.md)
- [↑ Back to Top](#icommand-interface)
- [→ Next: Command](./command.md)
