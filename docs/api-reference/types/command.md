# Command

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`Command` is a base class in Reactive Domain that implements the `ICorrelatedMessage` interface and serves as the foundation for all command messages in the system.

## Overview

Commands in Reactive Domain represent requests for the system to perform an action. They are part of the write side of the CQRS pattern and typically result in state changes. Commands are named in the imperative form (e.g., `CreateAccount`, `DepositFunds`) to emphasize that they represent intentions rather than facts. The `Command` base class provides common functionality for all command implementations, including correlation and causation tracking.

In the Command Query Responsibility Segregation (CQRS) pattern, commands represent intentions to change the system state. Unlike events, which represent facts that have occurred, commands can be rejected if they violate business rules or if the system is in an inappropriate state to handle them. When a command is processed successfully, it typically results in one or more events being raised via the `RaiseEvent()` method in the aggregate.

Commands in Reactive Domain follow these key principles:

1. **Single Responsibility**: Each command represents a single action or intention
2. **Immutability**: Commands are immutable after creation
3. **Validation**: Commands are validated before they are processed
4. **Explicit Intent**: Command names clearly communicate their purpose
5. **Correlation**: Commands maintain correlation and causation information for tracing

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

The `Command` base class provides the foundation for all command messages in Reactive Domain. It implements the `ICommand` and `ICorrelatedMessage` interfaces, which define the core contract for commands in the system.

> **Note**: In most cases, you should not call these constructors directly. Instead, use the `MessageBuilder` class to create commands with proper correlation tracking.

## Key Features

- **Message Identity**: Provides a unique `MsgId` for each command
- **Correlation Tracking**: Implements `ICorrelatedMessage` for tracking related messages across system boundaries
- **Immutability**: Ensures commands are immutable after creation, preventing unexpected changes
- **Type Safety**: Provides a type-safe base for all command implementations in the domain
- **Intent Communication**: Clearly communicates the intention to change system state
- **Validation Support**: Facilitates validation before state changes occur
- **Audit Trail**: Contributes to a complete audit trail when combined with events
- **Serialization**: Designed to be easily serializable for transport across process boundaries

## Command Types

In Reactive Domain, commands typically fall into several categories:

### Creation Commands

Commands that create new entities in the system:

```csharp
public class CreateCustomer : Command, ICorrelatedMessage
{
    // Identity properties
    public Guid CustomerId { get; }
    
    // Data properties
    public string FirstName { get; }
    public string LastName { get; }
    public string Email { get; }
    public DateTime DateOfBirth { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method for creating new commands with MessageBuilder
    public static CreateCustomer Create(
        Guid customerId, 
        string firstName, 
        string lastName, 
        string email, 
        DateTime dateOfBirth,
        ICorrelatedMessage source = null)
    {
        if (source != null)
        {
            return MessageBuilder.From(source, () => new CreateCustomer(
                customerId, firstName, lastName, email, dateOfBirth,
                Guid.NewGuid(), source.CorrelationId, source.MsgId));
        }
        else
        {
            return MessageBuilder.New(() => new CreateCustomer(
                customerId, firstName, lastName, email, dateOfBirth,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        }
    }
    
    // Private constructor for MessageBuilder
    private CreateCustomer(
        Guid customerId, 
        string firstName, 
        string lastName, 
        string email, 
        DateTime dateOfBirth,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        // Validate parameters
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));
            
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));
            
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));
            
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
            
        if (dateOfBirth == default)
            throw new ArgumentException("Date of birth is required", nameof(dateOfBirth));
        
        // Set properties
        CustomerId = customerId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        DateOfBirth = dateOfBirth;
        
        // Set correlation properties
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

### Modification Commands

Commands that modify existing entities:

```csharp
public class ChangeCustomerAddress : Command
{
    // Identity properties
    public Guid CustomerId { get; }
    
    // Data properties
    public string StreetAddress { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }
    
