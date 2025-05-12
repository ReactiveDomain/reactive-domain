# Banking Domain Example

This example demonstrates how to implement a banking application using Reactive Domain concepts, including CQRS, Event Sourcing, and correlation tracking.

## Commands

```csharp
// Command definition
public class TransferFunds : Command, ICorrelatedMessage
{
    public Guid SourceAccountId { get; }
    public Guid TargetAccountId { get; }
    public decimal Amount { get; }
    public string Reference { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public TransferFunds(Guid sourceAccountId, Guid targetAccountId, decimal amount, string reference, 
                         Guid msgId, Guid correlationId, Guid causationId)
    {
        // Validate business rules
        if (sourceAccountId == targetAccountId)
            throw new ArgumentException("Source and target accounts cannot be the same");
        if (amount <= 0)
            throw new ArgumentException("Transfer amount must be positive");
            
        SourceAccountId = sourceAccountId;
        TargetAccountId = targetAccountId;
        Amount = amount;
        Reference = reference;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Events

```csharp
public class FundsWithdrawn : Event, ICorrelatedMessage
{
    public Guid AccountId { get; }
    public decimal Amount { get; }
    public string Reference { get; }
    public DateTime Timestamp { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Constructor with MessageBuilder usage example
    public static FundsWithdrawn Create(Guid accountId, decimal amount, string reference, ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new FundsWithdrawn(
            accountId, amount, reference, DateTime.UtcNow,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    // Private constructor used by the factory method
    private FundsWithdrawn(Guid accountId, decimal amount, string reference, DateTime timestamp,
                          Guid msgId, Guid correlationId, Guid causationId)
    {
        AccountId = accountId;
        Amount = amount;
        Reference = reference;
        Timestamp = timestamp;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}

public class FundsDeposited : Event, ICorrelatedMessage
{
    public Guid AccountId { get; }
    public decimal Amount { get; }
    public string Reference { get; }
    public DateTime Timestamp { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    // Constructor with MessageBuilder usage example
    public static FundsDeposited Create(Guid accountId, decimal amount, string reference, ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new FundsDeposited(
            accountId, amount, reference, DateTime.UtcNow,
            Guid.NewGuid(), source.CorrelationId, source.MsgId));
    }
    
    // Private constructor used by the factory method
    private FundsDeposited(Guid accountId, decimal amount, string reference, DateTime timestamp,
                          Guid msgId, Guid correlationId, Guid causationId)
    {
        AccountId = accountId;
        Amount = amount;
        Reference = reference;
        Timestamp = timestamp;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}

public class TransferCompleted : Event, ICorrelatedMessage
{
    public Guid SourceAccountId { get; }
    public Guid TargetAccountId { get; }
    public decimal Amount { get; }
    public string Reference { get; }
    public DateTime Timestamp { get; }
    
    // Correlation properties
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public TransferCompleted(
        Guid sourceAccountId, 
        Guid targetAccountId, 
        decimal amount, 
        string reference, 
        DateTime timestamp,
        Guid msgId, 
        Guid correlationId, 
        Guid causationId)
    {
        SourceAccountId = sourceAccountId;
        TargetAccountId = targetAccountId;
        Amount = amount;
        Reference = reference;
        Timestamp = timestamp;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
```

## Aggregate Implementation

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isActive;
    private AccountStatus _status;
    private List<TransactionRecord> _recentTransactions = new List<TransactionRecord>();
    
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
        Register<AccountFrozen>(Apply);
        Register<AccountUnfrozen>(Apply);
        Register<AccountClosed>(Apply);
    }
    
    // Command handler methods
    public void Withdraw(decimal amount, string reference, ICorrelatedMessage source)
    {
        // Business rules validation
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (_status == AccountStatus.Frozen)
            throw new InvalidOperationException("Cannot withdraw from a frozen account");
            
        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive");
            
        if (_balance < amount)
            throw new InsufficientFundsException($"Insufficient funds. Current balance: {_balance}, Requested: {amount}");
            
        // Raise the event using MessageBuilder for correlation
        RaiseEvent(FundsWithdrawn.Create(Id, amount, reference, source));
    }
    
    public void Deposit(decimal amount, string reference, ICorrelatedMessage source)
    {
        // Business rules validation
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (_status == AccountStatus.Frozen)
            throw new InvalidOperationException("Cannot deposit to a frozen account");
            
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive");
            
        // Raise the event using MessageBuilder for correlation
        RaiseEvent(FundsDeposited.Create(Id, amount, reference, source));
    }
    
    // Event handler methods
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
        
        // Maintain a list of recent transactions for quick lookup
        _recentTransactions.Add(new TransactionRecord(
            TransactionType.Withdrawal,
            @event.Amount,
            @event.Reference,
            @event.Timestamp));
            
        // Trim the list to keep only the 10 most recent transactions
        if (_recentTransactions.Count > 10)
            _recentTransactions.RemoveAt(0);
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
        
        // Maintain a list of recent transactions for quick lookup
        _recentTransactions.Add(new TransactionRecord(
            TransactionType.Deposit,
            @event.Amount,
            @event.Reference,
            @event.Timestamp));
            
        // Trim the list to keep only the 10 most recent transactions
        if (_recentTransactions.Count > 10)
            _recentTransactions.RemoveAt(0);
    }
    
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _status = AccountStatus.Normal;
        _balance = @event.InitialBalance;
    }
    
    // Helper class for tracking recent transactions
    private class TransactionRecord
    {
        public TransactionType Type { get; }
        public decimal Amount { get; }
        public string Reference { get; }
        public DateTime Timestamp { get; }
        
        public TransactionRecord(TransactionType type, decimal amount, string reference, DateTime timestamp)
        {
            Type = type;
            Amount = amount;
            Reference = reference;
            Timestamp = timestamp;
        }
    }
    
    private enum TransactionType
    {
        Deposit,
        Withdrawal
    }
    
    private enum AccountStatus
    {
        Normal,
        Frozen,
        Closed
    }
}
```

## Command Handler Implementation

```csharp
public class TransferFundsHandler : ICommandHandler<TransferFunds>
{
    private readonly ICorrelatedRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TransferFundsHandler> _logger;
    
    public TransferFundsHandler(
        ICorrelatedRepository repository,
        IEventBus eventBus,
        ILogger<TransferFundsHandler> logger)
    {
        _repository = repository;
        _eventBus = eventBus;
        _logger = logger;
    }
    
    public void Handle(TransferFunds command)
    {
        _logger.LogInformation("Processing transfer: {Amount} from {SourceId} to {TargetId}",
            command.Amount, command.SourceAccountId, command.TargetAccountId);
            
        try
        {
            // Load both accounts with correlation context
            var sourceAccount = _repository.GetById<Account>(command.SourceAccountId, command);
            var targetAccount = _repository.GetById<Account>(command.TargetAccountId, command);
            
            // Execute the transfer
            sourceAccount.Withdraw(command.Amount, $"Transfer to {command.TargetAccountId}: {command.Reference}", command);
            targetAccount.Deposit(command.Amount, $"Transfer from {command.SourceAccountId}: {command.Reference}", command);
            
            // Save both accounts
            _repository.Save(sourceAccount);
            _repository.Save(targetAccount);
            
            // Publish a transfer completed event
            _eventBus.Publish(MessageBuilder.From(command, () => 
                new TransferCompleted(
                    command.SourceAccountId,
                    command.TargetAccountId,
                    command.Amount,
                    command.Reference,
                    DateTime.UtcNow,
                    Guid.NewGuid(),
                    command.CorrelationId,
                    command.MsgId)));
                    
            _logger.LogInformation("Transfer completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer failed: {ErrorMessage}", ex.Message);
            
            // Publish a transfer failed event
            _eventBus.Publish(MessageBuilder.From(command, () => 
                new TransferFailed(
                    command.SourceAccountId,
                    command.TargetAccountId,
                    command.Amount,
                    command.Reference,
                    ex.Message,
                    DateTime.UtcNow,
                    Guid.NewGuid(),
                    command.CorrelationId,
                    command.MsgId)));
                    
            throw;
        }
    }
}
```

## Read Model Implementation

```csharp
public class AccountSummaryReadModel : ReadModelBase
{
    public string AccountNumber { get; set; }
    public string AccountHolderName { get; set; }
    public decimal CurrentBalance { get; set; }
    public AccountStatus Status { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<TransactionSummary> RecentTransactions { get; set; } = new List<TransactionSummary>();
    
    public class TransactionSummary
    {
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public enum AccountStatus
    {
        Active,
        Frozen,
        Closed
    }
}
```

## Read Model Updater

```csharp
public class AccountSummaryUpdater : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<AccountFrozen>,
    IEventHandler<AccountClosed>
{
    private readonly IReadModelRepository<AccountSummaryReadModel> _readModelRepository;
    private readonly ILogger<AccountSummaryUpdater> _logger;
    
    public AccountSummaryUpdater(
        IReadModelRepository<AccountSummaryReadModel> readModelRepository,
        ILogger<AccountSummaryUpdater> logger)
    {
        _readModelRepository = readModelRepository;
        _logger = logger;
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        try
        {
            // Get the read model
            var readModel = _readModelRepository.GetById(@event.AccountId);
            
            // Update the read model
            readModel.CurrentBalance -= @event.Amount;
            readModel.LastUpdated = @event.Timestamp;
            
            // Add to recent transactions
            readModel.RecentTransactions.Add(new AccountSummaryReadModel.TransactionSummary
            {
                Type = "Withdrawal",
                Amount = @event.Amount,
                Reference = @event.Reference,
                Timestamp = @event.Timestamp
            });
            
            // Keep only the 10 most recent transactions
            if (readModel.RecentTransactions.Count > 10)
                readModel.RecentTransactions.RemoveAt(0);
            
            // Save the updated read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Updated account summary for withdrawal: {AccountId}, New Balance: {Balance}",
                @event.AccountId, readModel.CurrentBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account summary for withdrawal: {AccountId}", @event.AccountId);
            throw;
        }
    }
    
    public void Handle(FundsDeposited @event)
    {
        try
        {
            // Get the read model
            var readModel = _readModelRepository.GetById(@event.AccountId);
            
            // Update the read model
            readModel.CurrentBalance += @event.Amount;
            readModel.LastUpdated = @event.Timestamp;
            
            // Add to recent transactions
            readModel.RecentTransactions.Add(new AccountSummaryReadModel.TransactionSummary
            {
                Type = "Deposit",
                Amount = @event.Amount,
                Reference = @event.Reference,
                Timestamp = @event.Timestamp
            });
            
            // Keep only the 10 most recent transactions
            if (readModel.RecentTransactions.Count > 10)
                readModel.RecentTransactions.RemoveAt(0);
            
            // Save the updated read model
            _readModelRepository.Save(readModel);
            
            _logger.LogInformation("Updated account summary for deposit: {AccountId}, New Balance: {Balance}",
                @event.AccountId, readModel.CurrentBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account summary for deposit: {AccountId}", @event.AccountId);
            throw;
        }
    }
    
    // Additional event handlers...
}
```

## API Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IReadModelRepository<AccountSummaryReadModel> _readModelRepository;
    
    public AccountsController(
        ICommandBus commandBus,
        IReadModelRepository<AccountSummaryReadModel> readModelRepository)
    {
        _commandBus = commandBus;
        _readModelRepository = readModelRepository;
    }
    
    [HttpPost("transfer")]
    public IActionResult TransferFunds([FromBody] TransferFundsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
            
        try
        {
            // Create a command with correlation
            var command = new TransferFunds(
                request.SourceAccountId,
                request.TargetAccountId,
                request.Amount,
                request.Reference,
                Guid.NewGuid(),
                Guid.NewGuid(), // New correlation ID for this transaction
                Guid.Empty);    // No causation ID for the initial command
                
            // Send the command
            _commandBus.Send(command);
            
            return Accepted(new { TransactionId = command.MsgId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    [HttpGet("{id}")]
    public IActionResult GetAccount(Guid id)
    {
        try
        {
            var account = _readModelRepository.GetById(id);
            
            if (account == null)
                return NotFound();
                
            return Ok(account);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    public class TransferFundsRequest
    {
        [Required]
        public Guid SourceAccountId { get; set; }
        
        [Required]
        public Guid TargetAccountId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }
        
        public string Reference { get; set; }
    }
}
```

## Key Concepts Demonstrated

1. **CQRS Pattern**: Separation of commands (write operations) and queries (read operations)
2. **Event Sourcing**: Using events to represent state changes and reconstruct state
3. **Domain-Driven Design**: Rich domain models with business rules and validations
4. **Value Objects**: Immutable objects that represent concepts in the domain
5. **Correlation**: Tracking related messages through the system using `ICorrelatedMessage` and `MessageBuilder`
6. **Repository Pattern**: Using `ICorrelatedRepository` to load and save aggregates
7. **Read Models**: Specialized models for querying data efficiently
8. **Event Handlers**: Components that update read models based on domain events
9. **Command Handlers**: Components that process commands and update aggregates
10. **API Integration**: Exposing the domain through a REST API
