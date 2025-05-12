# Integration Guide

[← Back to Table of Contents](README.md)

This guide provides strategies and best practices for integrating Reactive Domain with other systems and frameworks.

## Table of Contents

- [Integration with ASP.NET Core](#integration-with-aspnet-core)
- [Integration with Other .NET Frameworks](#integration-with-other-net-frameworks)
- [Integration with Non-.NET Systems](#integration-with-non-net-systems)
- [API Design for Event-Sourced Systems](#api-design-for-event-sourced-systems)
- [Message Contracts and Versioning](#message-contracts-and-versioning)
- [Integration Testing Strategies](#integration-testing-strategies)

## Integration with ASP.NET Core

### Dependency Injection

Register Reactive Domain services with ASP.NET Core's dependency injection container:

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure EventStoreDB connection
        services.AddSingleton<IStreamStoreConnection>(provider =>
        {
            var connectionString = Configuration.GetConnectionString("EventStore");
            var connection = new EventStoreConnection(connectionString);
            connection.Connect();
            return connection;
        });
        
        // Register repositories
        services.AddSingleton<IStreamNameBuilder, PrefixedCamelCaseStreamNameBuilder>();
        services.AddSingleton<IEventSerializer, JsonMessageSerializer>();
        services.AddSingleton<IRepository, StreamStoreRepository>();
        
        // Register message buses
        services.AddSingleton<ICommandBus, InMemoryCommandBus>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IQueryBus, InMemoryQueryBus>();
        
        // Register command handlers
        services.AddTransient<ICommandHandler<CreateAccount>, CreateAccountHandler>();
        services.AddTransient<ICommandHandler<DepositFunds>, DepositFundsHandler>();
        services.AddTransient<ICommandHandler<WithdrawFunds>, WithdrawFundsHandler>();
        
        // Register event handlers
        services.AddTransient<IEventHandler<AccountCreated>, AccountCreatedHandler>();
        services.AddTransient<IEventHandler<FundsDeposited>, FundsDepositedHandler>();
        services.AddTransient<IEventHandler<FundsWithdrawn>, FundsWithdrawnHandler>();
        
        // Register query handlers
        services.AddTransient<IQueryHandler<GetAccountBalance, decimal>, GetAccountBalanceHandler>();
        services.AddTransient<IQueryHandler<GetAccountDetails, AccountDetails>, GetAccountDetailsHandler>();
        
        // Register controllers
        services.AddControllers();
    }
}
```

### API Controllers

Create API controllers that use Reactive Domain services:

```csharp
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    
    public AccountsController(ICommandBus commandBus, IQueryBus queryBus)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
    }
    
    [HttpPost]
    public IActionResult CreateAccount([FromBody] CreateAccountRequest request)
    {
        var accountId = Guid.NewGuid();
        
        _commandBus.Send(new CreateAccount(accountId, request.Owner, request.InitialBalance));
        
        return CreatedAtAction(nameof(GetAccount), new { id = accountId }, new { Id = accountId });
    }
    
    [HttpGet("{id}")]
    public IActionResult GetAccount(Guid id)
    {
        var details = _queryBus.Query<GetAccountDetails, AccountDetails>(new GetAccountDetails(id));
        
        if (details == null)
            return NotFound();
            
        return Ok(details);
    }
    
    [HttpPost("{id}/deposit")]
    public IActionResult Deposit(Guid id, [FromBody] DepositRequest request)
    {
        _commandBus.Send(new DepositFunds(id, request.Amount));
        return NoContent();
    }
    
    [HttpPost("{id}/withdraw")]
    public IActionResult Withdraw(Guid id, [FromBody] WithdrawRequest request)
    {
        try
        {
            _commandBus.Send(new WithdrawFunds(id, request.Amount));
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
```

### Background Services

Implement background services for event processing:

```csharp
public class EventProcessorService : BackgroundService
{
    private readonly IStreamStoreConnection _connection;
    private readonly IEventBus _eventBus;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<EventProcessorService> _logger;
    
    public EventProcessorService(
        IStreamStoreConnection connection,
        IEventBus eventBus,
        IEventSerializer serializer,
        ILogger<EventProcessorService> logger)
    {
        _connection = connection;
        _eventBus = eventBus;
        _serializer = serializer;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to all events
        var subscription = _connection.SubscribeToAll(
            (subscription, @event) =>
            {
                try
                {
                    // Deserialize the event
                    var deserializedEvent = _serializer.Deserialize(@event);
                    
                    // Publish to event bus
                    _eventBus.Publish(deserializedEvent);
                    
                    // Acknowledge the event
                    subscription.Acknowledge(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event");
                    subscription.Fail(@event, SubscriptionNakEventAction.Retry, ex.Message);
                }
            },
            subscriptionDropped: (subscription, reason, exception) =>
            {
                _logger.LogWarning(exception, "Subscription dropped: {Reason}", reason);
                
                // Reconnect after a delay
                Task.Delay(1000, stoppingToken)
                    .ContinueWith(_ => subscription.Reconnect(), stoppingToken);
            });
            
        // Wait for cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

## Integration with Other .NET Frameworks

### .NET Framework Integration

Integrate with .NET Framework applications:

```csharp
public class ReactiveModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Configure EventStoreDB connection
        builder.Register(c =>
        {
            var connectionString = ConfigurationManager.ConnectionStrings["EventStore"].ConnectionString;
            var connection = new EventStoreConnection(connectionString);
            connection.Connect();
            return connection;
        }).As<IStreamStoreConnection>().SingleInstance();
        
        // Register repositories
        builder.RegisterType<PrefixedCamelCaseStreamNameBuilder>().As<IStreamNameBuilder>().SingleInstance();
        builder.RegisterType<JsonMessageSerializer>().As<IEventSerializer>().SingleInstance();
        builder.RegisterType<StreamStoreRepository>().As<IRepository>().SingleInstance();
        
        // Register message buses
        builder.RegisterType<InMemoryCommandBus>().As<ICommandBus>().SingleInstance();
        builder.RegisterType<InMemoryEventBus>().As<IEventBus>().SingleInstance();
        builder.RegisterType<InMemoryQueryBus>().As<IQueryBus>().SingleInstance();
        
        // Register command handlers
        builder.RegisterType<CreateAccountHandler>().As<ICommandHandler<CreateAccount>>().InstancePerDependency();
        builder.RegisterType<DepositFundsHandler>().As<ICommandHandler<DepositFunds>>().InstancePerDependency();
        builder.RegisterType<WithdrawFundsHandler>().As<ICommandHandler<WithdrawFunds>>().InstancePerDependency();
        
        // Register event handlers
        builder.RegisterType<AccountCreatedHandler>().As<IEventHandler<AccountCreated>>().InstancePerDependency();
        builder.RegisterType<FundsDepositedHandler>().As<IEventHandler<FundsDeposited>>().InstancePerDependency();
        builder.RegisterType<FundsWithdrawnHandler>().As<IEventHandler<FundsWithdrawn>>().InstancePerDependency();
        
        // Register query handlers
        builder.RegisterType<GetAccountBalanceHandler>().As<IQueryHandler<GetAccountBalance, decimal>>().InstancePerDependency();
        builder.RegisterType<GetAccountDetailsHandler>().As<IQueryHandler<GetAccountDetails, AccountDetails>>().InstancePerDependency();
    }
}
```

### Xamarin/MAUI Integration

Integrate with mobile applications:

```csharp
public class ReactiveDomainService : IReactiveDomainService
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    
    public ReactiveDomainService(ICommandBus commandBus, IQueryBus queryBus)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
    }
    
    public async Task<Guid> CreateAccount(string owner, decimal initialBalance)
    {
        var accountId = Guid.NewGuid();
        
        await Task.Run(() => _commandBus.Send(new CreateAccount(accountId, owner, initialBalance)));
        
        return accountId;
    }
    
    public async Task<AccountDetails> GetAccount(Guid id)
    {
        return await Task.Run(() => _queryBus.Query<GetAccountDetails, AccountDetails>(new GetAccountDetails(id)));
    }
    
    public async Task Deposit(Guid id, decimal amount)
    {
        await Task.Run(() => _commandBus.Send(new DepositFunds(id, amount)));
    }
    
    public async Task Withdraw(Guid id, decimal amount)
    {
        await Task.Run(() => _commandBus.Send(new WithdrawFunds(id, amount)));
    }
}
```

## Integration with Non-.NET Systems

### REST API

Expose a REST API for non-.NET clients:

```csharp
[ApiController]
[Route("api/v1/accounts")]
public class AccountsApiController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    
    public AccountsApiController(ICommandBus commandBus, IQueryBus queryBus)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
    }
    
    [HttpPost]
    [ProducesResponseType(typeof(AccountCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CreateAccount([FromBody] CreateAccountRequest request)
    {
        var accountId = Guid.NewGuid();
        
        _commandBus.Send(new CreateAccount(accountId, request.Owner, request.InitialBalance));
        
        var response = new AccountCreatedResponse
        {
            Id = accountId,
            Links = new Dictionary<string, string>
            {
                ["self"] = Url.ActionLink(nameof(GetAccount), values: new { id = accountId }),
                ["deposit"] = Url.ActionLink(nameof(Deposit), values: new { id = accountId }),
                ["withdraw"] = Url.ActionLink(nameof(Withdraw), values: new { id = accountId })
            }
        };
        
        return CreatedAtAction(nameof(GetAccount), new { id = accountId }, response);
    }
    
    // ... other actions ...
}
```

### Message Queue Integration

Integrate with message queues for asynchronous communication:

```csharp
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName;
    
    public RabbitMqEventPublisher(string connectionString, string exchangeName)
    {
        _exchangeName = exchangeName;
        
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        
        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
    }
    
    public void Publish(object @event)
    {
        var eventType = @event.GetType().Name;
        var routingKey = $"event.{eventType.ToLowerInvariant()}";
        var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
        
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = eventType;
        
        _channel.BasicPublish(_exchangeName, routingKey, properties, body);
    }
    
    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
```

### gRPC Integration

Implement gRPC services for efficient communication:

```csharp
public class AccountGrpcService : AccountService.AccountServiceBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    
    public AccountGrpcService(ICommandBus commandBus, IQueryBus queryBus)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
    }
    
    public override Task<CreateAccountResponse> CreateAccount(CreateAccountRequest request, ServerCallContext context)
    {
        var accountId = Guid.NewGuid();
        
        _commandBus.Send(new CreateAccount(
            accountId,
            request.Owner,
            (decimal)request.InitialBalance));
            
        return Task.FromResult(new CreateAccountResponse
        {
            AccountId = accountId.ToString()
        });
    }
    
    public override Task<GetAccountResponse> GetAccount(GetAccountRequest request, ServerCallContext context)
    {
        var accountId = Guid.Parse(request.AccountId);
        
        var details = _queryBus.Query<GetAccountDetails, AccountDetails>(
            new GetAccountDetails(accountId));
            
        if (details == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Account not found"));
        }
        
        return Task.FromResult(new GetAccountResponse
        {
            AccountId = details.Id.ToString(),
            Owner = details.Owner,
            Balance = (double)details.Balance,
            CreatedAt = Timestamp.FromDateTime(details.CreatedAt.ToUniversalTime())
        });
    }
    
    // ... other methods ...
}
```

## API Design for Event-Sourced Systems

### Command API Design

Design command APIs for event-sourced systems:

1. **Use Command DTOs**: Create dedicated DTOs for command requests
2. **Return Minimal Data**: Return only essential data from command endpoints
3. **Use HTTP Status Codes**: Use appropriate status codes for command results
4. **Include Resource Links**: Include links to related resources in responses

```csharp
// Command DTO
public class CreateAccountRequest
{
    [Required]
    public string Owner { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal InitialBalance { get; set; }
}

// Command Response
public class AccountCreatedResponse
{
    public Guid Id { get; set; }
    public Dictionary<string, string> Links { get; set; }
}
```

### Query API Design

Design query APIs for event-sourced systems:

1. **Use Query Parameters**: Use query parameters for filtering and pagination
2. **Support Projections**: Allow clients to request specific fields
3. **Support Sorting**: Allow clients to specify sort order
4. **Implement Pagination**: Support pagination for large result sets

```csharp
[HttpGet]
public IActionResult GetAccounts(
    [FromQuery] string ownerFilter = null,
    [FromQuery] decimal? minBalance = null,
    [FromQuery] decimal? maxBalance = null,
    [FromQuery] string sortBy = "createdAt",
    [FromQuery] string sortOrder = "desc",
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
{
    var query = new GetAccountsQuery
    {
        OwnerFilter = ownerFilter,
        MinBalance = minBalance,
        MaxBalance = maxBalance,
        SortBy = sortBy,
        SortOrder = sortOrder,
        Page = page,
        PageSize = pageSize
    };
    
    var result = _queryBus.Query<GetAccountsQuery, PagedResult<AccountSummary>>(query);
    
    return Ok(new
    {
        Items = result.Items,
        TotalItems = result.TotalItems,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalPages = result.TotalPages,
        Links = new
        {
            Self = Url.Action(nameof(GetAccounts), new { ownerFilter, minBalance, maxBalance, sortBy, sortOrder, page, pageSize }),
            First = Url.Action(nameof(GetAccounts), new { ownerFilter, minBalance, maxBalance, sortBy, sortOrder, page = 1, pageSize }),
            Previous = result.Page > 1 ? Url.Action(nameof(GetAccounts), new { ownerFilter, minBalance, maxBalance, sortBy, sortOrder, page = result.Page - 1, pageSize }) : null,
            Next = result.Page < result.TotalPages ? Url.Action(nameof(GetAccounts), new { ownerFilter, minBalance, maxBalance, sortBy, sortOrder, page = result.Page + 1, pageSize }) : null,
            Last = Url.Action(nameof(GetAccounts), new { ownerFilter, minBalance, maxBalance, sortBy, sortOrder, page = result.TotalPages, pageSize })
        }
    });
}
```

### Event Subscription API

Design APIs for event subscriptions:

1. **Server-Sent Events**: Use SSE for real-time event notifications
2. **WebSockets**: Use WebSockets for bidirectional communication
3. **Webhooks**: Use webhooks for push notifications

```csharp
[HttpGet("events")]
public async Task GetEvents()
{
    var response = Response;
    response.Headers.Add("Content-Type", "text/event-stream");
    response.Headers.Add("Cache-Control", "no-cache");
    response.Headers.Add("Connection", "keep-alive");
    
    var subscription = _eventBus.Subscribe<object>(
        @event => WriteEvent(response, @event));
        
    try
    {
        await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted);
    }
    catch (TaskCanceledException)
    {
        // Client disconnected
    }
    finally
    {
        subscription.Dispose();
    }
}

private async Task WriteEvent(HttpResponse response, object @event)
{
    var eventType = @event.GetType().Name;
    var eventData = JsonConvert.SerializeObject(@event);
    
    await response.WriteAsync($"event: {eventType}\n");
    await response.WriteAsync($"data: {eventData}\n\n");
    await response.Body.FlushAsync();
}
```

## Message Contracts and Versioning

### Message Contract Design

Design message contracts for integration:

1. **Use Immutable Messages**: Make message classes immutable
2. **Include Metadata**: Include metadata such as correlation IDs
3. **Use Explicit Types**: Use explicit types rather than dynamic objects
4. **Document Contracts**: Document message contracts for consumers

```csharp
public class AccountCreated : IEvent
{
    public Guid Id { get; }
    public Guid AccountId { get; }
    public string Owner { get; }
    public decimal InitialBalance { get; }
    public DateTime CreatedAt { get; }
    
    public AccountCreated(Guid id, Guid accountId, string owner, decimal initialBalance, DateTime createdAt)
    {
        Id = id;
        AccountId = accountId;
        Owner = owner;
        InitialBalance = initialBalance;
        CreatedAt = createdAt;
    }
}
```

### Message Versioning

Implement message versioning:

1. **Version in Namespace**: Include version in namespace or package
2. **Version in Type Name**: Include version in type name
3. **Version in Message**: Include version in message properties
4. **Use Compatibility Attributes**: Use attributes to indicate compatibility

```csharp
namespace MyApp.Messages.V1
{
    public class AccountCreated : IEvent
    {
        public Guid Id { get; }
        public Guid AccountId { get; }
        public string Owner { get; }
        public decimal InitialBalance { get; }
        public DateTime CreatedAt { get; }
        
        public AccountCreated(Guid id, Guid accountId, string owner, decimal initialBalance, DateTime createdAt)
        {
            Id = id;
            AccountId = accountId;
            Owner = owner;
            InitialBalance = initialBalance;
            CreatedAt = createdAt;
        }
    }
}

namespace MyApp.Messages.V2
{
    [CompatibleWith(typeof(V1.AccountCreated))]
    public class AccountCreated : IEvent
    {
        public Guid Id { get; }
        public Guid AccountId { get; }
        public string Owner { get; }
        public decimal InitialBalance { get; }
        public DateTime CreatedAt { get; }
        public string Email { get; } // New field in V2
        
        public AccountCreated(Guid id, Guid accountId, string owner, decimal initialBalance, DateTime createdAt, string email)
        {
            Id = id;
            AccountId = accountId;
            Owner = owner;
            InitialBalance = initialBalance;
            CreatedAt = createdAt;
            Email = email;
        }
        
        // Convert from V1 to V2
        public static AccountCreated FromV1(V1.AccountCreated v1)
        {
            return new AccountCreated(
                v1.Id,
                v1.AccountId,
                v1.Owner,
                v1.InitialBalance,
                v1.CreatedAt,
                null); // No email in V1
        }
    }
}
```

### Message Transformation

Implement message transformation for version compatibility:

```csharp
public class MessageVersionTransformer : IMessageTransformer
{
    private readonly Dictionary<Type, Func<object, object>> _transformers = new Dictionary<Type, Func<object, object>>();
    
    public void RegisterTransformer<TSource, TTarget>(Func<TSource, TTarget> transformer)
        where TSource : class
        where TTarget : class
    {
        _transformers[typeof(TSource)] = source => transformer((TSource)source);
    }
    
    public object Transform(object message)
    {
        var messageType = message.GetType();
        
        if (_transformers.TryGetValue(messageType, out var transformer))
        {
            return transformer(message);
        }
        
        return message;
    }
}
```

## Integration Testing Strategies

### API Integration Tests

Test API integrations:

```csharp
[Fact]
public async Task CanCreateAndRetrieveAccount()
{
    // Arrange
    var client = _factory.CreateClient();
    
    var createRequest = new
    {
        Owner = "John Doe",
        InitialBalance = 100.0
    };
    
    // Act - Create account
    var createResponse = await client.PostAsJsonAsync("/api/accounts", createRequest);
    
    // Assert - Create response
    createResponse.EnsureSuccessStatusCode();
    var createResult = await createResponse.Content.ReadFromJsonAsync<AccountCreatedResponse>();
    Assert.NotEqual(Guid.Empty, createResult.Id);
    
    // Act - Get account
    var getResponse = await client.GetAsync($"/api/accounts/{createResult.Id}");
    
    // Assert - Get response
    getResponse.EnsureSuccessStatusCode();
    var account = await getResponse.Content.ReadFromJsonAsync<AccountDetails>();
    Assert.Equal(createResult.Id, account.Id);
    Assert.Equal("John Doe", account.Owner);
    Assert.Equal(100.0m, account.Balance);
}
```

### Message Queue Integration Tests

Test message queue integrations:

```csharp
[Fact]
public async Task PublishedEventsAreReceivedByConsumer()
{
    // Arrange
    var publisher = new RabbitMqEventPublisher(_connectionString, "test-exchange");
    var consumer = new RabbitMqEventConsumer(_connectionString, "test-exchange", "test-queue");
    
    var receivedEvents = new List<object>();
    var completionSource = new TaskCompletionSource<bool>();
    
    consumer.Subscribe<AccountCreated>(@event =>
    {
        receivedEvents.Add(@event);
        
        if (receivedEvents.Count >= 1)
        {
            completionSource.SetResult(true);
        }
    });
    
    // Act
    var @event = new AccountCreated(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "John Doe",
        100,
        DateTime.UtcNow);
        
    publisher.Publish(@event);
    
    // Wait for event to be received
    await Task.WhenAny(completionSource.Task, Task.Delay(5000));
    
    // Assert
    Assert.True(completionSource.Task.IsCompleted, "Event was not received within timeout");
    Assert.Single(receivedEvents);
    
    var receivedEvent = receivedEvents[0] as AccountCreated;
    Assert.NotNull(receivedEvent);
    Assert.Equal(@event.AccountId, receivedEvent.AccountId);
    Assert.Equal(@event.Owner, receivedEvent.Owner);
    Assert.Equal(@event.InitialBalance, receivedEvent.InitialBalance);
}
```

### Contract Testing

Implement contract testing for message contracts:

```csharp
[Fact]
public void MessageContractIsCompatible()
{
    // Arrange
    var v1Event = new V1.AccountCreated(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "John Doe",
        100,
        DateTime.UtcNow);
        
    // Act
    var serialized = JsonConvert.SerializeObject(v1Event);
    var deserialized = JsonConvert.DeserializeObject<V2.AccountCreated>(serialized);
    
    // Assert
    Assert.Equal(v1Event.Id, deserialized.Id);
    Assert.Equal(v1Event.AccountId, deserialized.AccountId);
    Assert.Equal(v1Event.Owner, deserialized.Owner);
    Assert.Equal(v1Event.InitialBalance, deserialized.InitialBalance);
    Assert.Equal(v1Event.CreatedAt, deserialized.CreatedAt);
    Assert.Null(deserialized.Email); // New field in V2
}
```

[↑ Back to Top](#integration-guide) | [← Back to Table of Contents](README.md)
