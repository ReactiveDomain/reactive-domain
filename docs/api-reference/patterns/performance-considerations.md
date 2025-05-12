# Performance Considerations for Reactive Domain Applications

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

This document outlines key performance considerations and optimization techniques for Reactive Domain applications, focusing on event sourcing, CQRS, and read model optimization.

## Table of Contents

1. [Event Stream Optimization](#event-stream-optimization)
2. [Snapshot Strategies](#snapshot-strategies)
3. [Read Model Performance](#read-model-performance)
4. [Caching Strategies](#caching-strategies)
5. [Scaling Event-Sourced Systems](#scaling-event-sourced-systems)
6. [Monitoring and Profiling](#monitoring-and-profiling)
7. [Best Practices](#best-practices)

## Event Stream Optimization

Efficient handling of event streams is critical for performance in event-sourced systems, especially as streams grow larger over time.

### 1. Batch Loading of Events

When loading events from the event store, use batching to avoid memory pressure:

```csharp
public async Task<TAggregate> GetByIdAsync(TId id)
{
    var streamName = StreamNamingConventions.GetAggregateStreamName<TAggregate, TId>(id);
    var aggregate = new TAggregate();
    
    // Load events in batches
    var sliceStart = 0L;
    const int sliceCount = 200; // Optimal batch size
    StreamEventsSlice slice;
    var events = new List<object>();
    
    do
    {
        slice = await _connection.ReadStreamEventsForwardAsync(
            streamName, sliceStart, sliceCount, false);
            
        // Process batch...
        foreach (var resolvedEvent in slice.Events)
        {
            // Deserialize and add to events list
            // ...
        }
        
        sliceStart = slice.NextEventNumber;
    } while (!slice.IsEndOfStream);
    
    // Apply events to aggregate
    aggregate.LoadFromHistory(events);
    
    return aggregate;
}
```

### 2. Event Stream Partitioning

For high-volume systems, consider partitioning event streams by logical boundaries:

```csharp
// Stream naming convention with partitioning
public static string GetPartitionedStreamName<TAggregate, TId>(TId id, string partition)
{
    return $"{typeof(TAggregate).Name}-{partition}-{id}";
}
```

### 3. Optimizing Event Size

Keep events small and focused to improve serialization/deserialization performance:

```csharp
// Good: Small, focused event
public class ItemAddedToCart : Event
{
    public Guid CartId { get; }
    public Guid ProductId { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }
    
    public ItemAddedToCart(Guid cartId, Guid productId, int quantity, decimal unitPrice)
    {
        CartId = cartId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}

// Avoid: Large, unfocused event
public class CartUpdated : Event
{
    public Guid CartId { get; }
    public List<CartItem> Items { get; } // Potentially large collection
    public CustomerInfo CustomerDetails { get; } // Complex nested object
    public Dictionary<string, object> Metadata { get; } // Unstructured data
    
    // Constructor...
}
```

## Snapshot Strategies

Snapshots can significantly improve performance for aggregates with many events by reducing the number of events that need to be loaded and applied.

### 1. Frequency-Based Snapshots

Create snapshots based on the number of events since the last snapshot:

```csharp
public async Task SaveAsync(TAggregate aggregate)
{
    // Save events...
    
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
    }
}
```

### 2. Time-Based Snapshots

For aggregates that change frequently, consider time-based snapshot strategies:

```csharp
public async Task SaveAsync(TAggregate aggregate)
{
    // Save events...
    
    // Check if we need to create a snapshot based on time
    var lastSnapshot = await _snapshotStore.GetLastSnapshotTimeAsync(aggregate.Id.ToString());
    var timeSinceLastSnapshot = DateTime.UtcNow - (lastSnapshot ?? DateTime.MinValue);
    
    if (timeSinceLastSnapshot > TimeSpan.FromHours(24))
    {
        var snapshot = new Snapshot
        {
            AggregateId = aggregate.Id.ToString(),
            Version = aggregate.Version,
            State = aggregate.CreateSnapshot(),
            Timestamp = DateTime.UtcNow
        };
        
        await _snapshotStore.SaveSnapshotAsync(snapshot);
    }
}
```

### 3. Snapshot Storage Optimization

Optimize snapshot storage for fast retrieval:

```csharp
public class SqlSnapshotStore : ISnapshotStore
{
    // ...
    
    public async Task<Snapshot> GetSnapshotAsync(string aggregateId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        // Use indexed query for fast retrieval
        var sql = @"
            SELECT TOP 1 AggregateId, Version, StateType, State, Timestamp
            FROM Snapshots WITH (INDEX(IX_Snapshots_AggregateId_Version))
            WHERE AggregateId = @AggregateId
            ORDER BY Version DESC";
            
        // ...
    }
}
```

## Read Model Performance

Optimizing read models is essential for query performance in CQRS architectures.

### 1. Denormalized Read Models

Design read models specifically for query patterns to avoid joins:

```csharp
// Denormalized read model for order details
public class OrderDetailReadModel
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Denormalized customer data
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
    
    // Denormalized shipping data
    public string ShippingAddress { get; set; }
    public string ShippingCity { get; set; }
    public string ShippingPostalCode { get; set; }
    
    // Denormalized payment data
    public string PaymentMethod { get; set; }
    public string PaymentStatus { get; set; }
    
    // Items in a JSON column for flexible querying
    public string ItemsJson { get; set; }
}
```

### 2. Optimized Indexing

Create appropriate indexes for common query patterns:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<OrderDetailReadModel>(entity =>
    {
        entity.HasKey(e => e.OrderId);
        
        // Index for customer queries
        entity.HasIndex(e => e.CustomerId);
        
        // Composite index for date-based queries
        entity.HasIndex(e => new { e.OrderDate, e.TotalAmount });
        
        // Full-text search index for order items
        entity.HasIndex(e => e.ItemsJson).ForFullTextSearch();
        
        // Filtered index for specific query patterns
        entity.HasIndex(e => e.PaymentStatus)
            .HasFilter("PaymentStatus = 'Pending'")
            .HasName("IX_OrderDetail_PendingPayments");
    });
}
```

### 3. Asynchronous Projections

Process projections asynchronously to avoid blocking the command path:

```csharp
public class CatchUpSubscriptionManager : BackgroundService
{
    // ...
    
    private void ProcessEvent(ResolvedEvent resolvedEvent)
    {
        // Deserialize event
        var eventType = Type.GetType(resolvedEvent.Event.EventType);
        var eventData = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
        var @event = _serializer.Deserialize(eventData, eventType);
        
        // Queue projection work to avoid blocking
        _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(@event);
        });
    }
}
```

## Caching Strategies

Implement caching to reduce database load and improve response times.

### 1. Aggregate Caching

Cache frequently accessed aggregates to reduce event store load:

```csharp
public class CachingRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly IRepository<TAggregate, TId> _innerRepository;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration;
    
    public CachingRepository(
        IRepository<TAggregate, TId> innerRepository,
        IMemoryCache cache,
        TimeSpan? cacheExpiration = null)
    {
        _innerRepository = innerRepository;
        _cache = cache;
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromMinutes(5);
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        var cacheKey = $"{typeof(TAggregate).Name}:{id}";
        
        if (_cache.TryGetValue(cacheKey, out TAggregate cachedAggregate))
        {
            return cachedAggregate;
        }
        
        var aggregate = await _innerRepository.GetByIdAsync(id);
        
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheExpiration);
            
        _cache.Set(cacheKey, aggregate, cacheOptions);
        
        return aggregate;
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        await _innerRepository.SaveAsync(aggregate);
        
        // Update cache after saving
        var cacheKey = $"{typeof(TAggregate).Name}:{aggregate.Id}";
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheExpiration);
            
        _cache.Set(cacheKey, aggregate, cacheOptions);
    }
}
```

### 2. Read Model Caching

Cache query results to improve read performance:

```csharp
public class CachingAccountQueryService : IAccountQueryService
{
    private readonly IAccountQueryService _innerService;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration;
    
    public CachingAccountQueryService(
        IAccountQueryService innerService,
        IMemoryCache cache,
        TimeSpan? cacheExpiration = null)
    {
        _innerService = innerService;
        _cache = cache;
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromMinutes(1);
    }
    
    public async Task<AccountSummaryDto> GetAccountByIdAsync(Guid id)
    {
        var cacheKey = $"Account:{id}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(_cacheExpiration);
            return await _innerService.GetAccountByIdAsync(id);
        });
    }
    
    public async Task<IEnumerable<AccountSummaryDto>> GetAllAccountsAsync(int page = 1, int pageSize = 10)
    {
        var cacheKey = $"Accounts:Page:{page}:Size:{pageSize}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(_cacheExpiration);
            return await _innerService.GetAllAccountsAsync(page, pageSize);
        });
    }
}
```

### 3. Distributed Caching

For scaled-out applications, use distributed caching:

```csharp
public class DistributedCachingRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly IRepository<TAggregate, TId> _innerRepository;
    private readonly IDistributedCache _cache;
    private readonly IEventSerializer _serializer;
    private readonly TimeSpan _cacheExpiration;
    
    public DistributedCachingRepository(
        IRepository<TAggregate, TId> innerRepository,
        IDistributedCache cache,
        IEventSerializer serializer,
        TimeSpan? cacheExpiration = null)
    {
        _innerRepository = innerRepository;
        _cache = cache;
        _serializer = serializer;
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromMinutes(5);
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        var cacheKey = $"{typeof(TAggregate).Name}:{id}";
        var cachedData = await _cache.GetStringAsync(cacheKey);
        
        if (!string.IsNullOrEmpty(cachedData))
        {
            return _serializer.Deserialize<TAggregate>(cachedData);
        }
        
        var aggregate = await _innerRepository.GetByIdAsync(id);
        
        var options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheExpiration);
            
        await _cache.SetStringAsync(
            cacheKey,
            _serializer.Serialize(aggregate),
            options);
            
        return aggregate;
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        await _innerRepository.SaveAsync(aggregate);
        
        // Update cache after saving
        var cacheKey = $"{typeof(TAggregate).Name}:{aggregate.Id}";
        var options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheExpiration);
            
        await _cache.SetStringAsync(
            cacheKey,
            _serializer.Serialize(aggregate),
            options);
    }
}
```

## Scaling Event-Sourced Systems

Strategies for scaling event-sourced systems to handle increased load.

### 1. Read Model Sharding

Shard read models by tenant or other logical boundaries:

```csharp
public class ShardedReadModelDbContext : IReadModelDbContext
{
    private readonly string _connectionStringTemplate;
    private readonly ITenantProvider _tenantProvider;
    private readonly Dictionary<string, ReadModelDbContext> _contextCache = new();
    
