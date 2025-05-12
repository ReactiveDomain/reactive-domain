# Infrastructure Setup for Reactive Domain Applications

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

This document provides comprehensive guidance on setting up the infrastructure for Reactive Domain applications, including configuring event stores, message buses, repositories, and read models.

## Table of Contents

1. [Event Store Configuration](#event-store-configuration)
2. [Message Bus Setup](#message-bus-setup)
3. [Repository Configuration](#repository-configuration)
4. [Read Model Implementation](#read-model-implementation)
5. [Dependency Injection](#dependency-injection)
6. [Logging and Monitoring](#logging-and-monitoring)
7. [Scaling Considerations](#scaling-considerations)
8. [Production Deployment](#production-deployment)
9. [Best Practices](#best-practices)

## Event Store Configuration

The event store is the heart of an event-sourced system, responsible for persisting and retrieving events. Reactive Domain supports multiple event store implementations, with EventStoreDB being the primary choice for production systems.

### 1. EventStoreDB Configuration

EventStoreDB is a purpose-built database for event sourcing that provides high performance, reliability, and specialized features for event-sourced systems.

#### Basic Connection Setup

```csharp
public static class EventStoreFactory
{
    public static IStreamStoreConnection CreateConnection(string connectionString)
    {
        var settings = ConnectionSettings.Create()
            .EnableVerboseLogging()
            .UseConsoleLogger()
            .KeepReconnecting()
            .KeepRetrying()
            .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
            .Build();
            
        var connection = EventStoreConnection.Create(
            settings,
            new Uri(connectionString));
            
        connection.ConnectAsync().Wait();
        return new StreamStoreConnection(connection);
    }
}

// Usage in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register EventStore connection
    services.AddSingleton<IStreamStoreConnection>(provider =>
    {
        var connectionString = Configuration.GetConnectionString("EventStore");
        return EventStoreFactory.CreateConnection(connectionString);
    });
    
    // Register other dependencies
    services.AddSingleton<IRepository<Account, Guid>, StreamStoreRepository<Account, Guid>>();
    // ...
}
```

#### Connection Settings for Production

For production environments, you should configure more robust connection settings:

```csharp
public static IStreamStoreConnection CreateProductionConnection(string connectionString)
{
    var settings = ConnectionSettings.Create()
        .SetDefaultUserCredentials(new UserCredentials(
            Environment.GetEnvironmentVariable("EVENTSTORE_USERNAME"),
            Environment.GetEnvironmentVariable("EVENTSTORE_PASSWORD")))
        .SetHeartbeatInterval(TimeSpan.FromSeconds(30))
        .SetHeartbeatTimeout(TimeSpan.FromSeconds(120))
        .SetOperationTimeout(TimeSpan.FromSeconds(60))
        .SetTimerPeriod(TimeSpan.FromMilliseconds(500))
        .SetReconnectionDelayTo(TimeSpan.FromSeconds(1))
        .SetMaxReconnections(10)
        .SetMaxOperationRetries(10)
        .SetMaxDiscoverAttempts(10)
        .SetGossipTimeout(TimeSpan.FromSeconds(5))
        .UseCustomLogger(new SerilogEventStoreLogger())
        .EnableConnectionTimeoutCheck()
        .Build();
    
    var clusterSettings = ClusterSettings.Create()
        .DiscoverClusterViaGossipSeeds()
        .SetGossipSeedEndPoints(ParseGossipSeeds(connectionString))
        .SetGossipTimeout(TimeSpan.FromSeconds(5))
        .Build();
    
    var connection = EventStoreConnection.Create(settings, clusterSettings);
    connection.ConnectAsync().Wait();
    return new StreamStoreConnection(connection);
}

private static IPEndPoint[] ParseGossipSeeds(string connectionString)
{
    // Parse connection string to extract gossip seed endpoints
    // Format: "gossip://node1:2113,node2:2113,node3:2113"
    var uri = new Uri(connectionString);
    var hostsAndPorts = uri.Host.Split(',');
    
    return hostsAndPorts.Select(hostAndPort =>
    {
        var parts = hostAndPort.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);
        return new IPEndPoint(Dns.GetHostAddresses(host)[0], port);
    }).ToArray();
}
```

#### Connection Health Monitoring

Implement health checks to monitor the EventStore connection:

```csharp
public class EventStoreHealthCheck : IHealthCheck
{
    private readonly IStreamStoreConnection _connection;
    
    public EventStoreHealthCheck(IStreamStoreConnection connection)
    {
        _connection = connection;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ping the event store by reading a small stream
            var streamName = "$stats-0-0";
            var slice = await _connection.ReadStreamEventsForwardAsync(
                streamName, 0, 1, false);
                
            return HealthCheckResult.Healthy("EventStore connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("EventStore connection failed", ex);
        }
    }
}

// Register in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register health checks
    services.AddHealthChecks()
        .AddCheck<EventStoreHealthCheck>("eventstore_connection");
}
```

### 2. In-Memory Event Store for Testing

For testing purposes, you can use an in-memory event store implementation:

```csharp
public class InMemoryEventStore : IStreamStoreConnection
{
    private readonly Dictionary<string, List<ResolvedEvent>> _streams = 
        new Dictionary<string, List<ResolvedEvent>>();
    private readonly object _lock = new object();
    
    public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(
        string stream, long start, int count, bool resolveLinks)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(stream, out var events))
            {
                return Task.FromResult(new StreamEventsSlice(
                    SliceReadStatus.StreamNotFound,
                    stream,
                    start,
                    ReadDirection.Forward,
                    new List<ResolvedEvent>(),
                    start,
                    true,
                    0));
            }
            
            var slice = events
                .Skip((int)start)
                .Take(count)
                .ToList();
                
            return Task.FromResult(new StreamEventsSlice(
                SliceReadStatus.Success,
                stream,
                start,
                ReadDirection.Forward,
                slice,
                start + slice.Count,
                slice.Count < count || (start + slice.Count) >= events.Count,
                events.Count));
        }
    }
    
    public Task<WriteResult> AppendToStreamAsync(
        string stream, long expectedVersion, IEnumerable<EventData> events)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(stream, out var streamEvents))
            {
                if (expectedVersion > -1)
                {
                    throw new WrongExpectedVersionException(
                        $"Stream {stream} does not exist but expected version {expectedVersion}");
                }
                
                streamEvents = new List<ResolvedEvent>();
                _streams.Add(stream, streamEvents);
            }
            else if (expectedVersion != -1 && streamEvents.Count != expectedVersion)
            {
                throw new WrongExpectedVersionException(
                    $"Expected version {expectedVersion} but got {streamEvents.Count}");
            }
            
            var position = streamEvents.Count;
            var resolvedEvents = events.Select((e, i) => new ResolvedEvent(
                new EventRecord(
                    position + i,
                    DateTime.UtcNow,
                    Guid.Parse(e.EventId),
                    e.Type,
                    true,
                    e.Data,
                    e.Metadata),
                null,
                null)).ToList();
                
            streamEvents.AddRange(resolvedEvents);
            
            return Task.FromResult(new WriteResult(
                position + resolvedEvents.Count - 1,
                position));
        }
    }
    
    // Implement other interface methods...
}

// Register for testing
public void ConfigureServices(IServiceCollection services)
{
    // Use in-memory event store for testing
    services.AddSingleton<IStreamStoreConnection, InMemoryEventStore>();
    services.AddSingleton<IRepository<Account, Guid>, StreamStoreRepository<Account, Guid>>();
}
```

### 3. Event Store Subscription Configuration

Configure event subscriptions to process events for read models and other event handlers:

```csharp
public class EventStoreSubscriptionManager : IEventSubscriptionManager, IDisposable
{
    private readonly IStreamStoreConnection _connection;
    private readonly ILogger<EventStoreSubscriptionManager> _logger;
    private readonly Dictionary<string, EventStoreSubscription> _subscriptions = 
        new Dictionary<string, EventStoreSubscription>();
    
    public EventStoreSubscriptionManager(
        IStreamStoreConnection connection,
        ILogger<EventStoreSubscriptionManager> logger)
    {
        _connection = connection;
        _logger = logger;
    }
    
    public async Task SubscribeToStreamAsync(
        string streamName,
        Action<ResolvedEvent> eventAppeared,
        Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
        long? lastCheckpoint = null)
    {
        if (_subscriptions.ContainsKey(streamName))
        {
            throw new InvalidOperationException($"Already subscribed to stream {streamName}");
        }
        
        var settings = new CatchUpSubscriptionSettings(
            maxLiveQueueSize: 10000,
            readBatchSize: 500,
            verboseLogging: false,
            resolveLinkTos: true,
            subscriptionName: $"Subscription-{streamName}");
            
        var subscription = _connection.SubscribeToStreamFrom(
            streamName,
            lastCheckpoint,
            settings,
            eventAppeared,
            liveProcessingStarted: null,
            subscriptionDropped: subscriptionDropped ?? DefaultSubscriptionDropped);
            
        _subscriptions.Add(streamName, subscription);
        _logger.LogInformation("Subscribed to stream {StreamName}", streamName);
    }
    
    public void Dispose()
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Stop();
        }
        
        _subscriptions.Clear();
    }
    
    private void DefaultSubscriptionDropped(SubscriptionDropReason reason, Exception ex)
    {
        _logger.LogError(ex, "Subscription dropped: {Reason}", reason);
    }
}

// Register in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IEventSubscriptionManager, EventStoreSubscriptionManager>();
}

// Usage in application startup
public class ApplicationStartup
{
    private readonly IEventSubscriptionManager _subscriptionManager;
    private readonly IReadModelProjection _accountProjection;
    private readonly ILogger<ApplicationStartup> _logger;
    
    public ApplicationStartup(
        IEventSubscriptionManager subscriptionManager,
        IReadModelProjection accountProjection,
        ILogger<ApplicationStartup> logger)
    {
        _subscriptionManager = subscriptionManager;
        _accountProjection = accountProjection;
        _logger = logger;
    }
    
    public async Task StartAsync()
    {
        // Subscribe to category streams
        await _subscriptionManager.SubscribeToStreamAsync(
            "$ce-account",
            eventAppeared: HandleAccountEvent,
            subscriptionDropped: HandleSubscriptionDropped);
    }
    
    private void HandleAccountEvent(ResolvedEvent resolvedEvent)
    {
        try
        {
            // Deserialize and process event
            var eventType = Type.GetType(resolvedEvent.Event.EventType);
            var eventData = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
            var @event = JsonConvert.DeserializeObject(eventData, eventType);
            
            // Project event to read model
            _accountProjection.Project(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event {EventId}", resolvedEvent.Event.EventId);
        }
    }
    
    private void HandleSubscriptionDropped(SubscriptionDropReason reason, Exception ex)
    {
        _logger.LogError(ex, "Account subscription dropped: {Reason}", reason);
        
        // Implement retry logic
        Task.Delay(TimeSpan.FromSeconds(5))
            .ContinueWith(_ => StartAsync())
            .ConfigureAwait(false);
    }
}
```

### 4. Stream Naming Conventions

Establish consistent stream naming conventions for your event-sourced system:

```csharp
public static class StreamNamingConventions
{
    // Stream for a specific aggregate instance
    public static string GetAggregateStreamName<TAggregate, TId>(TId id)
        where TAggregate : AggregateRoot<TId>
    {
        return $"{typeof(TAggregate).Name.ToLower()}-{id}";
    }
    
    // Category stream for all aggregates of a type
    public static string GetCategoryStreamName<TAggregate>()
        where TAggregate : AggregateRoot<object>
    {
        return $"$ce-{typeof(TAggregate).Name.ToLower()}";
    }
    
    // Stream for all events of a specific type
    public static string GetEventTypeStreamName<TEvent>()
        where TEvent : IEvent
    {
        return $"$et-{typeof(TEvent).Name.ToLower()}";
    }
    
    // Stream for a specific process manager
    public static string GetProcessManagerStreamName<TProcessManager, TId>(TId id)
        where TProcessManager : ProcessManager<TId>
    {
        return $"processmanager-{typeof(TProcessManager).Name.ToLower()}-{id}";
    }
    
    // Stream for a specific read model
    public static string GetReadModelStreamName<TReadModel, TId>(TId id)
        where TReadModel : ReadModelBase<TId>
    {
        return $"readmodel-{typeof(TReadModel).Name.ToLower()}-{id}";
    }
}

// Usage in repository
public class StreamStoreRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly IStreamStoreConnection _connection;
    
    public StreamStoreRepository(IStreamStoreConnection connection)
    {
        _connection = connection;
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(id);
        // Implementation...
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(aggregate.Id);
        // Implementation...
    }
}
```

## Message Bus Setup

The message bus is a critical component in CQRS and event-sourced systems, responsible for routing commands and events to their appropriate handlers. Reactive Domain provides a robust implementation that supports both synchronous and asynchronous messaging patterns.

### 1. Command Bus Configuration

The command bus routes commands to their handlers and enforces the one-command-one-handler principle:

```csharp
public class CommandBus : ICommandBus
{
    private readonly Dictionary<Type, Func<ICommand, Task>> _handlers = 
        new Dictionary<Type, Func<ICommand, Task>>();
    private readonly ILogger<CommandBus> _logger;
    
    public CommandBus(ILogger<CommandBus> logger)
    {
        _logger = logger;
    }
    
    public void Register<TCommand>(Func<TCommand, Task> handler) where TCommand : ICommand
    {
        var commandType = typeof(TCommand);
        if (_handlers.ContainsKey(commandType))
        {
            throw new InvalidOperationException(
                $"Handler for command type {commandType.Name} is already registered");
        }
        
        _handlers[commandType] = cmd => handler((TCommand)cmd);
        _logger.LogInformation("Registered handler for command type {CommandType}", commandType.Name);
    }
    
    public async Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        var commandType = command.GetType();
        if (!_handlers.TryGetValue(commandType, out var handler))
        {
            throw new InvalidOperationException(
                $"No handler registered for command type {commandType.Name}");
        }
        
        try
        {
            _logger.LogDebug("Executing command {CommandType} with ID {CommandId}", 
                commandType.Name, command.CommandId);
                
            await handler(command);
            
            _logger.LogDebug("Command {CommandType} with ID {CommandId} executed successfully", 
                commandType.Name, command.CommandId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandType} with ID {CommandId}", 
                commandType.Name, command.CommandId);
            throw;
        }
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register command bus
    services.AddSingleton<ICommandBus, CommandBus>();
    
    // Register command handlers
    services.AddTransient<CreateAccountHandler>();
    services.AddTransient<DepositFundsHandler>();
    services.AddTransient<WithdrawFundsHandler>();
    
    // Register command handler registrar
    services.AddSingleton<ICommandHandlerRegistrar>(provider =>
    {
        var commandBus = provider.GetRequiredService<ICommandBus>();
        var registrar = new CommandHandlerRegistrar(commandBus);
        
        // Register handlers
        registrar.Register<CreateAccount>(provider.GetRequiredService<CreateAccountHandler>().Handle);
        registrar.Register<DepositFunds>(provider.GetRequiredService<DepositFundsHandler>().Handle);
        registrar.Register<WithdrawFunds>(provider.GetRequiredService<WithdrawFundsHandler>().Handle);
        
        return registrar;
    });
}
```

### 2. Event Bus Configuration

The event bus distributes events to multiple subscribers, enabling the implementation of the Observer pattern:

```csharp
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = 
        new Dictionary<Type, List<Func<object, Task>>>();
    private readonly ILogger<EventBus> _logger;
    
    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }
    
    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class, IEvent
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers))
        {
            handlers = new List<Func<object, Task>>();
            _handlers[eventType] = handlers;
        }
        
        handlers.Add(evt => handler((TEvent)evt));
        _logger.LogInformation("Subscribed handler for event type {EventType}", eventType.Name);
    }
    
    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class, IEvent
    {
        var eventType = @event.GetType();
        if (!_handlers.TryGetValue(eventType, out var handlers))
        {
            _logger.LogDebug("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }
        
        _logger.LogDebug("Publishing event {EventType} with ID {EventId} to {HandlerCount} handlers", 
            eventType.Name, @event.EventId, handlers.Count);
            
        var tasks = handlers.Select(handler => 
            ExecuteHandlerSafely(handler, @event, eventType.Name));
            
        await Task.WhenAll(tasks);
    }
    
    private async Task ExecuteHandlerSafely(Func<object, Task> handler, IEvent @event, string eventTypeName)
    {
        try
        {
            await handler(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling event {EventType} with ID {EventId}", 
                eventTypeName, @event.EventId);
                
            // Consider adding a dead letter queue or retry mechanism here
        }
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register event bus
    services.AddSingleton<IEventBus, EventBus>();
    
    // Register event handlers
    services.AddTransient<AccountCreatedHandler>();
    services.AddTransient<FundsDepositedHandler>();
    services.AddTransient<FundsWithdrawnHandler>();
    
    // Register event handler registrar
    services.AddSingleton<IEventHandlerRegistrar>(provider =>
    {
        var eventBus = provider.GetRequiredService<IEventBus>();
        var registrar = new EventHandlerRegistrar(eventBus);
        
        // Register handlers
        registrar.Register<AccountCreated>(provider.GetRequiredService<AccountCreatedHandler>().Handle);
        registrar.Register<FundsDeposited>(provider.GetRequiredService<FundsDepositedHandler>().Handle);
        registrar.Register<FundsWithdrawn>(provider.GetRequiredService<FundsWithdrawnHandler>().Handle);
        
        return registrar;
    });
}
```

### 3. Asynchronous Message Processing

Implement asynchronous message processing for better scalability and responsiveness:

```csharp
public class AsyncEventProcessor : IEventProcessor, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IStreamStoreConnection _connection;
    private readonly ILogger<AsyncEventProcessor> _logger;
    private readonly ConcurrentDictionary<string, EventStoreSubscription> _subscriptions = 
        new ConcurrentDictionary<string, EventStoreSubscription>();
    private readonly ConcurrentDictionary<Type, string> _eventTypeToStreamName = 
        new ConcurrentDictionary<Type, string>();
    
    public AsyncEventProcessor(
        IEventBus eventBus,
        IStreamStoreConnection connection,
        ILogger<AsyncEventProcessor> logger)
    {
        _eventBus = eventBus;
        _connection = connection;
        _logger = logger;
    }
    
    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class, IEvent
    {
        // Register with the event bus
        _eventBus.Subscribe(handler);
        
        // Map event type to stream name
        var eventType = typeof(TEvent);
        var streamName = StreamNamingConventions.GetEventTypeStreamName<TEvent>();
        _eventTypeToStreamName[eventType] = streamName;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Subscribe to all registered event streams
        foreach (var kvp in _eventTypeToStreamName)
        {
            var streamName = kvp.Value;
            if (_subscriptions.ContainsKey(streamName))
            {
                continue;
            }
            
            var settings = new CatchUpSubscriptionSettings(
                maxLiveQueueSize: 10000,
                readBatchSize: 500,
                verboseLogging: false,
                resolveLinkTos: true,
                subscriptionName: $"AsyncProcessor-{streamName}");
                
            var subscription = _connection.SubscribeToStreamFrom(
                streamName,
                null, // Start from beginning
                settings,
                EventAppeared,
                liveProcessingStarted: null,
                subscriptionDropped: SubscriptionDropped);
                
            _subscriptions[streamName] = subscription;
            _logger.LogInformation("Subscribed to stream {StreamName}", streamName);
        }
    }
    
    private async Task EventAppeared(ResolvedEvent resolvedEvent)
    {
        try
        {
            // Deserialize event
            var eventType = Type.GetType(resolvedEvent.Event.EventType);
            if (eventType == null)
            {
                _logger.LogWarning("Unknown event type: {EventType}", resolvedEvent.Event.EventType);
                return;
            }
            
            var eventData = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
            var @event = JsonConvert.DeserializeObject(eventData, eventType) as IEvent;
            
            if (@event == null)
            {
                _logger.LogWarning("Failed to deserialize event: {EventId}", resolvedEvent.Event.EventId);
                return;
            }
            
            // Publish to event bus
            await _eventBus.PublishAsync(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event: {EventId}", resolvedEvent.Event.EventId);
        }
    }
    
    private void SubscriptionDropped(SubscriptionDropReason reason, Exception ex)
    {
        _logger.LogError(ex, "Subscription dropped: {Reason}", reason);
        
        // Implement retry logic
        Task.Delay(TimeSpan.FromSeconds(5))
            .ContinueWith(_ => StartAsync())
            .ConfigureAwait(false);
    }
    
    public void Dispose()
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Stop();
        }
        
        _subscriptions.Clear();
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register async event processor
    services.AddSingleton<IEventProcessor, AsyncEventProcessor>();
    
    // Register hosted service to start the processor
    services.AddHostedService<EventProcessorHostedService>();
}

public class EventProcessorHostedService : IHostedService
{
    private readonly IEventProcessor _eventProcessor;
    private readonly ILogger<EventProcessorHostedService> _logger;
    
    public EventProcessorHostedService(
        IEventProcessor eventProcessor,
        ILogger<EventProcessorHostedService> logger)
    {
        _eventProcessor = eventProcessor;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting event processor");
        await _eventProcessor.StartAsync(cancellationToken);
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping event processor");
        
        if (_eventProcessor is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        return Task.CompletedTask;
    }
}
```

### 4. Message Correlation and Tracing

Implement message correlation for tracking related commands and events across the system:

```csharp
public class CorrelationMiddleware : ICommandMiddleware
{
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<CorrelationMiddleware> _logger;
    
    public CorrelationMiddleware(
        ICorrelationIdProvider correlationIdProvider,
        ILogger<CorrelationMiddleware> logger)
    {
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }
    
    public async Task HandleAsync<TCommand>(TCommand command, Func<TCommand, Task> next)
        where TCommand : ICommand
    {
        if (command is ICorrelatedMessage correlatedMessage)
        {
            // If correlation ID is not set, set it
            if (string.IsNullOrEmpty(correlatedMessage.CorrelationId))
            {
                correlatedMessage.CorrelationId = _correlationIdProvider.GetCorrelationId();
                _logger.LogDebug("Set correlation ID {CorrelationId} for command {CommandType}",
                    correlatedMessage.CorrelationId, command.GetType().Name);
            }
            
            // Set correlation ID in ambient context
            using (_correlationIdProvider.SetCorrelationId(correlatedMessage.CorrelationId))
            {
                await next(command);
            }
        }
        else
        {
            await next(command);
        }
    }
}

public class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string> _currentCorrelationId = new AsyncLocal<string>();
    
    public string GetCorrelationId()
    {
        return _currentCorrelationId.Value ?? Guid.NewGuid().ToString();
    }
    
    public IDisposable SetCorrelationId(string correlationId)
    {
        var previousCorrelationId = _currentCorrelationId.Value;
        _currentCorrelationId.Value = correlationId;
        
        return new CorrelationIdScope(() => _currentCorrelationId.Value = previousCorrelationId);
    }
    
    private class CorrelationIdScope : IDisposable
    {
        private readonly Action _onDispose;
        
        public CorrelationIdScope(Action onDispose)
        {
            _onDispose = onDispose;
        }
        
        public void Dispose()
        {
            _onDispose();
        }
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register correlation ID provider
    services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
    
    // Register command middleware
    services.AddSingleton<ICommandMiddleware, CorrelationMiddleware>();
    
    // Register decorated command bus
    services.AddSingleton<ICommandBus>(provider =>
    {
        var innerBus = new CommandBus(provider.GetRequiredService<ILogger<CommandBus>>());
        var middleware = provider.GetRequiredService<ICommandMiddleware>();
        
        return new CommandBusWithMiddleware(innerBus, middleware);
    });
}

public class CommandBusWithMiddleware : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly ICommandMiddleware _middleware;
    
    public CommandBusWithMiddleware(
        ICommandBus innerBus,
        ICommandMiddleware middleware)
    {
        _innerBus = innerBus;
        _middleware = middleware;
    }
    
    public void Register<TCommand>(Func<TCommand, Task> handler) where TCommand : ICommand
    {
        _innerBus.Register(handler);
    }
    
    public async Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        await _middleware.HandleAsync(command, cmd => _innerBus.SendAsync(cmd));
    }
}
```

## Repository Configuration

Repositories in Reactive Domain provide a clean abstraction over the event store, handling the loading and saving of aggregates. Proper repository configuration is essential for efficient event sourcing.

### 1. Basic Repository Implementation

Implement a repository that loads and saves aggregates to the event store:

```csharp
public class StreamStoreRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly IStreamStoreConnection _connection;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<StreamStoreRepository<TAggregate, TId>> _logger;
    
    public StreamStoreRepository(
        IStreamStoreConnection connection,
        IEventSerializer serializer,
        ILogger<StreamStoreRepository<TAggregate, TId>> logger)
    {
        _connection = connection;
        _serializer = serializer;
        _logger = logger;
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(id);
        _logger.LogDebug("Loading aggregate {AggregateType} with ID {AggregateId} from stream {StreamName}",
            typeof(TAggregate).Name, id, streamName);
            
        var aggregate = new TAggregate();
        
        try
        {
            var sliceStart = 0L;
            const int sliceCount = 200;
            StreamEventsSlice slice;
            var events = new List<object>();
            
            do
            {
                slice = await _connection.ReadStreamEventsForwardAsync(
                    streamName, sliceStart, sliceCount, false);
                    
                if (slice.Status == SliceReadStatus.StreamNotFound)
                {
                    _logger.LogDebug("Stream {StreamName} not found", streamName);
                    break;
                }
                
                foreach (var resolvedEvent in slice.Events)
                {
                    var eventType = Type.GetType(resolvedEvent.Event.EventType);
                    if (eventType == null)
                    {
                        _logger.LogWarning("Unknown event type: {EventType}", resolvedEvent.Event.EventType);
                        continue;
                    }
                    
                    var eventData = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
                    var @event = _serializer.Deserialize(eventData, eventType);
                    
                    events.Add(@event);
                }
                
                sliceStart = slice.NextEventNumber;
            } while (!slice.IsEndOfStream);
            
            // Load events into aggregate
            aggregate.LoadFromHistory(events);
            
            _logger.LogDebug("Loaded aggregate {AggregateType} with ID {AggregateId} from {EventCount} events",
                typeof(TAggregate).Name, id, events.Count);
                
            return aggregate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, id);
            throw;
        }
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(aggregate.Id);
        var uncommittedEvents = aggregate.GetUncommittedEvents().ToList();
        
        if (!uncommittedEvents.Any())
        {
            _logger.LogDebug("No uncommitted events to save for aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, aggregate.Id);
            return;
        }
        
        _logger.LogDebug("Saving {EventCount} events for aggregate {AggregateType} with ID {AggregateId} to stream {StreamName}",
            uncommittedEvents.Count, typeof(TAggregate).Name, aggregate.Id, streamName);
            
        try
        {
            var expectedVersion = aggregate.Version - uncommittedEvents.Count;
            var eventData = uncommittedEvents.Select(e =>
            {
                var eventType = e.GetType();
                var data = _serializer.Serialize(e);
                var metadata = _serializer.Serialize(new EventMetadata
                {
                    AggregateType = typeof(TAggregate).AssemblyQualifiedName,
                    AggregateId = aggregate.Id.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                
                return new EventData(
                    Guid.NewGuid(),
                    eventType.AssemblyQualifiedName,
                    true,
                    Encoding.UTF8.GetBytes(data),
                    Encoding.UTF8.GetBytes(metadata));
            }).ToList();
            
            await _connection.AppendToStreamAsync(streamName, expectedVersion, eventData);
            
            // Clear uncommitted events after successful save
            aggregate.ClearUncommittedEvents();
            
            _logger.LogDebug("Successfully saved {EventCount} events for aggregate {AggregateType} with ID {AggregateId}",
                uncommittedEvents.Count, typeof(TAggregate).Name, aggregate.Id);
        }
        catch (WrongExpectedVersionException ex)
        {
            _logger.LogError(ex, "Concurrency conflict when saving aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, aggregate.Id);
            throw new ConcurrencyException(
                $"Concurrency conflict when saving aggregate {typeof(TAggregate).Name} with ID {aggregate.Id}",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, aggregate.Id);
            throw;
        }
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register event serializer
    services.AddSingleton<IEventSerializer, JsonEventSerializer>();
    
    // Register repositories
    services.AddSingleton<IRepository<Account, Guid>, StreamStoreRepository<Account, Guid>>();
    services.AddSingleton<IRepository<Customer, Guid>, StreamStoreRepository<Customer, Guid>>();
    services.AddSingleton<IRepository<Order, Guid>, StreamStoreRepository<Order, Guid>>();
}
```

### 2. Snapshot Repository Implementation

Implement a repository that supports snapshots for more efficient loading of aggregates with many events:

```csharp
public class SnapshotRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, ISnapshotable, new()
{
    private readonly IStreamStoreConnection _connection;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<SnapshotRepository<TAggregate, TId>> _logger;
    private readonly int _snapshotFrequency;
    
    public SnapshotRepository(
        IStreamStoreConnection connection,
        ISnapshotStore snapshotStore,
        IEventSerializer serializer,
        ILogger<SnapshotRepository<TAggregate, TId>> logger,
        int snapshotFrequency = 100)
    {
        _connection = connection;
        _snapshotStore = snapshotStore;
        _serializer = serializer;
        _logger = logger;
        _snapshotFrequency = snapshotFrequency;
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(id);
        var aggregate = new TAggregate();
        
        try
        {
            // Try to load snapshot first
            var snapshot = await _snapshotStore.GetSnapshotAsync(id.ToString());
            long sliceStart = 0;
            
            if (snapshot != null)
            {
                _logger.LogDebug("Found snapshot for aggregate {AggregateType} with ID {AggregateId} at version {Version}",
                    typeof(TAggregate).Name, id, snapshot.Version);
                    
                aggregate.RestoreFromSnapshot(snapshot.State);
                sliceStart = snapshot.Version + 1;
            }
            
            // Load events from snapshot version onwards
            const int sliceCount = 200;
            StreamEventsSlice slice;
            var events = new List<object>();
            
            do
            {
                slice = await _connection.ReadStreamEventsForwardAsync(
                    streamName, sliceStart, sliceCount, false);
                    
                if (slice.Status == SliceReadStatus.StreamNotFound)
                {
                    if (snapshot == null)
                    {
                        _logger.LogDebug("Stream {StreamName} not found", streamName);
                    }
                    break;
                }
                
                foreach (var resolvedEvent in slice.Events)
                {
                    var eventType = Type.GetType(resolvedEvent.Event.EventType);
                    if (eventType == null)
                    {
                        _logger.LogWarning("Unknown event type: {EventType}", resolvedEvent.Event.EventType);
                        continue;
                    }
                    
                    var eventData = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
                    var @event = _serializer.Deserialize(eventData, eventType);
                    
                    events.Add(@event);
                }
                
                sliceStart = slice.NextEventNumber;
            } while (!slice.IsEndOfStream);
            
            // Apply events after snapshot
            if (events.Any())
            {
                aggregate.LoadFromHistory(events);
                
                _logger.LogDebug("Loaded {EventCount} events for aggregate {AggregateType} with ID {AggregateId} after snapshot",
                    events.Count, typeof(TAggregate).Name, id);
            }
            
            return aggregate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, id);
            throw;
        }
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(aggregate.Id);
        var uncommittedEvents = aggregate.GetUncommittedEvents().ToList();
        
        if (!uncommittedEvents.Any())
        {
            _logger.LogDebug("No uncommitted events to save for aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, aggregate.Id);
            return;
        }
        
        _logger.LogDebug("Saving {EventCount} events for aggregate {AggregateType} with ID {AggregateId} to stream {StreamName}",
            uncommittedEvents.Count, typeof(TAggregate).Name, aggregate.Id, streamName);
            
        try
        {
            var expectedVersion = aggregate.Version - uncommittedEvents.Count;
            var eventData = uncommittedEvents.Select(e =>
            {
                var eventType = e.GetType();
                var data = _serializer.Serialize(e);
                var metadata = _serializer.Serialize(new EventMetadata
                {
                    AggregateType = typeof(TAggregate).AssemblyQualifiedName,
                    AggregateId = aggregate.Id.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                
                return new EventData(
                    Guid.NewGuid(),
                    eventType.AssemblyQualifiedName,
                    true,
                    Encoding.UTF8.GetBytes(data),
                    Encoding.UTF8.GetBytes(metadata));
            }).ToList();
            
            await _connection.AppendToStreamAsync(streamName, expectedVersion, eventData);
            
            // Check if we need to create a snapshot
            if (aggregate.Version % _snapshotFrequency == 0)
            {
                var snapshot = new Snapshot
                {
                    AggregateId = aggregate.Id.ToString(),
                    Version = aggregate.Version,
                    State = aggregate.CreateSnapshot(),
                    Timestamp = DateTime.UtcNow
                };
                
                await _snapshotStore.SaveSnapshotAsync(snapshot);
                
                _logger.LogDebug("Created snapshot for aggregate {AggregateType} with ID {AggregateId} at version {Version}",
                    typeof(TAggregate).Name, aggregate.Id, aggregate.Version);
            }
            
            // Clear uncommitted events after successful save
            aggregate.ClearUncommittedEvents();
            
            _logger.LogDebug("Successfully saved {EventCount} events for aggregate {AggregateType} with ID {AggregateId}",
                uncommittedEvents.Count, typeof(TAggregate).Name, aggregate.Id);
        }
        catch (WrongExpectedVersionException ex)
        {
            _logger.LogError(ex, "Concurrency conflict when saving aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, aggregate.Id);
            throw new ConcurrencyException(
                $"Concurrency conflict when saving aggregate {typeof(TAggregate).Name} with ID {aggregate.Id}",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving aggregate {AggregateType} with ID {AggregateId}",
                typeof(TAggregate).Name, aggregate.Id);
            throw;
        }
    }
}

// Snapshot store implementation
public class SqlSnapshotStore : ISnapshotStore
{
    private readonly string _connectionString;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<SqlSnapshotStore> _logger;
    
    public SqlSnapshotStore(
        string connectionString,
        IEventSerializer serializer,
        ILogger<SqlSnapshotStore> logger)
    {
        _connectionString = connectionString;
        _serializer = serializer;
        _logger = logger;
    }
    
    public async Task<Snapshot> GetSnapshotAsync(string aggregateId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            SELECT TOP 1 AggregateId, Version, State, Timestamp
            FROM Snapshots
            WHERE AggregateId = @AggregateId
            ORDER BY Version DESC";
            
        var snapshot = await connection.QueryFirstOrDefaultAsync<SnapshotRecord>(sql, new { AggregateId = aggregateId });
        
        if (snapshot == null)
        {
            return null;
        }
        
        var state = _serializer.Deserialize(snapshot.State, Type.GetType(snapshot.StateType));
        
        return new Snapshot
        {
            AggregateId = snapshot.AggregateId,
            Version = snapshot.Version,
            State = state,
            Timestamp = snapshot.Timestamp
        };
    }
    
    public async Task SaveSnapshotAsync(Snapshot snapshot)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            INSERT INTO Snapshots (AggregateId, Version, StateType, State, Timestamp)
            VALUES (@AggregateId, @Version, @StateType, @State, @Timestamp)";
            
        var stateType = snapshot.State.GetType();
        var serializedState = _serializer.Serialize(snapshot.State);
        
        await connection.ExecuteAsync(sql, new
        {
            snapshot.AggregateId,
            snapshot.Version,
            StateType = stateType.AssemblyQualifiedName,
            State = serializedState,
            snapshot.Timestamp
        });
    }
    
    private class SnapshotRecord
    {
        public string AggregateId { get; set; }
        public long Version { get; set; }
        public string StateType { get; set; }
        public string State { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register snapshot store
    services.AddSingleton<ISnapshotStore>(provider =>
    {
        var connectionString = Configuration.GetConnectionString("SnapshotStore");
        var serializer = provider.GetRequiredService<IEventSerializer>();
        var logger = provider.GetRequiredService<ILogger<SqlSnapshotStore>>();
        
        return new SqlSnapshotStore(connectionString, serializer, logger);
    });
    
    // Register repositories with snapshots
    services.AddSingleton<IRepository<Account, Guid>>(provider =>
    {
        var connection = provider.GetRequiredService<IStreamStoreConnection>();
        var snapshotStore = provider.GetRequiredService<ISnapshotStore>();
        var serializer = provider.GetRequiredService<IEventSerializer>();
        var logger = provider.GetRequiredService<ILogger<SnapshotRepository<Account, Guid>>>();
        
        return new SnapshotRepository<Account, Guid>(connection, snapshotStore, serializer, logger, 50);
    });
}
```

### 3. Correlated Repository Implementation

Implement a repository that supports correlation for tracking related aggregates:

```csharp
public class CorrelatedRepository<TAggregate, TId> : ICorrelatedRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly IRepository<TAggregate, TId> _innerRepository;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<CorrelatedRepository<TAggregate, TId>> _logger;
    
    public CorrelatedRepository(
        IRepository<TAggregate, TId> innerRepository,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<CorrelatedRepository<TAggregate, TId>> logger)
    {
        _innerRepository = innerRepository;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        return await _innerRepository.GetByIdAsync(id);
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        
        // Set correlation ID on uncommitted events
        var uncommittedEvents = aggregate.GetUncommittedEvents().ToList();
        foreach (var @event in uncommittedEvents.OfType<ICorrelatedMessage>())
        {
            if (string.IsNullOrEmpty(@event.CorrelationId))
            {
                @event.CorrelationId = correlationId;
            }
        }
        
        _logger.LogDebug("Saving aggregate {AggregateType} with ID {AggregateId} with correlation ID {CorrelationId}",
            typeof(TAggregate).Name, aggregate.Id, correlationId);
            
        await _innerRepository.SaveAsync(aggregate);
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register correlation ID provider
    services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
    
    // Register inner repositories
    services.AddSingleton<IRepository<Account, Guid>, StreamStoreRepository<Account, Guid>>();
    
    // Register correlated repositories as decorators
    services.AddSingleton<ICorrelatedRepository<Account, Guid>>(provider =>
    {
        var innerRepository = provider.GetRequiredService<IRepository<Account, Guid>>();
        var correlationIdProvider = provider.GetRequiredService<ICorrelationIdProvider>();
        var logger = provider.GetRequiredService<ILogger<CorrelatedRepository<Account, Guid>>>();
        
        return new CorrelatedRepository<Account, Guid>(innerRepository, correlationIdProvider, logger);
    });
}
```

### 4. Repository Factory

Implement a factory for creating repositories dynamically:

```csharp
public class RepositoryFactory : IRepositoryFactory
{
    private readonly IServiceProvider _serviceProvider;
    
    public RepositoryFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IRepository<TAggregate, TId> CreateRepository<TAggregate, TId>()
        where TAggregate : AggregateRoot<TId>, new()
    {
        return _serviceProvider.GetRequiredService<IRepository<TAggregate, TId>>();
    }
    
    public ICorrelatedRepository<TAggregate, TId> CreateCorrelatedRepository<TAggregate, TId>()
        where TAggregate : AggregateRoot<TId>, new()
    {
        return _serviceProvider.GetRequiredService<ICorrelatedRepository<TAggregate, TId>>();
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register repository factory
    services.AddSingleton<IRepositoryFactory, RepositoryFactory>();
}

// Usage in application code
public class AccountService
{
    private readonly IRepositoryFactory _repositoryFactory;
    
    public AccountService(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }
    
    public async Task<Account> GetAccountAsync(Guid id)
    {
        var repository = _repositoryFactory.CreateRepository<Account, Guid>();
        return await repository.GetByIdAsync(id);
    }
    
    public async Task CreateAccountAsync(string name, string accountNumber)
    {
        var repository = _repositoryFactory.CreateCorrelatedRepository<Account, Guid>();
        var account = new Account();
        account.Create(Guid.NewGuid(), name, accountNumber);
        await repository.SaveAsync(account);
    }
}
```

## Read Model Implementation

Read models in event-sourced systems provide optimized views of the data for querying. They are updated in response to events and are designed for efficient reads.

### 1. Basic Read Model Projector

Implement a projector that updates read models in response to domain events:

```csharp
public class AccountSummaryProjector : IEventHandler<AccountCreated>,
                                     IEventHandler<DepositMade>,
                                     IEventHandler<WithdrawalMade>
{
    private readonly IReadModelDbContext _dbContext;
    private readonly ILogger<AccountSummaryProjector> _logger;
    
    public AccountSummaryProjector(
        IReadModelDbContext dbContext,
        ILogger<AccountSummaryProjector> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async Task HandleAsync(AccountCreated @event)
    {
        _logger.LogDebug("Projecting AccountCreated event for account {AccountId}", @event.AccountId);
        
        var accountSummary = new AccountSummary
        {
            Id = @event.AccountId,
            AccountNumber = @event.AccountNumber,
            Name = @event.Name,
            Balance = 0,
            CreatedAt = @event.Timestamp,
            UpdatedAt = @event.Timestamp,
            Version = 1
        };
        
        _dbContext.AccountSummaries.Add(accountSummary);
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task HandleAsync(DepositMade @event)
    {
        _logger.LogDebug("Projecting DepositMade event for account {AccountId}", @event.AccountId);
        
        var accountSummary = await _dbContext.AccountSummaries
            .FindAsync(@event.AccountId);
            
        if (accountSummary == null)
        {
            _logger.LogWarning("Account summary not found for account {AccountId}", @event.AccountId);
            return;
        }
        
        accountSummary.Balance += @event.Amount;
        accountSummary.UpdatedAt = @event.Timestamp;
        accountSummary.Version++;
        
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task HandleAsync(WithdrawalMade @event)
    {
        _logger.LogDebug("Projecting WithdrawalMade event for account {AccountId}", @event.AccountId);
        
        var accountSummary = await _dbContext.AccountSummaries
            .FindAsync(@event.AccountId);
            
        if (accountSummary == null)
        {
            _logger.LogWarning("Account summary not found for account {AccountId}", @event.AccountId);
            return;
        }
        
        accountSummary.Balance -= @event.Amount;
        accountSummary.UpdatedAt = @event.Timestamp;
        accountSummary.Version++;
        
        await _dbContext.SaveChangesAsync();
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register read model projector
    services.AddScoped<AccountSummaryProjector>();
    
    // Register event handlers
    services.AddScoped<IEventHandler<AccountCreated>>(provider => 
        provider.GetRequiredService<AccountSummaryProjector>());
    services.AddScoped<IEventHandler<DepositMade>>(provider => 
        provider.GetRequiredService<AccountSummaryProjector>());
    services.AddScoped<IEventHandler<WithdrawalMade>>(provider => 
        provider.GetRequiredService<AccountSummaryProjector>());
}
```

### 2. Database Context for Read Models

Implement a database context for read models using Entity Framework Core:

```csharp
public class ReadModelDbContext : DbContext, IReadModelDbContext
{
    public ReadModelDbContext(DbContextOptions<ReadModelDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<AccountSummary> AccountSummaries { get; set; }
    public DbSet<TransactionSummary> TransactionSummaries { get; set; }
    public DbSet<CustomerSummary> CustomerSummaries { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountSummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Balance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.HasIndex(e => e.AccountNumber).IsUnique();
        });
        
        modelBuilder.Entity<TransactionSummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountId).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.Timestamp);
        });
        
        modelBuilder.Entity<CustomerSummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}

public interface IReadModelDbContext
{
    DbSet<AccountSummary> AccountSummaries { get; }
    DbSet<TransactionSummary> TransactionSummaries { get; }
    DbSet<CustomerSummary> CustomerSummaries { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register read model database context
    services.AddDbContext<ReadModelDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("ReadModelDb")));
    services.AddScoped<IReadModelDbContext>(provider =>
        provider.GetRequiredService<ReadModelDbContext>());
}
```

### 3. Catch-Up Subscription Manager

Implement a service that manages catch-up subscriptions to the event store for updating read models:

```csharp
public class CatchUpSubscriptionManager : BackgroundService
{
    private readonly IStreamStoreConnection _connection;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CatchUpSubscriptionManager> _logger;
    private readonly ICheckpointStore _checkpointStore;
    private readonly Dictionary<string, EventStoreAllCatchUpSubscription> _subscriptions;
    
    public CatchUpSubscriptionManager(
        IStreamStoreConnection connection,
        IServiceScopeFactory serviceScopeFactory,
        ICheckpointStore checkpointStore,
        ILogger<CatchUpSubscriptionManager> logger)
    {
        _connection = connection;
        _serviceScopeFactory = serviceScopeFactory;
        _checkpointStore = checkpointStore;
        _logger = logger;
        _subscriptions = new Dictionary<string, EventStoreAllCatchUpSubscription>();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting catch-up subscription manager");
        
        // Create subscriptions for each projection group
        await CreateSubscription("AccountProjections", HandleAccountEvent, stoppingToken);
        await CreateSubscription("CustomerProjections", HandleCustomerEvent, stoppingToken);
        await CreateSubscription("TransactionProjections", HandleTransactionEvent, stoppingToken);
        
        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        
        // Stop all subscriptions when the service is stopping
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Stop();
        }
        
        _logger.LogInformation("Catch-up subscription manager stopped");
    }
    
    private async Task CreateSubscription(
        string subscriptionName,
        Action<EventStoreCatchUpSubscription, ResolvedEvent> eventHandler,
        CancellationToken stoppingToken)
    {
        var checkpoint = await _checkpointStore.GetCheckpointAsync(subscriptionName);
        var position = checkpoint != null ? new Position(checkpoint.Value, checkpoint.Value) : Position.Start;
        
        _logger.LogInformation("Starting {SubscriptionName} subscription from position {Position}",
            subscriptionName, position);
            
        var settings = new CatchUpSubscriptionSettings(
            maxLiveQueueSize: 10000,
            readBatchSize: 500,
            verboseLogging: false,
            resolveLinkTos: true,
            subscriptionName: subscriptionName);
            
        var subscription = _connection.SubscribeToAllFrom(
            position,
            settings,
            eventAppeared: (sub, evt) => {
                try
                {
                    eventHandler(sub, evt);
                    
                    // Update checkpoint
                    var position = evt.OriginalPosition?.CommitPosition ?? 0;
                    _checkpointStore.StoreCheckpointAsync(subscriptionName, position).Wait();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event in subscription {SubscriptionName}",
                        subscriptionName);
                }
                
                return Task.CompletedTask;
            },
            liveProcessingStarted: _ => {
                _logger.LogInformation("{SubscriptionName} subscription caught up to live events",
                    subscriptionName);
                return Task.CompletedTask;
            },
            subscriptionDropped: (sub, reason, ex) => {
                _logger.LogWarning(ex, "{SubscriptionName} subscription dropped: {Reason}",
                    subscriptionName, reason);
                    
                if (reason != SubscriptionDropReason.UserInitiated && !stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Restarting {SubscriptionName} subscription", subscriptionName);
                    Task.Delay(TimeSpan.FromSeconds(5), stoppingToken)
                        .ContinueWith(_ => CreateSubscription(subscriptionName, eventHandler, stoppingToken));
                }
                
                return Task.CompletedTask;
            });
            
        _subscriptions[subscriptionName] = subscription;
    }
    
    private void HandleAccountEvent(EventStoreCatchUpSubscription subscription, ResolvedEvent resolvedEvent)
    {
        if (resolvedEvent.Event.EventType.StartsWith("Account"))
        {
            ProcessEvent(resolvedEvent);
        }
    }
    
    private void HandleCustomerEvent(EventStoreCatchUpSubscription subscription, ResolvedEvent resolvedEvent)
    {
        if (resolvedEvent.Event.EventType.StartsWith("Customer"))
        {
            ProcessEvent(resolvedEvent);
        }
    }
    
    private void HandleTransactionEvent(EventStoreCatchUpSubscription subscription, ResolvedEvent resolvedEvent)
    {
        if (resolvedEvent.Event.EventType.StartsWith("Transaction") ||
            resolvedEvent.Event.EventType == "DepositMade" ||
            resolvedEvent.Event.EventType == "WithdrawalMade")
        {
            ProcessEvent(resolvedEvent);
        }
    }
    
    private void ProcessEvent(ResolvedEvent resolvedEvent)
    {
        var eventType = Type.GetType(resolvedEvent.Event.EventType);
        if (eventType == null)
        {
            _logger.LogWarning("Unknown event type: {EventType}", resolvedEvent.Event.EventType);
            return;
        }
        
        using var scope = _serviceScopeFactory.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();
        
        var eventData = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
        var @event = serializer.Deserialize(eventData, eventType);
        
        eventBus.PublishAsync(@event).Wait();
    }
}

// Checkpoint store implementation
public class SqlCheckpointStore : ICheckpointStore
{
    private readonly string _connectionString;
    private readonly ILogger<SqlCheckpointStore> _logger;
    
    public SqlCheckpointStore(string connectionString, ILogger<SqlCheckpointStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }
    
    public async Task<long?> GetCheckpointAsync(string subscriptionName)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = "SELECT Position FROM Checkpoints WHERE SubscriptionName = @SubscriptionName";
        var position = await connection.QueryFirstOrDefaultAsync<long?>(sql, new { SubscriptionName = subscriptionName });
        
        _logger.LogDebug("Retrieved checkpoint for {SubscriptionName}: {Position}",
            subscriptionName, position);
            
        return position;
    }
    
    public async Task StoreCheckpointAsync(string subscriptionName, long position)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            MERGE INTO Checkpoints WITH (HOLDLOCK) AS target
            USING (SELECT @SubscriptionName AS SubscriptionName) AS source
            ON target.SubscriptionName = source.SubscriptionName
            WHEN MATCHED AND target.Position < @Position THEN
                UPDATE SET Position = @Position, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (SubscriptionName, Position, UpdatedAt)
                VALUES (@SubscriptionName, @Position, GETUTCDATE());
        ";
        
        await connection.ExecuteAsync(sql, new
        {
            SubscriptionName = subscriptionName,
            Position = position
        });
        
        _logger.LogDebug("Stored checkpoint for {SubscriptionName} at position {Position}",
            subscriptionName, position);
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register checkpoint store
    services.AddSingleton<ICheckpointStore>(provider =>
    {
        var connectionString = Configuration.GetConnectionString("ReadModelDb");
        var logger = provider.GetRequiredService<ILogger<SqlCheckpointStore>>();
        
        return new SqlCheckpointStore(connectionString, logger);
    });
    
    // Register catch-up subscription manager as a hosted service
    services.AddHostedService<CatchUpSubscriptionManager>();
}
```

### 4. Query Service Implementation

Implement query services that provide optimized access to read models:

```csharp
public class AccountQueryService : IAccountQueryService
{
    private readonly IReadModelDbContext _dbContext;
    private readonly ILogger<AccountQueryService> _logger;
    
    public AccountQueryService(
        IReadModelDbContext dbContext,
        ILogger<AccountQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async Task<AccountSummaryDto> GetAccountByIdAsync(Guid id)
    {
        _logger.LogDebug("Getting account summary for account {AccountId}", id);
        
        var account = await _dbContext.AccountSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
            
        if (account == null)
        {
            _logger.LogWarning("Account summary not found for account {AccountId}", id);
            return null;
        }
        
        return new AccountSummaryDto
        {
            Id = account.Id,
            AccountNumber = account.AccountNumber,
            Name = account.Name,
            Balance = account.Balance,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }
    
    public async Task<IEnumerable<AccountSummaryDto>> GetAllAccountsAsync(int page = 1, int pageSize = 10)
    {
        _logger.LogDebug("Getting all account summaries, page {Page}, pageSize {PageSize}", page, pageSize);
        
        var accounts = await _dbContext.AccountSummaries
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            
        return accounts.Select(account => new AccountSummaryDto
        {
            Id = account.Id,
            AccountNumber = account.AccountNumber,
            Name = account.Name,
            Balance = account.Balance,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        });
    }
    
    public async Task<IEnumerable<TransactionSummaryDto>> GetAccountTransactionsAsync(
        Guid accountId, int page = 1, int pageSize = 20)
    {
        _logger.LogDebug("Getting transactions for account {AccountId}, page {Page}, pageSize {PageSize}",
            accountId, page, pageSize);
            
        var transactions = await _dbContext.TransactionSummaries
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            
        return transactions.Select(transaction => new TransactionSummaryDto
        {
            Id = transaction.Id,
            AccountId = transaction.AccountId,
            Type = transaction.Type,
            Amount = transaction.Amount,
            Description = transaction.Description,
            Timestamp = transaction.Timestamp
        });
    }
}

// Registration in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register query services
    services.AddScoped<IAccountQueryService, AccountQueryService>();
    services.AddScoped<ICustomerQueryService, CustomerQueryService>();
}
```

### 5. Handling Eventual Consistency

Implement a service that helps clients deal with eventual consistency between write and read models:

```csharp
public class EventualConsistencyService : IEventualConsistencyService
{
    private readonly IStreamStoreConnection _connection;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<EventualConsistencyService> _logger;
    
    public EventualConsistencyService(
        IStreamStoreConnection connection,
        IEventSerializer serializer,
        ILogger<EventualConsistencyService> logger)
    {
        _connection = connection;
        _serializer = serializer;
        _logger = logger;
    }
    
    public async Task<bool> WaitForProjectionAsync<TEvent>(
        Guid aggregateId,
        Func<Task<bool>> readModelCheck,
        TimeSpan timeout,
        int maxAttempts = 10)
    {
        var streamName = StreamNamingConventions.GetAggregateStreamName<TEvent>(aggregateId);
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        
        while (stopwatch.Elapsed < timeout && attempts < maxAttempts)
        {
            attempts++;
            
            // Check if the read model has been updated
            if (await readModelCheck())
            {
                _logger.LogDebug("Read model for {AggregateId} is consistent after {ElapsedMs}ms and {Attempts} attempts",
                    aggregateId, stopwatch.ElapsedMilliseconds, attempts);
                return true;
            }
            
            // Wait before trying again
            var delay = CalculateExponentialBackoff(attempts);
            await Task.Delay(delay);
        }
        
        _logger.LogWarning("Read model for {AggregateId} did not become consistent after {ElapsedMs}ms and {Attempts} attempts",
            aggregateId, stopwatch.ElapsedMilliseconds, attempts);
        return false;
    }
    
    private TimeSpan CalculateExponentialBackoff(int attempt)
    {
        // Start with 50ms, then exponentially increase (50, 100, 200, 400, 800, etc.)
        var delayMs = Math.Min(50 * Math.Pow(2, attempt - 1), 2000);
        return TimeSpan.FromMilliseconds(delayMs);
    }
}

// Usage in application code
public class AccountController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IAccountQueryService _accountQueryService;
    private readonly IEventualConsistencyService _consistencyService;
    
    public AccountController(
        ICommandBus commandBus,
        IAccountQueryService accountQueryService,
        IEventualConsistencyService consistencyService)
    {
        _commandBus = commandBus;
        _accountQueryService = accountQueryService;
        _consistencyService = consistencyService;
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var accountId = Guid.NewGuid();
        var command = new CreateAccount(accountId, request.Name, request.InitialDeposit);
        
        await _commandBus.SendAsync(command);
        
        // Wait for the read model to be updated
        var isConsistent = await _consistencyService.WaitForProjectionAsync<Account>(
            accountId,
            async () => await _accountQueryService.GetAccountByIdAsync(accountId) != null,
            TimeSpan.FromSeconds(5));
            
        if (isConsistent)
        {
            var account = await _accountQueryService.GetAccountByIdAsync(accountId);
            return CreatedAtAction(nameof(GetAccount), new { id = accountId }, account);
        }
        
        // Return a 202 Accepted if the read model is not yet updated
        return AcceptedAtAction(nameof(GetAccount), new { id = accountId });
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var account = await _accountQueryService.GetAccountByIdAsync(id);
        
        if (account == null)
        {
            return NotFound();
        }
        
        return Ok(account);
    }
}
```
