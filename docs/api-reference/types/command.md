# Command

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`Command` is a base class in Reactive Domain that implements the `ICorrelatedMessage` interface and serves as the foundation for all command messages in the system.

## Overview

Commands in Reactive Domain represent requests for the system to perform an action. They are part of the write side of the CQRS pattern and typically result in state changes. The `Command` base class provides common functionality for all command implementations, including correlation and causation tracking.

In the Command Query Responsibility Segregation (CQRS) pattern, commands represent intentions to change the system state. Unlike events, which represent facts that have occurred, commands can be rejected if they violate business rules or if the system is in an inappropriate state to handle them.

## Class Definition

```csharp
public abstract class Command : ICommand, ICorrelatedMessage
{
    /// <summary>
    /// Gets the unique identifier for this message.
    /// </summary>
    public Guid MsgId { get; }
    
    /// <summary>
    /// Gets the correlation identifier that links related messages together.
    /// </summary>
    public Guid CorrelationId { get; }
    
    /// <summary>
    /// Gets the causation identifier that indicates which message caused this one.
    /// </summary>
    public Guid CausationId { get; }
    
    /// <summary>
    /// Initializes a new instance of the Command class with new correlation information.
    /// </summary>
    protected Command()
    {
        MsgId = Guid.NewGuid();
        CorrelationId = MsgId;
        CausationId = MsgId;
    }
    
    /// <summary>
    /// Initializes a new instance of the Command class with existing correlation information.
    /// </summary>
    /// <param name="correlationId">The correlation ID to use.</param>
    /// <param name="causationId">The causation ID to use.</param>
    protected Command(Guid correlationId, Guid causationId)
    {
        MsgId = Guid.NewGuid();
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Key Features

- **Message Identity**: Provides a unique `MsgId` for each command
- **Correlation Tracking**: Implements `ICorrelatedMessage` for tracking related messages
- **Immutability**: Ensures commands are immutable after creation
- **Type Safety**: Provides a type-safe base for all command implementations
- **Intent Communication**: Clearly communicates the intention to change system state
- **Validation Support**: Facilitates validation before state changes occur
- **Audit Trail**: Contributes to a complete audit trail when combined with events

## Command Types

In Reactive Domain, commands typically fall into several categories:

### Creation Commands

Commands that create new entities in the system:

```csharp
public class CreateCustomer : Command
{
    public readonly Guid CustomerId;
    public readonly string FirstName;
    public readonly string LastName;
    public readonly string Email;
    public readonly DateTime DateOfBirth;
    
    public CreateCustomer(Guid customerId, string firstName, string lastName, string email, DateTime dateOfBirth)
        : base()
    {
        CustomerId = customerId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        DateOfBirth = dateOfBirth;
    }
}
```

### Modification Commands

Commands that modify existing entities:

```csharp
public class ChangeCustomerAddress : Command
{
    public readonly Guid CustomerId;
    public readonly string StreetAddress;
    public readonly string City;
    public readonly string State;
    public readonly string PostalCode;
    public readonly string Country;
    
    public ChangeCustomerAddress(Guid customerId, string streetAddress, string city, 
                                string state, string postalCode, string country)
        : base()
    {
        CustomerId = customerId;
        StreetAddress = streetAddress;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }
}
```

### Deletion Commands

Commands that delete or deactivate entities:

```csharp
public class DeactivateCustomer : Command
{
    public readonly Guid CustomerId;
    public readonly string Reason;
    
    public DeactivateCustomer(Guid customerId, string reason)
        : base()
    {
        CustomerId = customerId;
        Reason = reason;
    }
}
```

### Process Commands

Commands that trigger business processes:

```csharp
public class PlaceOrder : Command
{
    public readonly Guid OrderId;
    public readonly Guid CustomerId;
    public readonly IReadOnlyList<OrderItem> Items;
    public readonly string ShippingAddress;
    public readonly string BillingAddress;
    public readonly PaymentMethod PaymentMethod;
    
    public PlaceOrder(Guid orderId, Guid customerId, IReadOnlyList<OrderItem> items,
                     string shippingAddress, string billingAddress, PaymentMethod paymentMethod)
        : base()
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        ShippingAddress = shippingAddress;
        BillingAddress = billingAddress;
        PaymentMethod = paymentMethod;
    }
}
```

## Usage

### Defining a Command

To create a new command type, inherit from the `Command` base class:

```csharp
public class CreateAccount : Command
{
    public readonly Guid AccountId;
    public readonly string AccountNumber;
    public readonly string CustomerName;
    public readonly decimal InitialDeposit;
    public readonly AccountType AccountType;
    