    public ShardedReadModelDbContext(
        string connectionStringTemplate,
        ITenantProvider tenantProvider)
    {
        _connectionStringTemplate = connectionStringTemplate;
        _tenantProvider = tenantProvider;
    }
    
    private ReadModelDbContext GetContextForCurrentTenant()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        
        if (!_contextCache.TryGetValue(tenantId, out var context))
        {
            var connectionString = string.Format(_connectionStringTemplate, tenantId);
            var options = new DbContextOptionsBuilder<ReadModelDbContext>()
                .UseSqlServer(connectionString)
                .Options;
                
            context = new ReadModelDbContext(options);
            _contextCache[tenantId] = context;
        }
        
        return context;
    }
    
    public DbSet<AccountSummary> AccountSummaries => 
        GetContextForCurrentTenant().AccountSummaries;
        
    public DbSet<TransactionSummary> TransactionSummaries => 
        GetContextForCurrentTenant().TransactionSummaries;
        
    public DbSet<CustomerSummary> CustomerSummaries => 
        GetContextForCurrentTenant().CustomerSummaries;
        
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await GetContextForCurrentTenant().SaveChangesAsync(cancellationToken);
    }
}
```

### 2. Event Store Clustering

Configure EventStoreDB in a clustered mode for high availability and throughput:

```csharp
public static IStreamStoreConnection CreateClusteredConnection(string[] gossipSeeds)
{
    var settings = ConnectionSettings.Create()
        .SetDefaultUserCredentials(new UserCredentials(
            Environment.GetEnvironmentVariable("EVENTSTORE_USERNAME"),
            Environment.GetEnvironmentVariable("EVENTSTORE_PASSWORD")))
        .SetHeartbeatInterval(TimeSpan.FromSeconds(30))
        .SetHeartbeatTimeout(TimeSpan.FromSeconds(120))
        .Build();
    
    var clusterSettings = ClusterSettings.Create()
        .DiscoverClusterViaGossipSeeds()
        .SetGossipSeedEndPoints(ParseGossipSeeds(gossipSeeds))
        .SetGossipTimeout(TimeSpan.FromSeconds(5))
        .Build();
    
    var connection = EventStoreConnection.Create(settings, clusterSettings);
    connection.ConnectAsync().Wait();
    return new StreamStoreConnection(connection);
}
```

### 3. Projection Scaling

Scale out projections using competing consumers pattern:

```csharp
public class ProjectionWorker : BackgroundService
{
    private readonly IStreamStoreConnection _connection;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ProjectionWorker> _logger;
    private readonly string _workerName;
    private readonly string _subscriptionGroup;
    
