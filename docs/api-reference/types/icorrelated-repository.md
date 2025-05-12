# ICorrelatedRepository Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICorrelatedRepository` interface extends the repository pattern with correlation support. It allows tracking correlation and causation IDs across message flows when working with event-sourced aggregates. This interface is crucial for implementing robust, traceable, and maintainable event-sourced systems.

In modern distributed applications, correlation tracking is not just a nice-to-have feature but an essential requirement for debugging, auditing, and monitoring business processes. The `ICorrelatedRepository` should be the default choice for all production systems.

## Correlation in Event Sourcing

In distributed systems and complex business processes, tracking the flow of messages and events is crucial for:

1. **Debugging and Troubleshooting**: Tracing the path of a business transaction through the system, making it easier to identify the root cause of issues
2. **Auditing and Compliance**: Maintaining a complete record of what caused each state change, essential for regulatory compliance and security investigations
3. **Business Process Monitoring**: Tracking the progress of long-running business processes across multiple services and components
4. **Distributed Tracing**: Following transactions across service boundaries in microservice architectures
5. **Idempotency Handling**: Detecting and properly handling duplicate messages to ensure exactly-once processing semantics
6. **Error Management**: Correlating errors with the operations that caused them for better error reporting and recovery

The `ICorrelatedRepository` provides these capabilities by ensuring that correlation information is propagated from commands to the events they generate. When an aggregate is loaded with a source message, the correlation context is established, and all events raised by that aggregate will inherit the correlation information.

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

The `ICorrelatedRepository` interface is used to store and retrieve event-sourced aggregates with correlation information. It is typically implemented by the `CorrelatedStreamStoreRepository` class and registered through dependency injection.

```csharp
// Register repositories in your DI container (typically in Startup.cs or Program.cs)
public void ConfigureServices(IServiceCollection services)
{
    // Register the base repository
    services.AddSingleton<IStreamNameBuilder, PrefixedCamelCaseStreamNameBuilder>();
    services.AddSingleton<IStreamStoreConnection>(provider => 
        new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113));
    services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
    services.AddSingleton<IRepository, StreamStoreRepository>();
    
    // Register the correlated repository as the default implementation
    services.AddSingleton<ICorrelatedRepository, CorrelatedStreamStoreRepository>();
    
    // Register command handlers and services
    services.AddTransient<ICommandHandler<CreateAccount>, AccountCommandHandler>();
    services.AddTransient<ICommandHandler<DepositFunds>, AccountCommandHandler>();
    // ... other registrations
}

// Example command handler using ICorrelatedRepository
public class AccountCommandHandler : 
    ICommandHandler<CreateAccount>,
    ICommandHandler<DepositFunds>
{
    private readonly ICorrelatedRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<AccountCommandHandler> _logger;
    
    public AccountCommandHandler(
        ICorrelatedRepository repository, 
        IEventPublisher eventPublisher,
        ILogger<AccountCommandHandler> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }
    
    public void Handle(CreateAccount command)
    {
        _logger.LogInformation("Processing CreateAccount command {CommandId}", command.MsgId);
        
        // Check if account already exists
        Account account;
        if (_repository.TryGetById(command.AccountId, out account, command))
        {
            throw new InvalidOperationException($"Account {command.AccountId} already exists");
        }
        
        // Create new account with correlation
        account = new Account(command.AccountId, command);
        
        // Save the account
        _repository.Save(account);
        
        // Publish events for read models and integration
        var events = account.TakeEvents();
        foreach (var @event in events)
        {
            _eventPublisher.Publish(@event);
            _logger.LogDebug("Published event {EventType} with ID {EventId}", 
                @event.GetType().Name, 
                (@event as ICorrelatedMessage)?.MsgId);
        }
    }
    
    public void Handle(DepositFunds command)
    {
        _logger.LogInformation("Processing DepositFunds command {CommandId}", command.MsgId);
        
        // Get the account with correlation
        var account = _repository.GetById<Account>(command.AccountId, command);
        
        // Process the command with correlation
        account.Deposit(command.Amount, command.Reference, command);
        
        // Save the account
        _repository.Save(account);
        
        // Publish events
        var events = account.TakeEvents();
        foreach (var @event in events)
        {
            _eventPublisher.Publish(@event);
        }
    }
}
```

### Advanced Command Handler Patterns

The `ICorrelatedRepository` enables sophisticated command handling patterns with robust error handling and correlation tracking:

