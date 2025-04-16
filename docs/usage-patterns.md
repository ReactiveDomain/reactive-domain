# Usage Patterns for Reactive Domain

[← Back to Table of Contents](README.md)

This document outlines common usage patterns and best practices for working with the Reactive Domain library. These patterns will help you implement event sourcing effectively in your applications.

## Table of Contents

- [Setting Up a New Reactive Domain Project](#setting-up-a-new-reactive-domain-project)
- [Creating and Working with Aggregates](#creating-and-working-with-aggregates)
- [Implementing Commands and Events](#implementing-commands-and-events)
- [Setting Up Repositories and Event Stores](#setting-up-repositories-and-event-stores)
- [Implementing Projections and Read Models](#implementing-projections-and-read-models)
- [Handling Concurrency and Versioning](#handling-concurrency-and-versioning)
- [Error Handling and Recovery Strategies](#error-handling-and-recovery-strategies)
- [Testing Event-Sourced Systems](#testing-event-sourced-systems)
- [Performance Optimization Techniques](#performance-optimization-techniques)
- [Integration with Other Systems and Frameworks](#integration-with-other-systems-and-frameworks)
- [Conclusion](#conclusion)

## Setting Up a New Reactive Domain Project

### Project Structure

A typical Reactive Domain project consists of the following components:

```
MyProject/
├── Domain/
│   ├── Aggregates/
│   ├── Commands/
│   ├── Events/
│   └── ValueObjects/
├── Application/
│   ├── CommandHandlers/
│   ├── EventHandlers/
│   └── Services/
├── Infrastructure/
│   ├── Repositories/
│   ├── Projections/
│   └── ReadModels/
└── API/
    ├── Controllers/
    └── DTOs/
```

### NuGet Packages

Add the following NuGet packages to your project:

```xml
<PackageReference Include="ReactiveDomain" Version="x.y.z" />
<PackageReference Include="ReactiveDomain.Testing" Version="x.y.z" Condition="'$(Configuration)' == 'Debug'" />
```

### Bootstrapping

Set up the event store connection and repositories in your application startup:

```csharp
// Create an event store connection
var connectionSettings = ConnectionSettings.Create()
    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
    .Build();
var eventStoreConnection = new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113);
eventStoreConnection.Connect();

// Create a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, eventStoreConnection, serializer);

// Create a correlated repository (optional)
var correlatedRepository = new CorrelatedStreamStoreRepository(repository);

// Register repositories in your DI container
services.AddSingleton<IRepository>(repository);
services.AddSingleton<ICorrelatedRepository>(correlatedRepository);
```

## Creating and Working with Aggregates

### Defining an Aggregate

Aggregates are the primary entities in your domain. They encapsulate state and behavior, and they're the source of events.

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    // Constructor for creating a new account
    public Account(Guid id) : base(id)
    {
    }
    
    // Constructor for creating a new account with correlation
    public Account(Guid id, ICorrelatedMessage source) : base(id, source)
    {
    }
    
    // Constructor for restoring an account from events
    protected Account(Guid id, IEnumerable<object> events) : base(id, events)
    {
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        RaiseEvent(new AmountDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new AmountWithdrawn(Id, amount));
    }
    
    public decimal GetBalance()
    {
        return _balance;
    }
    
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

### Aggregate Design Principles

1. **Single Responsibility**: Each aggregate should represent a single concept in your domain.
2. **Encapsulation**: Aggregates should encapsulate their state and behavior.
3. **Consistency Boundaries**: Aggregates define consistency boundaries. Changes within an aggregate are atomic.
4. **Identity**: Each aggregate has a unique identity.
5. **Event-Driven**: Aggregates raise events to represent changes in state.
6. **Command Validation**: Aggregates validate commands before raising events.
7. **Event Application**: Aggregates apply events to update their state.

## Implementing Commands and Events

### Defining Commands

Commands represent requests to change the state of the system. They should be named in the imperative form.

```csharp
public class CreateAccount : Command
{
    public readonly Guid AccountId;
    
    public CreateAccount(Guid accountId)
    {
        AccountId = accountId;
    }
}

public class DepositMoney : Command
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public DepositMoney(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}

public class WithdrawMoney : Command
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public WithdrawMoney(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}
```

### Defining Events

Events represent something that happened in the system. They should be named in the past tense.

```csharp
public class AccountCreated : Event
{
    public readonly Guid AccountId;
    
    public AccountCreated(Guid accountId)
    {
        AccountId = accountId;
    }
}

public class AmountDeposited : Event
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public AmountDeposited(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}

public class AmountWithdrawn : Event
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public AmountWithdrawn(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}
```

### Command and Event Design Principles

1. **Immutability**: Commands and events should be immutable.
2. **Intent-Revealing Names**: Use names that clearly express intent.
3. **Complete Information**: Include all information needed to process the command or understand the event.
4. **Validation**: Validate commands before processing them.
5. **Serialization**: Ensure commands and events can be serialized and deserialized.

## Setting Up Repositories and Event Stores

### Using the Repository

The repository provides methods for loading and saving aggregates.

```csharp
// Load an aggregate
var account = repository.GetById<Account>(accountId);

// Save an aggregate
repository.Save(account);

// Delete an aggregate
repository.Delete(account);

// Hard delete an aggregate (use with caution)
repository.HardDelete(account);
```

### Using the Correlated Repository

The correlated repository ensures that correlation and causation IDs are properly propagated.

```csharp
// Load an aggregate with correlation
var account = correlatedRepository.GetById<Account>(accountId, sourceMessage);

// Save an aggregate
correlatedRepository.Save(account);
```

### Repository and Event Store Design Principles

1. **Abstraction**: Use the repository abstraction to hide the details of event storage.
2. **Optimistic Concurrency**: Use optimistic concurrency control to prevent conflicts.
3. **Correlation**: Use the correlated repository to track correlation and causation.
4. **Stream Naming**: Use a consistent stream naming convention.
5. **Serialization**: Use a consistent serialization format for events.

## Implementing Projections and Read Models

### Defining a Projection

Projections transform events into read models.

```csharp
public class AccountBalanceProjection : IEventHandler<AmountDeposited>, IEventHandler<AmountWithdrawn>
{
    private readonly IReadModelRepository<AccountBalance> _repository;
    
    public AccountBalanceProjection(IReadModelRepository<AccountBalance> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AmountDeposited @event)
    {
        var accountBalance = _repository.GetById(@event.AccountId) ?? new AccountBalance(@event.AccountId);
        accountBalance.Balance += @event.Amount;
        _repository.Save(accountBalance);
    }
    
    public void Handle(AmountWithdrawn @event)
    {
        var accountBalance = _repository.GetById(@event.AccountId);
        if (accountBalance == null)
            throw new InvalidOperationException("Account not found");
            
        accountBalance.Balance -= @event.Amount;
        _repository.Save(accountBalance);
    }
}
```

### Defining a Read Model

Read models represent the state of the system for querying.

```csharp
public class AccountBalance
{
    public Guid Id { get; }
    public decimal Balance { get; set; }
    
    public AccountBalance(Guid id)
    {
        Id = id;
        Balance = 0;
    }
}
```

### Projection and Read Model Design Principles

1. **Separation of Concerns**: Separate read models from write models.
2. **Denormalization**: Denormalize data for efficient querying.
3. **Eventual Consistency**: Accept that read models may be eventually consistent.
4. **Idempotence**: Ensure projections are idempotent.
5. **Rebuilding**: Design projections to be rebuildable from the event stream.

## Handling Concurrency and Versioning

### Optimistic Concurrency Control

Reactive Domain uses optimistic concurrency control to prevent conflicts.

```csharp
try
{
    var account = repository.GetById<Account>(accountId);
    account.Withdraw(amount);
    repository.Save(account);
}
catch (AggregateVersionException ex)
{
    // Handle concurrency conflict
    // Typically, you would reload the aggregate and retry the operation
}
```

### Event Versioning

As your system evolves, you may need to version your events.

```csharp
// Version 1 of the event
public class AmountDeposited : Event
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public AmountDeposited(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}

// Version 2 of the event
public class AmountDepositedV2 : Event
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    public readonly string Currency;
    
    public AmountDepositedV2(Guid accountId, decimal amount, string currency)
    {
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
    }
}
```

### Handling Event Versioning in Aggregates

```csharp
private void Apply(AmountDeposited @event)
{
    _balance += @event.Amount;
}

private void Apply(AmountDepositedV2 @event)
{
    // Convert to the account's currency if necessary
    _balance += @event.Amount;
}
```

### Concurrency and Versioning Design Principles

1. **Optimistic Concurrency**: Use optimistic concurrency control to prevent conflicts.
2. **Event Upcasting**: Use event upcasting to handle event versioning.
3. **Backward Compatibility**: Maintain backward compatibility for events.
4. **Forward Compatibility**: Design for forward compatibility where possible.
5. **Version Tracking**: Track event versions explicitly.

## Error Handling and Recovery Strategies

### Command Validation

Validate commands before processing them to prevent errors.

```csharp
public class DepositMoneyValidator : IValidator<DepositMoney>
{
    public ValidationResult Validate(DepositMoney command)
    {
        if (command.Amount <= 0)
            return ValidationResult.Error("Amount must be positive");
            
        return ValidationResult.Success();
    }
}
```

### Exception Handling

Handle exceptions appropriately in command handlers.

```csharp
public class AccountCommandHandler : ICommandHandler<DepositMoney>
{
    private readonly IRepository _repository;
    
    public AccountCommandHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(DepositMoney command)
    {
        try
        {
            var account = _repository.GetById<Account>(command.AccountId);
            account.Deposit(command.Amount);
            _repository.Save(account);
        }
        catch (AggregateNotFoundException)
        {
            // Handle the case where the account doesn't exist
            throw new CommandHandlingException("Account not found");
        }
        catch (AggregateVersionException)
        {
            // Handle concurrency conflict
            throw new CommandHandlingException("Concurrency conflict");
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            throw new CommandHandlingException("An error occurred", ex);
        }
    }
}
```

### Retry Strategies

Implement retry strategies for transient errors.

```csharp
public void HandleWithRetry(DepositMoney command, int maxRetries = 3)
{
    int retries = 0;
    while (true)
    {
        try
        {
            Handle(command);
            return;
        }
        catch (AggregateVersionException)
        {
            if (++retries > maxRetries)
                throw;
                
            // Wait before retrying
            Thread.Sleep(100 * retries);
        }
    }
}
```

### Error Handling and Recovery Design Principles

1. **Validation**: Validate commands before processing them.
2. **Exception Handling**: Handle exceptions appropriately.
3. **Retry Strategies**: Implement retry strategies for transient errors.
4. **Logging**: Log errors for troubleshooting.
5. **Compensation**: Implement compensation logic for failed operations.

## Testing Event-Sourced Systems

### Unit Testing Aggregates

Use the `ReactiveDomain.Testing` package to unit test aggregates.

```csharp
[Fact]
public void CanDepositMoney()
{
    // Arrange
    var accountId = Guid.NewGuid();
    var account = new Account(accountId);
    
    // Act
    account.Deposit(100);
    
    // Assert
    var events = ((IEventSource)account).TakeEvents();
    Assert.Single(events);
    var @event = Assert.IsType<AmountDeposited>(events[0]);
    Assert.Equal(accountId, @event.AccountId);
    Assert.Equal(100, @event.Amount);
    Assert.Equal(100, account.GetBalance());
}
```

### Integration Testing with In-Memory Event Store

Use the `MockStreamStoreConnection` for integration testing.

```csharp
[Fact]
public void CanSaveAndLoadAggregate()
{
    // Arrange
    var accountId = Guid.NewGuid();
    var mockStore = new MockStreamStoreConnection("testRepo");
    mockStore.Connect();
    var repository = new StreamStoreRepository(new PrefixedCamelCaseStreamNameBuilder(), mockStore, new JsonMessageSerializer());
    
    // Act
    var account = new Account(accountId);
    account.Deposit(100);
    repository.Save(account);
    
    var loadedAccount = repository.GetById<Account>(accountId);
    
    // Assert
    Assert.Equal(100, loadedAccount.GetBalance());
}
```

### Testing Projections

Test projections by feeding them events and verifying the read model.

```csharp
[Fact]
public void ProjectionUpdatesReadModel()
{
    // Arrange
    var accountId = Guid.NewGuid();
    var readModelRepository = new InMemoryReadModelRepository<AccountBalance>();
    var projection = new AccountBalanceProjection(readModelRepository);
    
    // Act
    projection.Handle(new AmountDeposited(accountId, 100));
    projection.Handle(new AmountDeposited(accountId, 50));
    projection.Handle(new AmountWithdrawn(accountId, 30));
    
    // Assert
    var accountBalance = readModelRepository.GetById(accountId);
    Assert.NotNull(accountBalance);
    Assert.Equal(120, accountBalance.Balance);
}
```

### Testing Design Principles

1. **Isolation**: Test aggregates in isolation.
2. **Event Verification**: Verify that the correct events are raised.
3. **State Verification**: Verify that the state is updated correctly.
4. **In-Memory Testing**: Use in-memory event stores for testing.
5. **Projection Testing**: Test projections by feeding them events.

## Performance Optimization Techniques

### Snapshots

Use snapshots to improve loading performance for aggregates with many events.

```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    // ... existing code ...
    
    public void RestoreFromSnapshot(object snapshot)
    {
        var accountSnapshot = (AccountSnapshot)snapshot;
        _balance = accountSnapshot.Balance;
        ExpectedVersion = accountSnapshot.Version;
    }
    
    public object TakeSnapshot()
    {
        return new AccountSnapshot
        {
            Balance = _balance,
            Version = ExpectedVersion
        };
    }
}

public class AccountSnapshot
{
    public decimal Balance { get; set; }
    public long Version { get; set; }
}
```

### Read Model Optimization

Optimize read models for the queries they need to support.

```csharp
public class AccountBalanceReadModel
{
    public Dictionary<Guid, decimal> AccountBalances { get; } = new Dictionary<Guid, decimal>();
    
    public void Handle(AmountDeposited @event)
    {
        if (!AccountBalances.TryGetValue(@event.AccountId, out var balance))
            balance = 0;
            
        AccountBalances[@event.AccountId] = balance + @event.Amount;
    }
    
    public void Handle(AmountWithdrawn @event)
    {
        if (!AccountBalances.TryGetValue(@event.AccountId, out var balance))
            throw new InvalidOperationException("Account not found");
            
        AccountBalances[@event.AccountId] = balance - @event.Amount;
    }
}
```

### Performance Optimization Design Principles

1. **Snapshots**: Use snapshots for aggregates with many events.
2. **Read Model Optimization**: Optimize read models for the queries they need to support.
3. **Caching**: Use caching to improve performance.
4. **Batching**: Batch operations where possible.
5. **Asynchronous Processing**: Use asynchronous processing for non-critical operations.

## Integration with Other Systems and Frameworks

### ASP.NET Core Integration

Integrate Reactive Domain with ASP.NET Core.

```csharp
public class AccountsController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IReadModelRepository<AccountBalance> _readModelRepository;
    
    public AccountsController(ICommandBus commandBus, IReadModelRepository<AccountBalance> readModelRepository)
    {
        _commandBus = commandBus;
        _readModelRepository = readModelRepository;
    }
    
    [HttpPost]
    public IActionResult CreateAccount()
    {
        var accountId = Guid.NewGuid();
        _commandBus.Send(new CreateAccount(accountId));
        return CreatedAtAction(nameof(GetAccount), new { id = accountId }, null);
    }
    
    [HttpPost("{id}/deposit")]
    public IActionResult Deposit(Guid id, [FromBody] DepositRequest request)
    {
        _commandBus.Send(new DepositMoney(id, request.Amount));
        return NoContent();
    }
    
    [HttpPost("{id}/withdraw")]
    public IActionResult Withdraw(Guid id, [FromBody] WithdrawRequest request)
    {
        _commandBus.Send(new WithdrawMoney(id, request.Amount));
        return NoContent();
    }
    
    [HttpGet("{id}")]
    public IActionResult GetAccount(Guid id)
    {
        var accountBalance = _readModelRepository.GetById(id);
        if (accountBalance == null)
            return NotFound();
            
        return Ok(new AccountResponse
        {
            Id = accountBalance.Id,
            Balance = accountBalance.Balance
        });
    }
}

public class DepositRequest
{
    public decimal Amount { get; set; }
}

public class WithdrawRequest
{
    public decimal Amount { get; set; }
}

public class AccountResponse
{
    public Guid Id { get; set; }
    public decimal Balance { get; set; }
}
```

### Integration with Other Event Sourcing Systems

Integrate Reactive Domain with other event sourcing systems.

```csharp
public class ExternalEventAdapter : IEventHandler<ExternalEvent>
{
    private readonly ICommandBus _commandBus;
    
    public ExternalEventAdapter(ICommandBus commandBus)
    {
        _commandBus = commandBus;
    }
    
    public void Handle(ExternalEvent @event)
    {
        // Map the external event to a command
        var command = MapToCommand(@event);
        
        // Send the command
        _commandBus.Send(command);
    }
    
    private ICommand MapToCommand(ExternalEvent @event)
    {
        // Map the external event to a command
        // ...
    }
}
```

### Integration Design Principles

1. **Loose Coupling**: Use loose coupling to integrate with other systems.
2. **Message-Based Integration**: Use message-based integration where possible.
3. **Adapters**: Use adapters to translate between different message formats.
4. **Idempotence**: Ensure idempotent processing of messages.
5. **Correlation**: Use correlation IDs to track messages across systems.

## Conclusion

These usage patterns provide a foundation for implementing event sourcing with Reactive Domain. By following these patterns and best practices, you can build robust, scalable, and maintainable event-sourced applications.

For more detailed information on specific components, see the [Component Documentation](components/README.md) section. For code examples, see the [Code Examples](code-examples/README.md) section.

[↑ Back to Top](#usage-patterns-for-reactive-domain) | [← Back to Table of Contents](README.md)
