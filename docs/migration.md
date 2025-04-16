# Migration Guide

[← Back to Table of Contents](README.md)

This guide provides detailed information about migrating between different versions of Reactive Domain, including breaking changes, new features, and recommended migration strategies.

## Table of Contents

- [Breaking Changes and Deprecations](#breaking-changes-and-deprecations)
- [New Features and Enhancements](#new-features-and-enhancements)
- [Migration Strategies](#migration-strategies)
- [Backward Compatibility Considerations](#backward-compatibility-considerations)
- [Testing Strategies for Migrations](#testing-strategies-for-migrations)

## Breaking Changes and Deprecations

### Version 2.0.0

#### Removal of .NET 6 Support

As of version 2.0.0, Reactive Domain no longer supports .NET 6. Applications must be upgraded to .NET 7 or later.

**Migration Path:**
- Upgrade your application to target .NET 7 or later
- Update your project file to use the new target framework:
  ```xml
  <TargetFramework>net7.0</TargetFramework>
  ```

#### Changes to IEventSource Interface

The `IEventSource` interface has been modified to include a new `ExpectedVersion` property:

**Before:**
```csharp
public interface IEventSource
{
    Guid Id { get; }
    void RestoreFromEvents(IEnumerable<object> events);
    void UpdateWithEvents(IEnumerable<object> events);
    object[] TakeEvents();
}
```

**After:**
```csharp
public interface IEventSource
{
    Guid Id { get; }
    long ExpectedVersion { get; set; }
    void RestoreFromEvents(IEnumerable<object> events);
    void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
    object[] TakeEvents();
}
```

**Migration Path:**
- Update all implementations of `IEventSource` to include the new `ExpectedVersion` property
- Modify `UpdateWithEvents` method to accept the `expectedVersion` parameter
- If you're using the `AggregateRoot` base class, these changes are handled automatically

#### Repository API Changes

The repository API has been updated to use the new `ExpectedVersion` property:

**Before:**
```csharp
public interface IRepository
{
    TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource;
    void Save(IEventSource aggregate);
    void Delete(IEventSource aggregate);
}
```

**After:**
```csharp
public interface IRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
    void Save(IEventSource aggregate);
    void Delete(IEventSource aggregate);
}
```

**Migration Path:**
- Update all code that calls `GetById` to use `TryGetById` instead
- Handle the case where `TryGetById` returns `false`
- Example:
  ```csharp
  // Before
  var aggregate = repository.GetById<MyAggregate>(id);
  
  // After
  if (!repository.TryGetById<MyAggregate>(id, out var aggregate))
  {
      throw new AggregateNotFoundException(typeof(MyAggregate), id);
  }
  ```

#### Message Bus API Changes

The message bus API has been simplified:

**Before:**
```csharp
public interface ICommandBus
{
    void Send<TCommand>(TCommand command) where TCommand : class, ICommand;
    void RegisterHandler<TCommand>(Action<TCommand> handler) where TCommand : class, ICommand;
}
```

**After:**
```csharp
public interface ICommandBus
{
    void Send<TCommand>(TCommand command) where TCommand : class, ICommand;
}

public interface ICommandHandler<in TCommand> where TCommand : class, ICommand
{
    void Handle(TCommand command);
}
```

**Migration Path:**
- Convert all message handlers to implement the new handler interfaces
- Register handlers using the new registration mechanism
- Example:
  ```csharp
  // Before
  commandBus.RegisterHandler<CreateAccount>(cmd => 
  {
      var account = new Account(cmd.AccountId);
      repository.Save(account);
  });
  
  // After
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
  
  // Registration
  services.AddTransient<ICommandHandler<CreateAccount>, CreateAccountHandler>();
  ```

### Version 1.5.0

#### Deprecated Methods

The following methods have been deprecated in version 1.5.0 and will be removed in version 2.0.0:

- `AggregateRoot.Apply(object)` - Use `AggregateRoot.RaiseEvent(object)` instead
- `Repository.GetByIdAsync<TAggregate>(Guid)` - Use `Repository.TryGetByIdAsync<TAggregate>(Guid, out TAggregate)` instead
- `EventStore.AppendEvents(string, IEnumerable<object>)` - Use `EventStore.AppendToStream(string, long, IEnumerable<IEventData>)` instead

**Migration Path:**
- Replace all calls to deprecated methods with their replacements
- Update unit tests to use the new methods
- Run static code analysis to find all usages of deprecated methods

## New Features and Enhancements

### Version 2.0.0

#### Improved Correlation and Causation Tracking

Version 2.0.0 introduces enhanced correlation and causation tracking:

```csharp
public interface ICorrelatedMessage : IMessage
{
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}

public interface ICorrelatedRepository : IRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    void Save(IEventSource aggregate);
}
```

**Usage:**
```csharp
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly ICorrelatedRepository _repository;
    
    public CreateAccountHandler(ICorrelatedRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(CreateAccount command)
    {
        var account = new Account(command.AccountId);
        _repository.TryGetById(command.AccountId, out account, command);
        account.Initialize(command.InitialBalance);
        _repository.Save(account);
    }
}
```

#### Enhanced Snapshot Support

Version 2.0.0 introduces improved snapshot support:

```csharp
public interface ISnapshotSource
{
    void RestoreFromSnapshot(object snapshot);
    object TakeSnapshot();
}

public interface ISnapshotStore
{
    void SaveSnapshot(Guid aggregateId, Type aggregateType, object snapshot, long version);
    SnapshotEnvelope GetSnapshot(Guid aggregateId, Type aggregateType);
}
```

**Usage:**
```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    private decimal _balance;
    
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

#### Improved Testing Support

Version 2.0.0 introduces enhanced testing support:

```csharp
public class AggregateTest<TAggregate> where TAggregate : AggregateRoot, new()
{
    protected TAggregate Aggregate { get; }
    
    public AggregateTest()
    {
        Aggregate = new TAggregate();
    }
    
    protected void Given(params object[] events)
    {
        Aggregate.RestoreFromEvents(events);
    }
    
    protected object[] When(Action<TAggregate> action)
    {
        action(Aggregate);
        return Aggregate.TakeEvents();
    }
}
```

**Usage:**
```csharp
public class AccountTests : AggregateTest<Account>
{
    [Fact]
    public void CanDepositMoney()
    {
        // Given
        Given(new AccountCreated(Guid.NewGuid(), "John Doe"));
        
        // When
        var events = When(a => a.Deposit(100));
        
        // Then
        var @event = Assert.Single(events);
        var depositEvent = Assert.IsType<AmountDeposited>(@event);
        Assert.Equal(100, depositEvent.Amount);
    }
}
```

### Version 1.5.0

#### Asynchronous Command Handling

Version 1.5.0 introduces asynchronous command handling:

```csharp
public interface IAsyncCommandHandler<in TCommand> where TCommand : class, ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
```

**Usage:**
```csharp
public class CreateAccountHandler : IAsyncCommandHandler<CreateAccount>
{
    private readonly IRepository _repository;
    
    public CreateAccountHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public async Task HandleAsync(CreateAccount command, CancellationToken cancellationToken = default)
    {
        var account = new Account(command.AccountId);
        await Task.Delay(100, cancellationToken); // Simulate async work
        _repository.Save(account);
    }
}
```

#### Improved Event Store Connection Management

Version 1.5.0 introduces improved event store connection management:

```csharp
public interface IStreamStoreConnectionFactory
{
    IStreamStoreConnection Create(string connectionString);
}
```

**Usage:**
```csharp
public class StreamStoreRepository : IRepository
{
    private readonly IStreamStoreConnection _connection;
    
    public StreamStoreRepository(IStreamStoreConnectionFactory connectionFactory, string connectionString)
    {
        _connection = connectionFactory.Create(connectionString);
        _connection.Connect();
    }
    
    // ... repository implementation ...
}
```

## Migration Strategies

### Incremental Migration

For large applications, an incremental migration approach is recommended:

1. **Update Dependencies**: Update to the latest version of Reactive Domain
2. **Address Compiler Errors**: Fix any compiler errors related to breaking changes
3. **Update Core Components**: Migrate core components first (repositories, event handlers)
4. **Update Aggregates**: Migrate aggregates to implement new interfaces
5. **Update Command Handlers**: Migrate command handlers to the new pattern
6. **Update Event Handlers**: Migrate event handlers to the new pattern
7. **Update Tests**: Update tests to use the new APIs
8. **Verify Functionality**: Verify that all functionality works as expected

### Big Bang Migration

For smaller applications, a big bang migration approach may be feasible:

1. **Create a New Branch**: Create a new branch for the migration
2. **Update Dependencies**: Update to the latest version of Reactive Domain
3. **Update All Code**: Update all code to use the new APIs
4. **Run Tests**: Run all tests to verify functionality
5. **Deploy**: Deploy the updated application

### Parallel Deployment

For critical applications, a parallel deployment approach may be appropriate:

1. **Create a New Application**: Create a new application using the latest version
2. **Implement Core Functionality**: Implement core functionality in the new application
3. **Migrate Data**: Migrate data from the old application to the new one
4. **Validate**: Validate that the new application works as expected
5. **Switch Over**: Switch traffic from the old application to the new one

## Backward Compatibility Considerations

### Event Compatibility

When migrating to a new version, it's important to maintain compatibility with existing events:

1. **Never Delete Events**: Once events are in production, never delete them
2. **Add Fields, Don't Remove**: When modifying events, add new fields rather than removing existing ones
3. **Provide Defaults**: When adding new fields, provide sensible defaults
4. **Use Event Upcasting**: Transform old events to new versions during deserialization

Example of event upcasting:

```csharp
public class EventUpcastingSerializer : IEventSerializer
{
    private readonly IEventSerializer _innerSerializer;
    
    public EventUpcastingSerializer(IEventSerializer innerSerializer)
    {
        _innerSerializer = innerSerializer;
    }
    
    public object Deserialize(RecordedEvent recordedEvent)
    {
        var deserialized = _innerSerializer.Deserialize(recordedEvent);
        
        // Upcast old event versions to new versions
        if (deserialized is AccountCreatedV1 v1)
        {
            return new AccountCreatedV2(v1.AccountId, v1.Owner, null);
        }
        
        return deserialized;
    }
    
    public IEventData Serialize(object @event, Guid eventId)
    {
        return _innerSerializer.Serialize(@event, eventId);
    }
}
```

### Command Compatibility

When migrating to a new version, it's important to maintain compatibility with existing commands:

1. **Versioned Commands**: Use versioned commands to support both old and new clients
2. **Command Translation**: Translate old commands to new commands
3. **Command Validation**: Validate commands before processing

Example of command translation:

```csharp
public class CommandTranslator : ICommandHandler<CreateAccountV1>
{
    private readonly ICommandBus _commandBus;
    
    public CommandTranslator(ICommandBus commandBus)
    {
        _commandBus = commandBus;
    }
    
    public void Handle(CreateAccountV1 command)
    {
        // Translate old command to new command
        var newCommand = new CreateAccountV2(command.AccountId, command.Owner, null);
        
        // Send new command
        _commandBus.Send(newCommand);
    }
}
```

### Read Model Compatibility

When migrating to a new version, it's important to maintain compatibility with existing read models:

1. **Versioned APIs**: Use versioned APIs to support both old and new clients
2. **API Translation**: Translate old API calls to new API calls
3. **Data Migration**: Migrate data from old read models to new read models

Example of API translation:

```csharp
[ApiController]
[Route("api/v1/accounts")]
public class AccountsControllerV1 : ControllerBase
{
    private readonly AccountsControllerV2 _v2Controller;
    
    public AccountsControllerV1(AccountsControllerV2 v2Controller)
    {
        _v2Controller = v2Controller;
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        // Forward to v2 controller
        var result = await _v2Controller.GetAccountV2(id);
        
        // Transform result to v1 format
        if (result is OkObjectResult okResult)
        {
            var accountV2 = (AccountDto)okResult.Value;
            var accountV1 = new AccountDtoV1
            {
                Id = accountV2.Id,
                Owner = accountV2.Owner,
                Balance = accountV2.Balance
            };
            
            return Ok(accountV1);
        }
        
        return result;
    }
}
```

## Testing Strategies for Migrations

### Unit Testing

When migrating to a new version, it's important to have comprehensive unit tests:

1. **Test Old Behavior**: Verify that existing behavior still works
2. **Test New Behavior**: Verify that new behavior works as expected
3. **Test Migration Paths**: Verify that migration paths work as expected

Example of testing migration paths:

```csharp
[Fact]
public void CanUpcastOldEvents()
{
    // Arrange
    var oldEvent = new AccountCreatedV1(Guid.NewGuid(), "John Doe");
    var serializer = new JsonMessageSerializer();
    var eventData = serializer.Serialize(oldEvent, Guid.NewGuid());
    var recordedEvent = new RecordedEvent("account-1", 0, eventData.EventId, eventData.Type, eventData.Data, eventData.Metadata, true, DateTime.UtcNow);
    
    var upcastingSerializer = new EventUpcastingSerializer(serializer);
    
    // Act
    var upcastedEvent = upcastingSerializer.Deserialize(recordedEvent);
    
    // Assert
    var v2Event = Assert.IsType<AccountCreatedV2>(upcastedEvent);
    Assert.Equal(oldEvent.AccountId, v2Event.AccountId);
    Assert.Equal(oldEvent.Owner, v2Event.Owner);
    Assert.Null(v2Event.Email);
}
```

### Integration Testing

When migrating to a new version, it's important to have comprehensive integration tests:

1. **Test End-to-End Flows**: Verify that end-to-end flows still work
2. **Test Integration Points**: Verify that integration points still work
3. **Test Performance**: Verify that performance is acceptable

Example of testing end-to-end flows:

```csharp
[Fact]
public async Task CanCreateAndRetrieveAccount()
{
    // Arrange
    var accountId = Guid.NewGuid();
    var commandBus = _fixture.GetService<ICommandBus>();
    var queryBus = _fixture.GetService<IQueryBus>();
    
    // Act - Create account
    commandBus.Send(new CreateAccount(accountId, "John Doe", 100));
    
    // Wait for read model to be updated
    await Task.Delay(100);
    
    // Act - Retrieve account
    var query = new GetAccountQuery(accountId);
    var account = await queryBus.Query<GetAccountQuery, AccountDto>(query);
    
    // Assert
    Assert.NotNull(account);
    Assert.Equal(accountId, account.Id);
    Assert.Equal("John Doe", account.Owner);
    Assert.Equal(100, account.Balance);
}
```

### Performance Testing

When migrating to a new version, it's important to verify performance:

1. **Benchmark Old Version**: Establish performance baselines
2. **Benchmark New Version**: Measure performance of the new version
3. **Identify Bottlenecks**: Identify and address performance bottlenecks
4. **Optimize**: Optimize critical paths

Example of performance testing:

```csharp
[Fact]
public void CanHandleHighVolumeCommands()
{
    // Arrange
    var commandBus = _fixture.GetService<ICommandBus>();
    var stopwatch = new Stopwatch();
    const int commandCount = 1000;
    
    // Act
    stopwatch.Start();
    
    for (int i = 0; i < commandCount; i++)
    {
        commandBus.Send(new CreateAccount(Guid.NewGuid(), $"User {i}", 100));
    }
    
    stopwatch.Stop();
    
    // Assert
    Console.WriteLine($"Processed {commandCount} commands in {stopwatch.ElapsedMilliseconds}ms");
    Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Command processing should be faster than 5 seconds");
}
```

[↑ Back to Top](#migration-guide) | [← Back to Table of Contents](README.md)