    // Factory method
    public static ChangeCustomerAddress Create(
        Guid customerId, 
        string streetAddress, 
        string city, 
        string state, 
        string postalCode, 
        string country,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new ChangeCustomerAddress(
            customerId, streetAddress, city, state, postalCode, country));
    }
    
    // Constructor for MessageBuilder
    private ChangeCustomerAddress(Guid customerId, string streetAddress, string city, 
                                string state, string postalCode, string country)
    {
        // Validate parameters
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));
            
        if (string.IsNullOrWhiteSpace(streetAddress))
            throw new ArgumentException("Street address is required", nameof(streetAddress));
            
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required", nameof(city));
            
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State is required", nameof(state));
            
        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code is required", nameof(postalCode));
            
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country is required", nameof(country));
        
        // Set properties
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
    // Identity properties
    public Guid CustomerId { get; }
    
    // Data properties
    public string Reason { get; }
    
    // Factory method
    public static DeactivateCustomer Create(Guid customerId, string reason, ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new DeactivateCustomer(customerId, reason));
    }
    
    // Constructor for MessageBuilder
    private DeactivateCustomer(Guid customerId, string reason)
    {
        // Validate parameters
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));
            
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required", nameof(reason));
        
        // Set properties
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
    // Identity properties
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    
    // Data properties
    public IReadOnlyList<OrderItem> Items { get; }
    public string ShippingAddress { get; }
    public string BillingAddress { get; }
    public PaymentMethod PaymentMethod { get; }
    
    // Factory method
    public static PlaceOrder Create(
        Guid orderId,
        Guid customerId,
        IReadOnlyList<OrderItem> items,
        string shippingAddress,
        string billingAddress,
        PaymentMethod paymentMethod,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new PlaceOrder(
            orderId, customerId, items, shippingAddress, billingAddress, paymentMethod));
    }
    
    // Constructor for MessageBuilder
    private PlaceOrder(Guid orderId, Guid customerId, IReadOnlyList<OrderItem> items,
                     string shippingAddress, string billingAddress, PaymentMethod paymentMethod)
    {
        // Validate parameters
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order ID cannot be empty", nameof(orderId));
            
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));
            
        if (items == null || !items.Any())
            throw new ArgumentException("Order must contain at least one item", nameof(items));
            
        if (string.IsNullOrWhiteSpace(shippingAddress))
            throw new ArgumentException("Shipping address is required", nameof(shippingAddress));
            
        if (string.IsNullOrWhiteSpace(billingAddress))
            throw new ArgumentException("Billing address is required", nameof(billingAddress));
            
        if (paymentMethod == null)
            throw new ArgumentException("Payment method is required", nameof(paymentMethod));
        
        // Set properties
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

There are two recommended patterns for defining commands in Reactive Domain:

#### Pattern 1: Using Factory Methods with MessageBuilder (Recommended)

This pattern uses a private constructor and static factory methods to ensure proper correlation:

```csharp
public class CreateAccount : Command, ICorrelatedMessage
{
    // Identity properties
    public Guid AccountId { get; }
    
    // Data properties
    public string AccountNumber { get; }
    public string CustomerName { get; }
    public decimal InitialDeposit { get; }
    public AccountType AccountType { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Factory method for creating a new command
    public static CreateAccount Create(
        Guid accountId,
        string accountNumber,
        string customerName,
        decimal initialDeposit,
        AccountType accountType,
        ICorrelatedMessage source = null)
    {
        // Validate business rules
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account ID cannot be empty", nameof(accountId));
            
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required", nameof(accountNumber));
            
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required", nameof(customerName));
            
        if (initialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative", nameof(initialDeposit));
        
        // Create the command with proper correlation
        if (source != null)
        {
            return MessageBuilder.From(source, () => new CreateAccount(
                accountId, accountNumber, customerName, initialDeposit, accountType,
                Guid.NewGuid(), source.CorrelationId, source.MsgId));
        }
        else
        {
            return MessageBuilder.New(() => new CreateAccount(
                accountId, accountNumber, customerName, initialDeposit, accountType,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        }
    }
    
    // Private constructor for MessageBuilder
    private CreateAccount(
        Guid accountId,
        string accountNumber,
        string customerName,
        decimal initialDeposit,
        AccountType accountType,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        // Set properties
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        InitialDeposit = initialDeposit;
        AccountType = accountType;
        
        // Set correlation properties
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
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

#### Pattern 2: Using MessageBuilder Directly

This pattern is simpler but requires using MessageBuilder at the call site:

```csharp
public class CreateAccount : Command
{
    // Identity properties
    public Guid AccountId { get; }
    