```csharp
public abstract class BaseCommandHandler<TCommand> : ICommandHandler<TCommand> 
    where TCommand : ICommand, ICorrelatedMessage
{
    protected readonly ICorrelatedRepository Repository;
    protected readonly IEventPublisher EventPublisher;
    protected readonly ILogger Logger;
    
    protected BaseCommandHandler(
        ICorrelatedRepository repository,
        IEventPublisher eventPublisher,
        ILogger logger)
    {
        Repository = repository;
        EventPublisher = eventPublisher;
        Logger = logger;
    }
    
    public void Handle(TCommand command)
    {
        try
        {
            Logger.LogInformation(
                "Processing command {CommandType} with ID {CommandId}",
                typeof(TCommand).Name,
                command.MsgId);
                
            // Execute the command-specific logic
            HandleCore(command);
            
            Logger.LogInformation(
                "Successfully processed command {CommandType} with ID {CommandId}",
                typeof(TCommand).Name,
                command.MsgId);
        }
        catch (AggregateNotFoundException ex)
        {
            Logger.LogWarning(
                ex,
                "Aggregate not found while processing command {CommandType} with ID {CommandId}",
                typeof(TCommand).Name,
                command.MsgId);
                
            throw new CommandProcessingException(
                $"The requested {GetAggregateTypeName(ex.AggregateType)} with ID {ex.AggregateId} was not found",
                ex,
                command);
        }
        catch (AggregateDeletedException ex)
        {
            Logger.LogWarning(
                ex,
                "Deleted aggregate accessed while processing command {CommandType} with ID {CommandId}",
                typeof(TCommand).Name,
                command.MsgId);
                
            throw new CommandProcessingException(
                $"The requested {GetAggregateTypeName(ex.AggregateType)} with ID {ex.AggregateId} has been deleted",
                ex,
                command);
        }
        catch (AggregateVersionException ex)
        {
            Logger.LogWarning(
                ex,
                "Concurrency conflict detected while processing command {CommandType} with ID {CommandId}",
                typeof(TCommand).Name,
                command.MsgId);
                
            throw new ConcurrencyException(
                $"The {GetAggregateTypeName(ex.AggregateType)} with ID {ex.AggregateId} has been modified by another process",
                ex,
                command);
        }
        catch (Exception ex) when (!(ex is CommandProcessingException))
        {
            Logger.LogError(
                ex,
                "Error processing command {CommandType} with ID {CommandId}",
                typeof(TCommand).Name,
                command.MsgId);
                
            throw new CommandProcessingException(
                $"An error occurred while processing {typeof(TCommand).Name}",
                ex,
                command);
        }
    }
    
    protected abstract void HandleCore(TCommand command);
    
    protected void PublishEvents(IEnumerable<object> events)
    {
        foreach (var @event in events)
        {
            EventPublisher.Publish(@event);
            
            if (@event is ICorrelatedMessage correlatedEvent)
            {
                Logger.LogDebug(
                    "Published event {EventType} with ID {EventId}, correlation {CorrelationId}",
                    @event.GetType().Name,
                    correlatedEvent.MsgId,
                    correlatedEvent.CorrelationId);
            }
        }
    }
    
    private string GetAggregateTypeName(Type aggregateType)
    {
        return aggregateType?.Name.Replace("Aggregate", "") ?? "entity";
    }
}

// Concrete implementation for a specific command
public class CreateAccountHandler : BaseCommandHandler<CreateAccount>
{
    public CreateAccountHandler(
        ICorrelatedRepository repository,
        IEventPublisher eventPublisher,
        ILogger<CreateAccountHandler> logger)
        : base(repository, eventPublisher, logger)
    {
    }
    
    protected override void HandleCore(CreateAccount command)
    {
        // Check if account already exists
        Account account;
        if (Repository.TryGetById(command.AccountId, out account, command))
        {
            throw new InvalidOperationException($"Account {command.AccountId} already exists");
        }
        
        // Create new account with correlation
        account = new Account(command.AccountId, command);
        
        // Save the account
        Repository.Save(account);
        
        // Publish events
        PublishEvents(account.TakeEvents());
    }
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

### Correlation Design

1. **Default to ICorrelatedRepository**: Always use `ICorrelatedRepository` instead of the base `IRepository` interface in production systems. The minimal overhead is far outweighed by the benefits of correlation tracking.

2. **Consistent Command Structure**: Ensure all commands implement `ICommand` and `ICorrelatedMessage` interfaces to maintain a consistent correlation chain.

3. **Command-to-Aggregate Flow**: Always pass the command to aggregate methods to maintain correlation between commands and the events they generate.

4. **Correlation in Process Managers**: Use correlation IDs to track long-running business processes across multiple aggregates and services.

5. **Preserve Correlation Chain**: When creating new commands in response to events, use `MessageBuilder.From(sourceEvent, () => new DerivedCommand(...))` to maintain the correlation chain.

### Operational Excellence

1. **Structured Logging**: Include correlation IDs in structured log messages for easier troubleshooting and log aggregation.

   ```csharp
   _logger.LogInformation(
       "Processing transfer {Amount} from {SourceId} to {TargetId} (Correlation: {CorrelationId})",
       command.Amount,
       command.SourceAccountId,
       command.TargetAccountId,
       command.CorrelationId);
   ```

2. **Monitoring and Alerting**: Set up monitoring dashboards based on correlation IDs to track business processes and detect anomalies.

3. **Distributed Tracing**: Integrate with distributed tracing systems like OpenTelemetry by propagating correlation IDs across service boundaries.

4. **Error Correlation**: Include correlation IDs in error reports and exception handling to link errors back to the originating commands.

5. **Performance Tracking**: Measure and track performance metrics for business operations using correlation IDs as identifiers.

### Testing and Quality Assurance

1. **Correlation Verification**: Write unit tests that verify correlation IDs are properly propagated from commands to events.

2. **Test Fixtures**: Create test fixtures that automatically set up correlation for testing command handlers and aggregates.

3. **End-to-End Testing**: Use correlation IDs to trace operations through all components in end-to-end tests.

4. **Debugging Support**: Add debugging tools that can filter logs and events by correlation ID during development and testing.

5. **Correlation Assertions**: Include assertions in tests to verify that events have the expected correlation and causation IDs.

   ```csharp
   [Fact]
   public void When_DepositingFunds_Should_MaintainCorrelation()
   {
       // Arrange
       var command = MessageBuilder.New(() => new DepositFunds(accountId, 100));
       var account = new Account(accountId, command);
       
       // Act
       account.Deposit(100, "Test deposit", command);
       var events = account.TakeEvents();
       
       // Assert
       var depositedEvent = events.OfType<FundsDeposited>().Single();
       Assert.Equal(command.CorrelationId, depositedEvent.CorrelationId);
       Assert.Equal(command.MsgId, depositedEvent.CausationId);
   }
   ```

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