    public CreateAccount(Guid accountId, string accountNumber, string customerName, 
                         decimal initialDeposit, AccountType accountType)
        : base()
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        InitialDeposit = initialDeposit;
        AccountType = accountType;
    }
    
    // Constructor for correlated commands
    public CreateAccount(Guid accountId, string accountNumber, string customerName, 
                         decimal initialDeposit, AccountType accountType,
                         Guid correlationId, Guid causationId)
        : base(correlationId, causationId)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        InitialDeposit = initialDeposit;
        AccountType = accountType;
    }
}

public enum AccountType
{
    Checking,
    Savings,
    MoneyMarket,
    CertificateOfDeposit
}
```

### Using MessageBuilder with Commands

It's recommended to use the `MessageBuilder` factory to create commands with proper correlation:

```csharp
// Create a new command that starts a correlation chain
var createCommand = MessageBuilder.New(() => new CreateAccount(
    Guid.NewGuid(), 
    "ACC-123", 
    "John Doe",
    1000.00m,
    AccountType.Checking
));

// Create a command from an existing message
var depositCommand = MessageBuilder.From(createCommand, () => new DepositFunds(
    ((CreateAccount)createCommand).AccountId, 
    500.00m,
    "Initial deposit"
));

// Create another command in the same correlation chain
var setOverdraftCommand = MessageBuilder.From(createCommand, () => new SetOverdraftLimit(
    ((CreateAccount)createCommand).AccountId,
    250.00m
));
```

### Command Validation

Commands should be validated before they are processed. This can be done in the command handler or using a validation framework:

```csharp
public class CreateAccountValidator : ICommandValidator<CreateAccount>
{
    private readonly ICustomerRepository _customerRepository;
    
    public CreateAccountValidator(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }
    
    public ValidationResult Validate(CreateAccount command)
    {
        var result = new ValidationResult();
        
        // Validate account number format
        if (!Regex.IsMatch(command.AccountNumber, @"^ACC-\d{3,6}$"))
        {
            result.AddError("AccountNumber", "Account number must be in the format ACC-XXXXXX");
        }
        
        // Validate initial deposit
        if (command.InitialDeposit < 0)
        {
            result.AddError("InitialDeposit", "Initial deposit cannot be negative");
        }
        
        if (command.AccountType == AccountType.Savings && command.InitialDeposit < 100)
        {
            result.AddError("InitialDeposit", "Savings accounts require a minimum initial deposit of $100");
        }
        
        // Validate customer exists
        if (!_customerRepository.Exists(command.CustomerName))
        {
            result.AddError("CustomerName", "Customer does not exist");
        }
        
        return result;
    }
}
```

### Handling Commands

Commands are typically handled by command handlers:

```csharp
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly ICorrelatedRepository _repository;
    private readonly ICommandValidator<CreateAccount> _validator;
    private readonly ILogger _logger;
    
    public CreateAccountHandler(
        ICorrelatedRepository repository,
        ICommandValidator<CreateAccount> validator,
        ILogger logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }
    
    public void Handle(CreateAccount command)
    {
        _logger.LogInformation($"Handling CreateAccount command for {command.CustomerName}", command);
        
        // Validate the command
        var validationResult = _validator.Validate(command);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning($"Command validation failed: {string.Join(", ", validationResult.Errors)}", command);
            throw new CommandValidationException(validationResult.Errors);
        }
        
        try
        {
            // Create and save the aggregate
            var account = new Account(command.AccountId, command);
            
            // If initial deposit is provided, perform the deposit
            if (command.InitialDeposit > 0)
            {
                account.Deposit(command.InitialDeposit, command);
            }
            
            // Save the aggregate with correlation information
            _repository.Save(account, command);
            
            _logger.LogInformation($"Account {command.AccountNumber} created successfully", command);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating account: {ex.Message}", command);
            throw;
        }
    }
}
```

### Command Bus

Commands are typically sent through a command bus, which routes them to the appropriate handlers:

```csharp
public class CommandBusExample
{
    private readonly ICommandBus _commandBus;
    
    public CommandBusExample(ICommandBus commandBus)
    {
        _commandBus = commandBus;
    }
    