    public ProjectionWorker(
        IStreamStoreConnection connection,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProjectionWorker> logger,
        string workerName,
        string subscriptionGroup)
    {
        _connection = connection;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _workerName = workerName;
        _subscriptionGroup = subscriptionGroup;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting projection worker {WorkerName} for group {SubscriptionGroup}",
            _workerName, _subscriptionGroup);
            
        var settings = new PersistentSubscriptionSettings(
            resolveLinkTos: true,
            startFrom: Position.Start,
            messageTimeout: TimeSpan.FromMinutes(1),
            maxRetryCount: 10,
            liveBufferSize: 500,
            readBatchSize: 20,
            historyBufferSize: 500,
            checkPointAfter: TimeSpan.FromSeconds(10),
            minCheckPointCount: 10,
            maxCheckPointCount: 1000,
            maxSubscriberCount: 10,
            namedConsumerStrategy: SystemConsumerStrategies.RoundRobin);
            
        try
        {
            await _connection.CreatePersistentSubscriptionAsync(
                "$all", _subscriptionGroup, settings, stoppingToken);
        }
        catch (InvalidOperationException)
        {
            // Subscription already exists
        }
        
        var subscription = await _connection.ConnectToPersistentSubscriptionAsync(
            "$all",
            _subscriptionGroup,
            (_, evt) => ProcessEvent(evt),
            (_, reason, ex) => HandleSubscriptionDropped(reason, ex),
            _workerName,
            bufferSize: 10,
            autoAck: false);
            
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        
        subscription.Stop();
    }
    
    private Task ProcessEvent(ResolvedEvent evt)
    {
        // Process event...
        return Task.CompletedTask;
    }
    
    private Task HandleSubscriptionDropped(SubscriptionDropReason reason, Exception ex)
    {
        _logger.LogError(ex, "Subscription dropped: {Reason}", reason);
        return Task.CompletedTask;
    }
}
```

## Monitoring and Profiling

Implement comprehensive monitoring to identify and address performance bottlenecks.

### 1. Event Store Metrics

Monitor EventStoreDB performance metrics:

```csharp
public class EventStoreMetricsCollector : BackgroundService
{
    private readonly IStreamStoreConnection _connection;
    private readonly IMetricsPublisher _metricsPublisher;
    private readonly ILogger<EventStoreMetricsCollector> _logger;
    
