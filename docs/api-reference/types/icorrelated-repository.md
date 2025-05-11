# ICorrelatedRepository Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICorrelatedRepository` interface extends the repository pattern with correlation support. It allows tracking correlation and causation IDs across message flows when working with event-sourced aggregates.

## Correlation in Event Sourcing

In distributed systems and complex business processes, tracking the flow of messages and events is crucial for:

1. **Debugging and Troubleshooting**: Tracing the path of a business transaction through the system
2. **Auditing**: Maintaining a complete record of what caused each state change
3. **Business Process Monitoring**: Tracking the progress of long-running business processes
4. **Distributed Tracing**: Following transactions across service boundaries

The `ICorrelatedRepository` provides this capability by ensuring that correlation information is propagated from commands to the events they generate. When an aggregate is loaded with a source message, the correlation context is established, and all events raised by that aggregate will inherit the correlation information.

**Namespace**: `ReactiveDomain.Foundation`  
**Assembly**: `ReactiveDomain.Foundation.dll`

```csharp
public interface ICorrelatedRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    void Save(IEventSource aggregate);
    void Delete(IEventSource aggregate);
    void HardDelete(IEventSource aggregate);
}
```

## Methods

### TryGetById<TAggregate>(Guid, out TAggregate, ICorrelatedMessage)

Attempts to retrieve an aggregate by its ID with correlation information.

```csharp
bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `aggregate` (`TAggregate`): When this method returns, contains the aggregate with the specified ID, if found; otherwise, the default value for the type of the `aggregate` parameter.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `System.Boolean` - `true` if the aggregate was found; otherwise, `false`.

**Remarks**: This method attempts to retrieve an aggregate by its ID and sets up correlation information. If the aggregate is not found, it returns `false` and sets `aggregate` to `null`.

### TryGetById<TAggregate>(Guid, int, out TAggregate, ICorrelatedMessage)

Attempts to retrieve an aggregate by its ID and version with correlation information.

```csharp
bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `version` (`System.Int32`): The version of the aggregate to retrieve.
- `aggregate` (`TAggregate`): When this method returns, contains the aggregate with the specified ID and version, if found; otherwise, the default value for the type of the `aggregate` parameter.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `System.Boolean` - `true` if the aggregate was found; otherwise, `false`.

**Remarks**: This method attempts to retrieve an aggregate by its ID and version and sets up correlation information. If the aggregate is not found, it returns `false` and sets `aggregate` to `null`.

### GetById<TAggregate>(Guid, ICorrelatedMessage)

Retrieves an aggregate by its ID with correlation information.

```csharp
TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `TAggregate` - The aggregate with the specified ID.

**Exceptions**:
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.

**Remarks**: This method retrieves an aggregate by its ID and sets up correlation information. If the aggregate is not found, it throws an exception.

### GetById<TAggregate>(Guid, int, ICorrelatedMessage)

Retrieves an aggregate by its ID and version with correlation information.

```csharp
TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `version` (`System.Int32`): The version of the aggregate to retrieve.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `TAggregate` - The aggregate with the specified ID and version.

**Exceptions**:
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.
- `ReactiveDomain.AggregateVersionException`: Thrown when the specified version does not match the expected version.

**Remarks**: This method retrieves an aggregate by its ID and version and sets up correlation information. If the aggregate is not found, it throws an exception.

### Save

Saves an aggregate to the repository.

```csharp
void Save(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to save.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Remarks**: This method saves an aggregate to the repository. It takes the events from the aggregate and appends them to the event stream in the repository.

### Delete

Marks an aggregate as deleted in the repository.

```csharp
void Delete(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to delete.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Remarks**: This method marks an aggregate as deleted in the repository. It appends a deletion event to the event stream. The aggregate can still be retrieved, but will be marked as deleted.

### HardDelete

Permanently deletes an aggregate from the repository.

```csharp
void HardDelete(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to delete.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Remarks**: This method permanently deletes an aggregate from the repository. It removes the event stream from the repository. The aggregate cannot be retrieved after this operation.

## Usage

### Basic Usage with Correlation

The `ICorrelatedRepository` interface is used to store and retrieve event-sourced aggregates with correlation information. It is typically implemented by the `CorrelatedStreamStoreRepository` class.

```csharp
// Create a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
var eventStoreConnection = new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113);
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, eventStoreConnection, serializer);
var correlatedRepository = new CorrelatedStreamStoreRepository(repository);