    public void SendCommands()
    {
        // Create a new command
        var createCommand = MessageBuilder.New(() => new CreateAccount(
            Guid.NewGuid(),
            "ACC-12345",
            "Jane Smith",
            1000.00m,
            AccountType.Checking
        ));
        
        // Send the command
        _commandBus.Send(createCommand);
        
        // Create a related command
        var depositCommand = MessageBuilder.From(createCommand, () => new DepositFunds(
            ((CreateAccount)createCommand).AccountId,
            500.00m,
            "Bonus deposit"
        ));
        
        // Send the related command
        _commandBus.Send(depositCommand);
    }
}
```

## Integration with Aggregates

Commands are used to modify aggregates, which then produce events:

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    private decimal _overdraftLimit;
    private List<Transaction> _transactions;
    
    public Account(Guid id, ICorrelatedMessage source) : base(id)
    {
        // Validate the ID
        if (id == Guid.Empty)
            throw new ArgumentException("Account ID cannot be empty", nameof(id));
            
        // Raise the creation event
        RaiseEvent(MessageBuilder.From(source, () => new AccountCreated(
            id,
            ((CreateAccount)source).AccountNumber,
            ((CreateAccount)source).CustomerName,
            ((CreateAccount)source).AccountType
        )));
    }
    
    public void Deposit(decimal amount, ICorrelatedMessage source)
    {
        // Validate state and parameters
        if (!_isActive)
            throw new InvalidOperationException("Cannot deposit to an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));
            
        // Raise the event
        RaiseEvent(MessageBuilder.From(source, () => new FundsDeposited(
            Id,
            amount,
            _balance + amount,
            DateTime.UtcNow
        )));
    }
    
    public void Withdraw(decimal amount, ICorrelatedMessage source)
    {
        // Validate state and parameters
        if (!_isActive)
            throw new InvalidOperationException("Cannot withdraw from an inactive account");
            
        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
            
        if (_balance + _overdraftLimit < amount)
            throw new InsufficientFundsException($"Insufficient funds. Balance: {_balance}, Overdraft Limit: {_overdraftLimit}");
            
        // Raise the event
        RaiseEvent(MessageBuilder.From(source, () => new FundsWithdrawn(
            Id,
            amount,
            _balance - amount,
            DateTime.UtcNow
        )));
    }
    
    public void SetOverdraftLimit(decimal limit, ICorrelatedMessage source)
    {
        // Validate state and parameters
        if (!_isActive)
            throw new InvalidOperationException("Cannot set overdraft limit on an inactive account");
            
        if (limit < 0)
            throw new ArgumentException("Overdraft limit cannot be negative", nameof(limit));
            
        // Raise the event
        RaiseEvent(MessageBuilder.From(source, () => new OverdraftLimitSet(
            Id,
            limit
        )));
    }
    
    public void Close(ICorrelatedMessage source)
    {
        // Validate state
        if (!_isActive)
            throw new InvalidOperationException("Account is already closed");
            
        if (_balance < 0)
            throw new InvalidOperationException("Cannot close account with negative balance");
            
        // Raise the event
        RaiseEvent(MessageBuilder.From(source, () => new AccountClosed(
            Id,
            DateTime.UtcNow
        )));
    }
    
    // Event application methods
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _balance = 0;
        _overdraftLimit = 0;
        _transactions = new List<Transaction>();
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
        _transactions.Add(new Transaction(
            TransactionType.Deposit,
            @event.Amount,
            @event.Timestamp
        ));
    }
    
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
        _transactions.Add(new Transaction(
            TransactionType.Withdrawal,
            @event.Amount,
            @event.Timestamp
        ));
    }
    
    private void Apply(OverdraftLimitSet @event)
    {
        _overdraftLimit = @event.Limit;
    }
    
    private void Apply(AccountClosed @event)
    {
        _isActive = false;
        _overdraftLimit = 0;
    }
}

public class Transaction
{
    public TransactionType Type { get; }
    public decimal Amount { get; }
    public DateTime Timestamp { get; }
    
    public Transaction(TransactionType type, decimal amount, DateTime timestamp)
    {
        Type = type;
        Amount = amount;
        Timestamp = timestamp;
    }
}

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Fee,
    Interest
}

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message) { }
}
```

## Command Versioning

As your system evolves, you may need to version your commands. Here's an approach to handle command versioning:

```csharp
// Version 1 of the command
public class CreateAccountV1 : Command
{
    public readonly Guid AccountId;
    public readonly string AccountNumber;
    public readonly string CustomerName;
    
    public CreateAccountV1(Guid accountId, string accountNumber, string customerName)
        : base()
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
    }
}

// Version 2 of the command with additional fields
public class CreateAccountV2 : Command
{
    public readonly Guid AccountId;
    public readonly string AccountNumber;
    public readonly string CustomerName;
    public readonly AccountType AccountType; // New field
    public readonly decimal InitialDeposit; // New field
    
    public CreateAccountV2(Guid accountId, string accountNumber, string customerName,
                          AccountType accountType, decimal initialDeposit)
        : base()
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        AccountType = accountType;
        InitialDeposit = initialDeposit;
    }
}

// Command handler that can handle both versions
public class CreateAccountHandler : 
    ICommandHandler<CreateAccountV1>,
    ICommandHandler<CreateAccountV2>
{
    private readonly ICorrelatedRepository _repository;
    
    public CreateAccountHandler(ICorrelatedRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccountV1 command)
    {
        // Handle version 1 - use default values for missing fields
        var account = new Account(command.AccountId, command);
        _repository.Save(account, command);
    }
    
    public void Handle(CreateAccountV2 command)
    {
        // Handle version 2 - use all provided fields
        var account = new Account(command.AccountId, command);
        
        // If initial deposit is provided, perform the deposit
        if (command.InitialDeposit > 0)
        {
            account.Deposit(command.InitialDeposit, command);
        }
        
        _repository.Save(account, command);
    }
}
```