    // Data properties
    public string AccountNumber { get; }
    public string CustomerName { get; }
    public decimal InitialDeposit { get; }
    public AccountType AccountType { get; }
    
    // Constructor for MessageBuilder
    public CreateAccount(
        Guid accountId,
        string accountNumber,
        string customerName,
        decimal initialDeposit,
        AccountType accountType)
    {
        // Validate business rules
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account ID cannot be empty", nameof(accountId));
            
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required", nameof(accountNumber));
            
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required", nameof(customerName));
            
        if (initialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative", nameof(initialDeposit));
        
        // Set properties
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerName = customerName;
        InitialDeposit = initialDeposit;
        AccountType = accountType;
    }
}

// Usage with MessageBuilder:
// Starting a new correlation chain
var createCommand = MessageBuilder.New(() => new CreateAccount(
    Guid.NewGuid(),
    "ACC-123456",
    "John Doe",
    1000.00m,
    AccountType.Checking
));

// Continuing an existing correlation chain
var depositCommand = MessageBuilder.From(createCommand).Build(() => new DepositFunds(
    ((CreateAccount)createCommand).AccountId,
    500.00m,
    "Initial deposit"
));
```

### Command Design Best Practices

1. **Use Properties Instead of Fields**: Use properties with getters only to ensure immutability
2. **Validate in Constructor**: Perform validation in the constructor to ensure commands are always valid
3. **Use Factory Methods**: Provide static factory methods to create commands with proper correlation
4. **Hide Correlation Details**: Keep correlation IDs internal to the command implementation
5. **Include Identity Properties**: Always include identity properties to identify the target aggregate
6. **Use Meaningful Names**: Name commands clearly to express their intent (e.g., `CreateAccount`, `DepositFunds`)
7. **Keep Commands Focused**: Each command should represent a single action or intention

### Creating Correlated Commands

> **Important**: Always use `MessageBuilder` to create correlated commands. Do not manually set correlation and causation IDs by calling constructors directly.

The recommended way to create commands that continue an existing correlation chain is to use the `MessageBuilder` class. This ensures proper correlation tracking and maintains the causality chain.

#### How MessageBuilder Sets Correlation IDs

When using MessageBuilder, correlation IDs are set automatically according to these rules:

1. For `MessageBuilder.New()` (starting a new correlation chain):
   - `MsgId` = new Guid() (a new unique ID)
   - `CorrelationId` = MsgId (same as MsgId)
   - `CausationId` = MsgId (same as MsgId)

2. For `MessageBuilder.From(source).Build(...)` (continuing an existing correlation chain):
   - `MsgId` = new Guid() (a new unique ID)
   - `CorrelationId` = source.CorrelationId (copied from source message)
   - `CausationId` = source.MsgId (set to the source message's ID)

This approach ensures proper tracking of message relationships without exposing correlation details in your public API.

#### Example Usage

```csharp
// Starting a new correlation chain with MessageBuilder.New()
var createCommand = MessageBuilder.New(() => new CreateAccount(
    Guid.NewGuid(),
    "ACC-123456",
    "John Doe",
    1000.00m,
    AccountType.Checking
));

// INCORRECT - Do not create correlated commands this way:
// var depositCommand = new DepositFunds(
//     ((CreateAccount)createCommand).AccountId,
//     500.00m,
//     "Initial deposit",
//     createCommand.CorrelationId,  // Don't pass correlation IDs directly
//     createCommand.MsgId
// );

// CORRECT - Use MessageBuilder.From() to continue the correlation chain:
var depositCommand = MessageBuilder.From(createCommand).Build(() => new DepositFunds(
    ((CreateAccount)createCommand).AccountId,
    500.00m,
    "Initial deposit"
));

// Create another command in the same correlation chain
var setOverdraftCommand = MessageBuilder.From(createCommand).Build(() => new SetOverdraftLimit(
    ((CreateAccount)createCommand).AccountId,
    250.00m
));
```

### Command Validation

Command validation is a critical aspect of maintaining system integrity. There are three common approaches to validation in Reactive Domain:

#### 1. Self-Validation in Commands

Commands can validate their own parameters in the constructor or factory method:

```csharp
public static CreateAccount Create(
    Guid accountId,
    string accountNumber,
    string customerName,
    decimal initialDeposit,
    AccountType accountType,
    ICorrelatedMessage source = null)
{
    // Validate business rules
    if (accountId == Guid.Empty)
        throw new ArgumentException("Account ID cannot be empty", nameof(accountId));
        
    if (string.IsNullOrWhiteSpace(accountNumber))
        throw new ArgumentException("Account number is required", nameof(accountNumber));
        
    if (!Regex.IsMatch(accountNumber, @"^ACC-\d{3,6}$"))
        throw new ArgumentException("Account number must be in the format ACC-XXXXXX", nameof(accountNumber));
        
    if (string.IsNullOrWhiteSpace(customerName))
        throw new ArgumentException("Customer name is required", nameof(customerName));
        
    if (initialDeposit < 0)
        throw new ArgumentException("Initial deposit cannot be negative", nameof(initialDeposit));
    
    if (accountType == AccountType.Savings && initialDeposit < 100)
        throw new ArgumentException("Savings accounts require a minimum initial deposit of $100", nameof(initialDeposit));
    
    // Create the command with proper correlation
    // ...
}
```

#### 2. External Validators

Separate validator classes can be used for more complex validation rules, especially those requiring external dependencies:

```csharp
public class CreateAccountValidator : ICommandValidator<CreateAccount>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAccountNumberValidator _accountNumberValidator;
    