// Create a command with correlation information
ICorrelatedMessage command = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid()));

// Create a new aggregate with correlation information
var account = new Account(Guid.NewGuid());
account.CreateAccount("12345", command); // Pass the command to establish correlation

// Save the aggregate
correlatedRepository.Save(account);

// Retrieve the aggregate with correlation information
var retrievedAccount = correlatedRepository.GetById<Account>(account.Id, command);

// Update the aggregate with correlation
retrievedAccount.Deposit(100, command);
correlatedRepository.Save(retrievedAccount);

// Delete the aggregate
correlatedRepository.Delete(retrievedAccount);
```

### Integration with Command Handlers

The `ICorrelatedRepository` is particularly useful in command handlers where correlation tracking is important:

```csharp
public class CorrelatedAccountCommandHandler : 
    ICommandHandler<CreateAccount>,
    ICommandHandler<DepositFunds>,
    ICommandHandler<WithdrawFunds>,
    ICommandHandler<CloseAccount>
{
    private readonly ICorrelatedRepository _repository;
    private readonly IEventBus _eventBus;
    
    public CorrelatedAccountCommandHandler(ICorrelatedRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(CreateAccount command)
    {
        // Check if account already exists
        Account account;
        if (_repository.TryGetById(command.AccountId, out account, command))
        {
            throw new InvalidOperationException($"Account {command.AccountId} already exists");
        }
        
        // Create new account with correlation
        account = new Account(command.AccountId);
        account.CreateAccount(command.AccountNumber, command.InitialDeposit, command);
        
        // Save the account
        _repository.Save(account);
        
        // Events are automatically correlated with the command
        // This allows tracing the entire transaction flow
    }
    
    public void Handle(DepositFunds command)
    {
        // Get the account with correlation
        var account = _repository.GetById<Account>(command.AccountId, command);
        
        // Process the command with correlation
        account.Deposit(command.Amount, command);
        
        // Save the account
        _repository.Save(account);
    }
    
    // Additional handlers...
}
```

### Tracing Business Processes

Correlation is particularly valuable for tracing business processes that span multiple aggregates:

```csharp
public class TransferFundsProcess
{
    private readonly ICorrelatedRepository _repository;
    
    public TransferFundsProcess(ICorrelatedRepository repository)
    {
        _repository = repository;
    }
    
    public void ExecuteTransfer(TransferFunds command)
    {
        // Load both accounts with correlation
        var sourceAccount = _repository.GetById<Account>(command.SourceAccountId, command);
        var targetAccount = _repository.GetById<Account>(command.TargetAccountId, command);
        
        try
        {
            // Withdraw from source account with correlation
            sourceAccount.Withdraw(command.Amount, command);
            
            // Deposit to target account with correlation
            targetAccount.Deposit(command.Amount, command);
            
            // Save both accounts
            _repository.Save(sourceAccount);
            _repository.Save(targetAccount);
            
            // All events will have the same correlation ID but different causation IDs
            // This allows tracing the entire transfer process
        }
        catch (Exception ex)
        {
            // Handle errors
            // The correlation ID can be used to trace the error through the system
        }
    }
}
```

## Correlation and Causation

The `ICorrelatedRepository` interface helps track correlation and causation IDs across message flows:

- **Correlation ID**: Identifies a business transaction that spans multiple messages
- **Causation ID**: Identifies the message that caused the current message
- **Message ID**: Uniquely identifies each message in the system

When an aggregate is loaded with a source message, the source message's correlation and causation IDs are propagated to any events raised by the aggregate. This allows tracking the flow of messages through the system.

### How Correlation Works

1. **Initial Command**: A command enters the system with a new correlation ID (equal to its message ID) and no causation ID
2. **Command Handler**: Loads an aggregate using the correlated repository, passing the command as the source
3. **Aggregate Operations**: The aggregate raises events, which inherit the correlation ID from the command
4. **Event Causation**: Each event's causation ID is set to the command's message ID
5. **Event Handlers**: Process events and may issue new commands, maintaining the correlation chain

### Correlation Chain Example

```
Command: CreateAccount
- MsgId: A
- CorrelationId: A
- CausationId: null

  Event: AccountCreated
  - MsgId: B
  - CorrelationId: A
  - CausationId: A

    Command: SendWelcomeEmail
    - MsgId: C
    - CorrelationId: A
    - CausationId: B

      Event: EmailSent
      - MsgId: D
      - CorrelationId: A
      - CausationId: C
```

This chain allows tracing the entire business transaction from start to finish, even across multiple services and message handlers.

### Benefits of Correlation Tracking

1. **Debugging**: Easily trace related messages through logs and monitoring systems
2. **Auditing**: Maintain a complete record of business transactions
3. **Idempotency**: Detect and handle duplicate messages
4. **Process Monitoring**: Track the progress of long-running business processes
5. **Distributed Tracing**: Follow transactions across service boundaries

## Best Practices

1. **Always Use Correlation**: Use correlated repositories for all production systems to maintain traceability
2. **Pass Commands to Aggregates**: Always pass the command to aggregate methods to maintain correlation
3. **Correlation in Sagas**: Use correlation IDs to track long-running business processes (sagas)
4. **Logging with Correlation**: Include correlation IDs in log messages for easier troubleshooting
5. **Monitoring**: Set up monitoring based on correlation IDs to track business processes
6. **Error Handling**: Use correlation IDs to track errors through the system
7. **Testing**: Verify that correlation IDs are properly propagated in unit tests

## Advanced Scenarios

### Distributed Systems

In distributed systems, correlation IDs should be propagated across service boundaries:

```csharp
public class ExternalServiceClient
{
    private readonly HttpClient _httpClient;
    
    public ExternalServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task CallExternalService(ICorrelatedMessage source, string data)
    {
        // Add correlation headers to the request
        var request = new HttpRequestMessage(HttpMethod.Post, "api/endpoint");
        request.Headers.Add("X-Correlation-ID", source.CorrelationId.ToString());
        request.Headers.Add("X-Causation-ID", source.MsgId.ToString());
        
        // Add request body
        request.Content = new StringContent(data, Encoding.UTF8, "application/json");
        
        // Send the request
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
```

### Correlation with Event Sourcing and CQRS

In a full CQRS architecture, correlation IDs help maintain consistency between read and write models:

```csharp
public class ReadModelUpdater : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>
{
    private readonly IReadModelRepository<AccountReadModel> _readModelRepository;
    private readonly ILogger _logger;
    
    public ReadModelUpdater(IReadModelRepository<AccountReadModel> readModelRepository, ILogger logger)
    {
        _readModelRepository = readModelRepository;
        _logger = logger;
    }
    
    public void Handle(AccountCreated @event)
    {
        _logger.LogInformation(
            "Updating read model for AccountCreated event. CorrelationId: {@CorrelationId}, CausationId: {@CausationId}",
            @event.CorrelationId, @event.CausationId);
            
        var readModel = new AccountReadModel
        {
            Id = @event.AccountId,
            AccountNumber = @event.AccountNumber,
            Balance = @event.InitialDeposit,
            IsActive = true,
            LastUpdated = DateTime.UtcNow,
            LastEventId = @event.MsgId // Store the event ID for idempotency
        };
        
        _readModelRepository.Save(readModel);
    }
    
    // Additional event handlers...
}
```

## Related Types

- [IRepository](irepository.md): The base repository interface
- [IEventSource](ievent-source.md): The interface for event-sourced entities
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [ICorrelatedEventSource](icorrelated-event-source.md): Interface for correlation tracking in event sources
- [MessageBuilder](message-builder.md): Factory for creating correlated messages
- [CorrelatedStreamStoreRepository](correlated-stream-store-repository.md): Implementation of `ICorrelatedRepository`

---

**Navigation**:
- [← Previous: IRepository](./irepository.md)
- [↑ Back to Top](#icorrelatedrepository-interface)
- [→ Next: ICorrelatedEventSource](./icorrelated-event-source.md)