    public EventStoreMetricsCollector(
        IStreamStoreConnection connection,
        IMetricsPublisher metricsPublisher,
        ILogger<EventStoreMetricsCollector> logger)
    {
        _connection = connection;
        _metricsPublisher = metricsPublisher;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stats = await _connection.GetStatsAsync();
                
                _metricsPublisher.PublishGauge("eventstore.tcp.connections", stats.TcpConnections);
                _metricsPublisher.PublishGauge("eventstore.http.connections", stats.HttpConnections);
                _metricsPublisher.PublishGauge("eventstore.process.cpu", stats.ProcessCpu);
                _metricsPublisher.PublishGauge("eventstore.process.memory", stats.ProcessMemory);
                
                // Publish other relevant metrics
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting EventStore metrics");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### 2. Repository Performance Tracking

Track repository performance using metrics and logging:

```csharp
public class MetricsRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, new()
{
    private readonly IRepository<TAggregate, TId> _innerRepository;
    private readonly IMetricsPublisher _metricsPublisher;
    private readonly ILogger<MetricsRepository<TAggregate, TId>> _logger;
    
    public MetricsRepository(
        IRepository<TAggregate, TId> innerRepository,
        IMetricsPublisher metricsPublisher,
        ILogger<MetricsRepository<TAggregate, TId>> logger)
    {
        _innerRepository = innerRepository;
        _metricsPublisher = metricsPublisher;
        _logger = logger;
    }
    
    public async Task<TAggregate> GetByIdAsync(TId id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var aggregate = await _innerRepository.GetByIdAsync(id);
            
            stopwatch.Stop();
            _metricsPublisher.PublishTimer(
                $"repository.{typeof(TAggregate).Name}.get",
                stopwatch.ElapsedMilliseconds);
                
            _logger.LogDebug(
                "Retrieved {AggregateType} with ID {AggregateId} in {ElapsedMs}ms",
                typeof(TAggregate).Name, id, stopwatch.ElapsedMilliseconds);
                
            return aggregate;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _metricsPublisher.PublishTimer(
                $"repository.{typeof(TAggregate).Name}.get.error",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
    
    public async Task SaveAsync(TAggregate aggregate)
    {
        var stopwatch = Stopwatch.StartNew();
        var uncommittedEvents = aggregate.GetUncommittedEvents().Count();
        
        try
        {
            await _innerRepository.SaveAsync(aggregate);
            
            stopwatch.Stop();
            _metricsPublisher.PublishTimer(
                $"repository.{typeof(TAggregate).Name}.save",
                stopwatch.ElapsedMilliseconds);
                
            _metricsPublisher.PublishCounter(
                $"repository.{typeof(TAggregate).Name}.events",
                uncommittedEvents);
                
            _logger.LogDebug(
                "Saved {AggregateType} with ID {AggregateId} and {EventCount} events in {ElapsedMs}ms",
                typeof(TAggregate).Name, aggregate.Id, uncommittedEvents, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _metricsPublisher.PublishTimer(
                $"repository.{typeof(TAggregate).Name}.save.error",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### 3. Query Performance Monitoring

Monitor query performance to identify slow queries:

```csharp
public class MetricsQueryService : IAccountQueryService
{
    private readonly IAccountQueryService _innerService;
    private readonly IMetricsPublisher _metricsPublisher;
    private readonly ILogger<MetricsQueryService> _logger;
    
    public MetricsQueryService(
        IAccountQueryService innerService,
        IMetricsPublisher metricsPublisher,
        ILogger<MetricsQueryService> logger)
    {
        _innerService = innerService;
        _metricsPublisher = metricsPublisher;
        _logger = logger;
    }
    
    public async Task<AccountSummaryDto> GetAccountByIdAsync(Guid id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _innerService.GetAccountByIdAsync(id);
            
            stopwatch.Stop();
            _metricsPublisher.PublishTimer("query.account.byid", stopwatch.ElapsedMilliseconds);
            
            if (stopwatch.ElapsedMilliseconds > 100)
            {
                _logger.LogWarning(
                    "Slow query: GetAccountByIdAsync took {ElapsedMs}ms for account {AccountId}",
                    stopwatch.ElapsedMilliseconds, id);
            }
            
            return result;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _metricsPublisher.PublishTimer("query.account.byid.error", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
    
    public async Task<IEnumerable<AccountSummaryDto>> GetAllAccountsAsync(int page = 1, int pageSize = 10)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _innerService.GetAllAccountsAsync(page, pageSize);
            
            stopwatch.Stop();
            _metricsPublisher.PublishTimer("query.account.all", stopwatch.ElapsedMilliseconds);
            
            if (stopwatch.ElapsedMilliseconds > 200)
            {
                _logger.LogWarning(
                    "Slow query: GetAllAccountsAsync took {ElapsedMs}ms for page {Page}, size {PageSize}",
                    stopwatch.ElapsedMilliseconds, page, pageSize);
            }
            
            return result;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _metricsPublisher.PublishTimer("query.account.all.error", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

## Best Practices

### 1. Event Design

- Keep events small and focused
- Include only relevant data in events
- Use value objects for complex properties
- Consider versioning strategy for long-lived events

### 2. Aggregate Design

- Keep aggregates small and focused on a single responsibility
- Limit the number of events per aggregate
- Consider splitting large aggregates into smaller ones
- Use snapshots for aggregates with many events

### 3. Read Model Design

- Design read models for specific query patterns
- Denormalize data to avoid joins
- Create appropriate indexes for common queries
- Consider materialized views for complex aggregations

### 4. Caching Strategy

- Cache hot aggregates to reduce event store load
- Use distributed caching for scaled-out applications
- Implement cache invalidation strategies
- Consider read-through and write-through caching

### 5. Scaling Considerations

- Shard read models by tenant or other boundaries
- Use competing consumers for processing projections
- Configure event store clustering for high availability
- Implement backpressure mechanisms for high-volume systems