    public CreateAccountValidator(
        ICustomerRepository customerRepository,
        IAccountNumberValidator accountNumberValidator)
    {
        _customerRepository = customerRepository;
        _accountNumberValidator = accountNumberValidator;
    }
    
    public ValidationResult Validate(CreateAccount command)
    {
        var result = new ValidationResult();
        
        // Validate account number format and uniqueness
        if (!_accountNumberValidator.IsValid(command.AccountNumber))
        {
            result.AddError("AccountNumber", "Invalid account number format");
        }
        
        if (_accountNumberValidator.IsDuplicate(command.AccountNumber))
        {
            result.AddError("AccountNumber", "Account number already exists");
        }
        
        // Validate initial deposit based on account type
        if (command.AccountType == AccountType.Savings && command.InitialDeposit < 100)
        {
            result.AddError("InitialDeposit", "Savings accounts require a minimum initial deposit of $100");
        }
        else if (command.AccountType == AccountType.MoneyMarket && command.InitialDeposit < 1000)
        {
            result.AddError("InitialDeposit", "Money market accounts require a minimum initial deposit of $1,000");
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

#### 3. Aggregate-Level Validation

Aggregates can perform domain-specific validation when handling commands:

```csharp
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
```

#### Recommended Validation Strategy

For a robust validation approach, combine these strategies:

1. **Command Self-Validation**: Validate basic parameter constraints in the command itself
2. **External Validators**: Use separate validators for complex rules requiring external dependencies
3. **Aggregate Validation**: Perform domain-specific validation in the aggregate

This layered approach ensures that:
- Commands are always well-formed before being processed
- Complex business rules are enforced consistently
- Domain invariants are protected at the aggregate level

### Handling Commands

Commands are processed by command handlers that implement the `ICommandHandler<T>` interface. Command handlers are responsible for:

1. Validating the command
2. Loading or creating the appropriate aggregate
3. Calling the appropriate method on the aggregate
4. Saving the aggregate to persist any changes

#### Standard Command Handler Pattern

```csharp
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly ICorrelatedRepository _repository;
    private readonly ICommandValidator<CreateAccount> _validator;
    private readonly ILogger<CreateAccountHandler> _logger;
    
    public CreateAccountHandler(
        ICorrelatedRepository repository,
        ICommandValidator<CreateAccount> validator,
        ILogger<CreateAccountHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }
    
    public void Handle(CreateAccount command)
    {
        _logger.LogInformation("Handling CreateAccount command for {CustomerName}", command.CustomerName);
        
        // 1. Validate the command
        var validationResult = _validator.Validate(command);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Command validation failed: {Errors}", 
                string.Join(", ", validationResult.Errors));
            throw new CommandValidationException(validationResult.Errors);
        }
        
        try
        {
            // 2. Create the aggregate
            var account = new Account(command.AccountId, command);
            
            // 3. Apply additional commands if needed
            if (command.InitialDeposit > 0)
            {
                account.Deposit(command.InitialDeposit, command);
            }
            
            // 4. Save the aggregate with correlation information
            _repository.Save(account);
            
            _logger.LogInformation("Account {AccountNumber} created successfully", 
                command.AccountNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating account: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
```

#### Command Handler for Existing Aggregates

```csharp
public class DepositFundsHandler : ICommandHandler<DepositFunds>
{
    private readonly ICorrelatedRepository _repository;
    private readonly ILogger<DepositFundsHandler> _logger;
    
    public DepositFundsHandler(
        ICorrelatedRepository repository,
        ILogger<DepositFundsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public void Handle(DepositFunds command)
    {
        _logger.LogInformation("Handling DepositFunds command for account {AccountId}", 
            command.AccountId);
        
        try
        {
            // 1. Load the aggregate
            var account = _repository.GetById<Account>(command.AccountId);
            if (account == null)
            {
                throw new AggregateNotFoundException(typeof(Account), command.AccountId);
            }
            
            // 2. Call the appropriate method on the aggregate
            account.Deposit(command.Amount, command);
            
            // 3. Save the aggregate
            _repository.Save(account);
            
            _logger.LogInformation("Deposited {Amount} to account {AccountId} successfully", 
                command.Amount, command.AccountId);
        }
        catch (AggregateNotFoundException ex)
        {
            _logger.LogWarning(ex, "Account {AccountId} not found", command.AccountId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error depositing funds: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
```

#### Command Handler with Optimistic Concurrency

```csharp
public class WithdrawFundsHandler : ICommandHandler<WithdrawFunds>
{
    private readonly ICorrelatedRepository _repository;
    private readonly ILogger<WithdrawFundsHandler> _logger;
    private readonly int _maxRetries = 3;
    
    public WithdrawFundsHandler(
        ICorrelatedRepository repository,
        ILogger<WithdrawFundsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public void Handle(WithdrawFunds command)
    {
        _logger.LogInformation("Handling WithdrawFunds command for account {AccountId}", 
            command.AccountId);
        
        int retryCount = 0;
        while (true)
        {
            try
            {
                // 1. Load the aggregate
                var account = _repository.GetById<Account>(command.AccountId);
                if (account == null)
                {
                    throw new AggregateNotFoundException(typeof(Account), command.AccountId);
                }
                
                // 2. Call the appropriate method on the aggregate
                account.Withdraw(command.Amount, command);
                
                // 3. Save the aggregate
                _repository.Save(account);
                
                _logger.LogInformation("Withdrew {Amount} from account {AccountId} successfully", 
                    command.Amount, command.AccountId);
                    
                return; // Success, exit the retry loop
            }
            catch (AggregateVersionException ex) when (retryCount < _maxRetries)
            {
                // Handle optimistic concurrency conflict
                retryCount++;
                _logger.LogWarning(ex, "Optimistic concurrency conflict detected, retry {RetryCount}/{MaxRetries}", 
                    retryCount, _maxRetries);
                    
                // Add a small delay before retrying
                Thread.Sleep(50 * retryCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing funds: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }
}
```

#### Command Handler Best Practices

1. **Single Responsibility**: Each command handler should handle exactly one command type
2. **Proper Error Handling**: Use try-catch blocks to handle exceptions and provide meaningful error messages
3. **Logging**: Log all important operations, including command receipt, validation results, and success/failure
4. **Optimistic Concurrency**: Implement retry logic for handling optimistic concurrency conflicts
5. **Correlation**: Maintain correlation information throughout the command handling process
6. **Validation**: Validate commands before processing them
7. **Transaction Boundaries**: Each command handler represents a single transaction boundary
```

### Command Bus

The Command Bus is a central component in Reactive Domain that routes commands to their appropriate handlers. It provides a clean separation between command senders and handlers, allowing for a more decoupled architecture.

#### Command Bus Interface

```csharp
public interface ICommandBus
{
    void Send<TCommand>(TCommand command) where TCommand : class, ICommand;
    Task SendAsync<TCommand>(TCommand command) where TCommand : class, ICommand;
    TResult Send<TCommand, TResult>(TCommand command) where TCommand : class, ICommand;
    Task<TResult> SendAsync<TCommand, TResult>(TCommand command) where TCommand : class, ICommand;
}
```

#### Using the Command Bus

```csharp
public class AccountService
{
    private readonly ICommandBus _commandBus;
    private readonly ILogger<AccountService> _logger;
    
    public AccountService(ICommandBus commandBus, ILogger<AccountService> logger)
    {
        _commandBus = commandBus;
        _logger = logger;
    }
    
    public Guid CreateNewAccount(string customerName, decimal initialDeposit, AccountType accountType)
    {
        _logger.LogInformation("Creating new account for {CustomerName}", customerName);
        
        // Generate a new account ID
        var accountId = Guid.NewGuid();
        
        // Generate a unique account number
        var accountNumber = $"ACC-{Guid.NewGuid().ToString().Substring(0, 6)}";
        
        // Create the command using the factory method
        var createCommand = CreateAccount.Create(
            accountId,
            accountNumber,
            customerName,
            initialDeposit,
            accountType
        );
        
        // Send the command
        _commandBus.Send(createCommand);
        
        _logger.LogInformation("Account creation command sent for {AccountId}", accountId);
        
        return accountId;
    }
    
    public void DepositFunds(Guid accountId, decimal amount, string reference)
    {
        _logger.LogInformation("Depositing {Amount} to account {AccountId}", amount, accountId);
        
        // Create the command
        var depositCommand = DepositFunds.Create(
            accountId,
            amount,
            reference,
            null // No source message in this context
        );
        
        // Send the command
        _commandBus.Send(depositCommand);
        
        _logger.LogInformation("Deposit command sent for {AccountId}", accountId);
    }
    
    public async Task<TransferResult> TransferFundsAsync(
        Guid sourceAccountId, 
        Guid targetAccountId, 
        decimal amount, 
        string reference)
    {
        _logger.LogInformation("Transferring {Amount} from {SourceAccountId} to {TargetAccountId}",
            amount, sourceAccountId, targetAccountId);
        
        // Create the command
        var transferCommand = TransferFunds.Create(
            Guid.NewGuid(), // Transfer ID
            sourceAccountId,
            targetAccountId,
            amount,
            reference,
            null // No source message in this context
        );
        
        // Send the command and await the result
        var result = await _commandBus.SendAsync<TransferFunds, TransferResult>(transferCommand);
        
        _logger.LogInformation("Transfer completed with status {Status}", result.Status);
        
        return result;
    }
}
```

#### Command Bus Implementation

Reactive Domain provides a default implementation of the command bus that uses dependency injection to resolve command handlers:

```csharp
public class DefaultCommandBus : ICommandBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefaultCommandBus> _logger;
    
    public DefaultCommandBus(IServiceProvider serviceProvider, ILogger<DefaultCommandBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        _logger.LogDebug("Sending command of type {CommandType}", typeof(TCommand).Name);
        
        var handler = ResolveHandler<TCommand>();
        handler.Handle(command);
    }
    
    public async Task SendAsync<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        _logger.LogDebug("Sending async command of type {CommandType}", typeof(TCommand).Name);
        
        var handler = ResolveHandler<TCommand>();
        
        if (handler is IAsyncCommandHandler<TCommand> asyncHandler)
        {
            await asyncHandler.HandleAsync(command);
        }
        else
        {
            handler.Handle(command);
        }
    }
    
    // Other methods omitted for brevity
    
    private ICommandHandler<TCommand> ResolveHandler<TCommand>() where TCommand : class, ICommand
    {
        var handler = _serviceProvider.GetService<ICommandHandler<TCommand>>();
        
        if (handler == null)
        {
            throw new CommandHandlerNotFoundException(typeof(TCommand));
        }
        
        return handler;
    }
}
```

#### Command Bus Registration

To use the command bus, you need to register it and all command handlers in your dependency injection container:

```csharp
// In Startup.ConfigureServices or equivalent
public void ConfigureServices(IServiceCollection services)
{
    // Register the command bus
    services.AddSingleton<ICommandBus, DefaultCommandBus>();
    
    // Register command handlers
    services.AddTransient<ICommandHandler<CreateAccount>, CreateAccountHandler>();
    services.AddTransient<ICommandHandler<DepositFunds>, DepositFundsHandler>();
    services.AddTransient<ICommandHandler<WithdrawFunds>, WithdrawFundsHandler>();
    services.AddTransient<ICommandHandler<TransferFunds>, TransferFundsHandler>();
    
    // Register validators
    services.AddTransient<ICommandValidator<CreateAccount>, CreateAccountValidator>();
    
    // Register other dependencies
    services.AddSingleton<ICorrelatedRepository, EventStoreRepository>();
    // ...
}
```

#### Command Bus Benefits

1. **Decoupling**: Separates command senders from handlers
2. **Centralized Dispatch**: Provides a single entry point for all commands
3. **Extensibility**: Easily add cross-cutting concerns like logging, validation, and authorization
4. **Testability**: Makes it easy to mock the command bus for testing
5. **Async Support**: Supports both synchronous and asynchronous command handling
```

## Integration with Aggregates

Commands are used to modify aggregates, which then produce events. The typical flow is:

1. A command is sent to a command handler
2. The command handler loads the appropriate aggregate
3. The command handler calls a method on the aggregate, passing the command
4. The aggregate validates the command against its current state and business rules
5. If valid, the aggregate calls `RaiseEvent()` to create one or more events
6. The `RaiseEvent()` method both updates the aggregate's state via `Apply()` methods and records the events
7. The command handler saves the aggregate, persisting the new events

This pattern ensures that all state changes are captured as events and that business rules are enforced consistently:

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

1. **Immutable Commands**: Make all command properties read-only to prevent modification after creation
2. **Imperative Naming**: Use imperative verb naming convention (e.g., `CreateAccount`, `DepositFunds`)
3. **Command Validation**: Validate commands early in the processing pipeline
4. **Single Responsibility**: Each command should represent a single action or intention
5. **Use MessageBuilder**: Use `MessageBuilder` to create commands with proper correlation information
6. **Command Documentation**: Document the purpose, parameters, and possible outcomes of each command
7. **Versioning Strategy**: Plan for command schema evolution to handle changes over time
8. **Proper Event Creation**: Always use `RaiseEvent()` in aggregate methods that handle commands to create events
9. **Business Rule Enforcement**: Enforce all business rules in command handlers or aggregate methods before raising events
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