## Testing Commands

Commands should be thoroughly tested to ensure they behave as expected:

```csharp
public class CreateAccountHandlerTests
{
    [Fact]
    public void Handle_ValidCommand_CreatesAccount()
    {
        // Arrange
        var repository = new InMemoryCorrelatedRepository();
        var validator = new MockValidator<CreateAccount>(true);
        var logger = new MockLogger();
        var handler = new CreateAccountHandler(repository, validator, logger);
        
        var command = MessageBuilder.New(() => new CreateAccount(
            Guid.NewGuid(),
            "ACC-12345",
            "John Doe",
            1000.00m,
            AccountType.Checking
        ));
        
        // Act
        handler.Handle(command);
        
        // Assert
        var account = repository.GetById<Account>(command.AccountId, command);
        Assert.NotNull(account);
        Assert.Equal(1000.00m, account.GetBalance());
    }
    
    [Fact]
    public void Handle_InvalidCommand_ThrowsValidationException()
    {
        // Arrange
        var repository = new InMemoryCorrelatedRepository();
        var validationErrors = new[] { "Account number is invalid" };
        var validator = new MockValidator<CreateAccount>(false, validationErrors);
        var logger = new MockLogger();
        var handler = new CreateAccountHandler(repository, validator, logger);
        
        var command = MessageBuilder.New(() => new CreateAccount(
            Guid.NewGuid(),
            "INVALID",
            "John Doe",
            1000.00m,
            AccountType.Checking
        ));
        
        // Act & Assert
        var exception = Assert.Throws<CommandValidationException>(() => handler.Handle(command));
        Assert.Contains("Account number is invalid", exception.Message);
    }
}
```

## Best Practices

1. **Immutable Commands**: Make all command properties read-only to ensure they cannot be changed after creation
2. **Descriptive Names**: Use verb-noun naming convention (e.g., `CreateAccount`, `DepositFunds`) to clearly communicate intent
3. **Minimal Data**: Include only the data needed to perform the action, avoiding unnecessary information
4. **Use MessageBuilder**: Always use `MessageBuilder` to create commands with proper correlation tracking
5. **Validation**: Validate commands before processing them to ensure they meet business rules
6. **Single Responsibility**: Each command should represent a single action or intention
7. **Versioning Strategy**: Plan for command versioning from the beginning
8. **Error Handling**: Implement proper error handling and reporting for command failures
9. **Logging**: Log command processing with correlation IDs for traceability
10. **Testing**: Thoroughly test command handlers with both valid and invalid commands

## Common Pitfalls

1. **Mutable Commands**: Avoid mutable properties in commands as they can lead to inconsistent state
2. **Business Logic in Commands**: Commands should be simple data carriers without business logic
3. **Missing Correlation**: Ensure correlation information is properly maintained throughout the command flow
4. **Large Commands**: Keep commands focused and minimal to avoid complexity
5. **Insufficient Validation**: Failing to validate commands properly can lead to invalid system state
6. **Tight Coupling**: Avoid coupling commands to specific implementations or frameworks
7. **Inconsistent Naming**: Maintain consistent naming conventions across all commands
8. **Command Reuse**: Avoid reusing command instances for multiple operations
9. **Excessive Command Fields**: Include only necessary fields to avoid bloat
10. **Ignoring Command Failures**: Always handle command failures gracefully and provide meaningful feedback

## Related Components

- [ICommand](./icommand.md): Interface for command messages
- [ICorrelatedMessage](./icorrelated-message.md): Interface for messages with correlation information
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [ICommandHandler](./icommand-handler.md): Interface for handling commands
- [Event](./event.md): Base class for event messages that result from commands
- [AggregateRoot](./aggregate-root.md): Base class for domain aggregates that process commands
- [ICommandBus](./icommand-bus.md): Interface for routing commands to handlers
- [ReadModelBase](./read-model-base.md): Base class for read models that are updated as a result of command processing

For a comprehensive view of how commands interact with other components, see the [Key Component Relationships](../../architecture.md#key-component-relationships) section in the Architecture Guide, particularly the [Command and Event Relationship](../../architecture.md#command-and-event-relationship) diagram.

---

**Navigation**:
- [← Previous: MessageBuilder](./message-builder.md)
- [↑ Back to Top](#command)
- [→ Next: Event](./event.md)
