# Performance Optimization Guide

[← Back to Table of Contents](README.md)

This guide provides strategies and techniques for optimizing the performance of applications built with Reactive Domain.

## Table of Contents

- [Event Store Performance Considerations](#event-store-performance-considerations)
- [Snapshot Strategies](#snapshot-strategies)
- [Read Model Optimization Techniques](#read-model-optimization-techniques)
- [Message Handling Performance](#message-handling-performance)
- [Scaling Strategies for High-Throughput Systems](#scaling-strategies-for-high-throughput-systems)
- [Monitoring and Profiling Techniques](#monitoring-and-profiling-techniques)
- [Benchmarking and Performance Testing](#benchmarking-and-performance-testing)

## Event Store Performance Considerations

### Hardware Recommendations

EventStoreDB performance is influenced by hardware choices:

1. **CPU**: Multi-core processors for parallel event processing
2. **Memory**: Sufficient RAM for caching frequently accessed events
3. **Storage**: Fast SSDs for low-latency event storage and retrieval
4. **Network**: High-bandwidth, low-latency network for cluster communication

### Configuration Optimization

Optimize EventStoreDB configuration for your workload:

```bash
# Example EventStoreDB configuration
EVENTSTORE_DB="/var/lib/eventstore"
EVENTSTORE_INDEX="/var/lib/eventstore/index"
EVENTSTORE_LOG="/var/log/eventstore"
EVENTSTORE_RUN_PROJECTIONS=All
EVENTSTORE_START_STANDARD_PROJECTIONS=true
EVENTSTORE_DISABLE_HTTP_CACHING=false
EVENTSTORE_DISABLE_ADMIN_UI=false
EVENTSTORE_WORKER_THREADS=10
EVENTSTORE_READER_THREADS_COUNT=5
```

### Stream Naming Strategies

Efficient stream naming improves performance:

```csharp
public class OptimizedStreamNameBuilder : IStreamNameBuilder
{
    public string GenerateForAggregate(Type aggregateType, Guid aggregateId)
    {
        // Use short, consistent names for better performance
        var typeName = aggregateType.Name.ToLowerInvariant();
        return $"agg-{typeName}-{aggregateId}";
    }
}
```

### Batch Operations

Use batch operations to reduce round trips:

```csharp
public void SaveMultipleAggregates(IEnumerable<IEventSource> aggregates)
{
    foreach (var aggregate in aggregates)
    {
        var events = aggregate.TakeEvents();
        var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
        
        _connection.AppendToStream(streamName, aggregate.ExpectedVersion, 
            events.Select(e => _serializer.Serialize(e, Guid.NewGuid())));
    }
}
```

## Snapshot Strategies

### When to Use Snapshots

Snapshots are beneficial when:

1. Aggregates have many events (>100)
2. Aggregates are frequently loaded
3. Event replay is computationally expensive

### Implementing Snapshots

```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    private decimal _balance;
    private List<Transaction> _recentTransactions = new List<Transaction>();
    
    // ... existing code ...
    
    public void RestoreFromSnapshot(object snapshot)
    {
        var accountSnapshot = (AccountSnapshot)snapshot;
        _balance = accountSnapshot.Balance;
        _recentTransactions = accountSnapshot.RecentTransactions;
        ExpectedVersion = accountSnapshot.Version;
    }
    
    public object TakeSnapshot()
    {
        return new AccountSnapshot
        {
            Balance = _balance,
            RecentTransactions = _recentTransactions.ToList(),
            Version = ExpectedVersion
        };
    }
}

public class AccountSnapshot
{
    public decimal Balance { get; set; }
    public List<Transaction> RecentTransactions { get; set; }
    public long Version { get; set; }
}
```

### Snapshot Frequency

Optimize snapshot frequency:

```csharp
public class SnapshotRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly ISnapshotStore _snapshotStore;
    private readonly int _snapshotFrequency;
    
    public SnapshotRepository(
        IRepository innerRepository, 
        ISnapshotStore snapshotStore,
        int snapshotFrequency = 100)
    {
        _innerRepository = innerRepository;
        _snapshotStore = snapshotStore;
        _snapshotFrequency = snapshotFrequency;
    }
    
    public void Save(IEventSource aggregate)
    {
        _innerRepository.Save(aggregate);
        
        // Take a snapshot if the aggregate supports it and has enough events
        if (aggregate is ISnapshotSource snapshotSource && 
            aggregate.ExpectedVersion % _snapshotFrequency == 0)
        {
            var snapshot = snapshotSource.TakeSnapshot();
            _snapshotStore.SaveSnapshot(
                aggregate.Id, 
                aggregate.GetType(), 
                snapshot, 
                aggregate.ExpectedVersion);
        }
    }
    
    // ... other methods ...
}
```

### Snapshot Storage Optimization

Optimize snapshot storage:

1. **Compression**: Compress snapshots to reduce storage requirements
2. **Serialization**: Use efficient serialization formats (e.g., Protocol Buffers)
3. **Storage Tier**: Store snapshots on appropriate storage tiers based on access patterns

## Read Model Optimization Techniques

### Specialized Read Models

Create specialized read models for specific query patterns:

```csharp
public class AccountBalanceReadModel
{
    private readonly Dictionary<Guid, decimal> _balances = new Dictionary<Guid, decimal>();
    
    public void Handle(AccountCreated @event)
    {
        _balances[@event.AccountId] = @event.InitialBalance;
    }
    
    public void Handle(AmountDeposited @event)
    {
        _balances[@event.AccountId] += @event.Amount;
    }
    
    public void Handle(AmountWithdrawn @event)
    {
        _balances[@event.AccountId] -= @event.Amount;
    }
    
    public decimal GetBalance(Guid accountId)
    {
        return _balances.TryGetValue(accountId, out var balance) ? balance : 0;
    }
}
```

### Denormalization Strategies

Denormalize data for efficient queries:

```csharp
public class AccountSummaryReadModel
{
    private readonly Dictionary<Guid, AccountSummary> _summaries = new Dictionary<Guid, AccountSummary>();
    
    public void Handle(AccountCreated @event)
    {
        _summaries[@event.AccountId] = new AccountSummary
        {
            Id = @event.AccountId,
            Owner = @event.Owner,
            Balance = @event.InitialBalance,
            TransactionCount = 0,
            LastActivity = DateTime.UtcNow
        };
    }
    
    public void Handle(AmountDeposited @event)
    {
        var summary = _summaries[@event.AccountId];
        summary.Balance += @event.Amount;
        summary.TransactionCount++;
        summary.LastActivity = DateTime.UtcNow;
    }
    
    // ... other handlers ...
    
    public AccountSummary GetSummary(Guid accountId)
    {
        return _summaries.TryGetValue(accountId, out var summary) ? summary : null;
    }
    
    public IEnumerable<AccountSummary> GetRecentlyActiveAccounts(int count)
    {
        return _summaries.Values
            .OrderByDescending(s => s.LastActivity)
            .Take(count);
    }
}

public class AccountSummary
{
    public Guid Id { get; set; }
    public string Owner { get; set; }
    public decimal Balance { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastActivity { get; set; }
}
```

### Database Optimization

Optimize database storage for read models:

1. **Indexing**: Create appropriate indexes for query patterns
2. **Partitioning**: Partition large tables for better performance
3. **Caching**: Implement caching for frequently accessed data

```csharp
public class CachedAccountReadModel : IDisposable
{
    private readonly IAccountReadModel _innerReadModel;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;
    
    public CachedAccountReadModel(
        IAccountReadModel innerReadModel,
        IMemoryCache cache,
        TimeSpan? cacheDuration = null)
    {
        _innerReadModel = innerReadModel;
        _cache = cache;
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5);
    }
    
    public AccountSummary GetSummary(Guid accountId)
    {
        var cacheKey = $"account-summary-{accountId}";
        
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = _cacheDuration;
            return _innerReadModel.GetSummary(accountId);
        });
    }
    
    public void Handle(AccountCreated @event)
    {
        _innerReadModel.Handle(@event);
        InvalidateCache(@event.AccountId);
    }
    
    public void Handle(AmountDeposited @event)
    {
        _innerReadModel.Handle(@event);
        InvalidateCache(@event.AccountId);
    }
    
    // ... other handlers ...
    
    private void InvalidateCache(Guid accountId)
    {
        _cache.Remove($"account-summary-{accountId}");
    }
    
    public void Dispose()
    {
        (_innerReadModel as IDisposable)?.Dispose();
    }
}
```

## Message Handling Performance

### Command Batching

Batch related commands for efficiency:

```csharp
public class BatchingCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly ConcurrentQueue<ICommand> _commandQueue = new ConcurrentQueue<ICommand>();
    private readonly int _batchSize;
    private readonly Timer _batchTimer;
    
    public BatchingCommandBus(ICommandBus innerBus, int batchSize = 100, int batchIntervalMs = 100)
    {
        _innerBus = innerBus;
        _batchSize = batchSize;
        _batchTimer = new Timer(ProcessBatch, null, batchIntervalMs, batchIntervalMs);
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        _commandQueue.Enqueue(command);
        
        // Process immediately if queue exceeds batch size
        if (_commandQueue.Count >= _batchSize)
        {
            ProcessBatch(null);
        }
    }
    
    private void ProcessBatch(object state)
    {
        var commands = new List<ICommand>();
        
        // Dequeue up to batch size commands
        while (commands.Count < _batchSize && _commandQueue.TryDequeue(out var command))
        {
            commands.Add(command);
        }
        
        // Process commands in batch
        foreach (var command in commands)
        {
            _innerBus.Send(command);
        }
    }
    
    public void Dispose()
    {
        _batchTimer.Dispose();
    }
}
```

### Parallel Processing

Process commands and events in parallel:

```csharp
public class ParallelEventProcessor<TEvent> : IEventHandler<TEvent>
{
    private readonly IEventHandler<TEvent> _innerHandler;
    private readonly ParallelOptions _parallelOptions;
    
    public ParallelEventProcessor(
        IEventHandler<TEvent> innerHandler,
        int maxDegreeOfParallelism = -1)
    {
        _innerHandler = innerHandler;
        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };
    }
    
    public void Handle(IEnumerable<TEvent> events)
    {
        Parallel.ForEach(events, _parallelOptions, @event =>
        {
            _innerHandler.Handle(@event);
        });
    }
    
    public void Handle(TEvent @event)
    {
        _innerHandler.Handle(@event);
    }
}
```

### Asynchronous Processing

Use asynchronous processing for non-blocking operations:

```csharp
public class AsyncCommandBus : IAsyncCommandBus
{
    private readonly ConcurrentDictionary<Type, Func<ICommand, Task>> _handlers = 
        new ConcurrentDictionary<Type, Func<ICommand, Task>>();
    
    public void RegisterHandler<TCommand>(Func<TCommand, Task> handler) 
        where TCommand : class, ICommand
    {
        _handlers[typeof(TCommand)] = cmd => handler((TCommand)cmd);
    }
    
    public async Task SendAsync<TCommand>(TCommand command) 
        where TCommand : class, ICommand
    {
        if (_handlers.TryGetValue(typeof(TCommand), out var handler))
        {
            await handler(command);
        }
        else
        {
            throw new InvalidOperationException($"No handler registered for {typeof(TCommand).Name}");
        }
    }
}
```

## Scaling Strategies for High-Throughput Systems

### Horizontal Scaling

Scale out command and query processors:

1. **Load Balancing**: Distribute commands across multiple processors
2. **Stateless Design**: Design processors to be stateless for easy scaling
3. **Consistent Hashing**: Route related commands to the same processor

### Event Partitioning

Partition events for parallel processing:

```csharp
public class PartitionedEventProcessor : IEventProcessor
{
    private readonly IEventProcessor[] _processors;
    private readonly IPartitionStrategy _partitionStrategy;
    
    public PartitionedEventProcessor(
        IEnumerable<IEventProcessor> processors,
        IPartitionStrategy partitionStrategy)
    {
        _processors = processors.ToArray();
        _partitionStrategy = partitionStrategy;
    }
    
    public void Process(object @event)
    {
        var partition = _partitionStrategy.GetPartition(@event, _processors.Length);
        _processors[partition].Process(@event);
    }
}

public interface IPartitionStrategy
{
    int GetPartition(object @event, int partitionCount);
}

public class AggregateIdPartitionStrategy : IPartitionStrategy
{
    public int GetPartition(object @event, int partitionCount)
    {
        // Extract aggregate ID from event
        var aggregateId = ExtractAggregateId(@event);
        
        // Use consistent hashing
        return Math.Abs(aggregateId.GetHashCode()) % partitionCount;
    }
    
    private Guid ExtractAggregateId(object @event)
    {
        // Extract aggregate ID based on event type
        var property = @event.GetType().GetProperties()
            .FirstOrDefault(p => 
                p.Name == "AggregateId" || 
                p.Name == "Id" || 
                p.Name.EndsWith("Id"));
                
        return property != null 
            ? (Guid)property.GetValue(@event) 
            : Guid.Empty;
    }
}
```

### Competing Consumers

Implement competing consumers for event processing:

```csharp
public class CompetingConsumerManager
{
    private readonly IStreamStoreConnection _connection;
    private readonly IEventProcessor _processor;
    private readonly string _consumerGroup;
    private readonly int _consumerCount;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private readonly List<Task> _consumerTasks = new List<Task>();
    
    public CompetingConsumerManager(
        IStreamStoreConnection connection,
        IEventProcessor processor,
        string consumerGroup,
        int consumerCount = 4)
    {
        _connection = connection;
        _processor = processor;
        _consumerGroup = consumerGroup;
        _consumerCount = consumerCount;
    }
    
    public void Start()
    {
        for (int i = 0; i < _consumerCount; i++)
        {
            var consumerId = $"{_consumerGroup}-{i}";
            _consumerTasks.Add(Task.Run(() => RunConsumer(consumerId, _cancellationTokenSource.Token)));
        }
    }
    
    public async Task StopAsync()
    {
        _cancellationTokenSource.Cancel();
        await Task.WhenAll(_consumerTasks);
    }
    
    private async Task RunConsumer(string consumerId, CancellationToken cancellationToken)
    {
        // Connect to persistent subscription
        var subscription = _connection.ConnectToPersistentSubscription(
            "$all",
            _consumerGroup,
            (subscription, @event) =>
            {
                try
                {
                    // Process the event
                    _processor.Process(@event);
                    
                    // Acknowledge successful processing
                    subscription.Acknowledge(@event);
                }
                catch (Exception ex)
                {
                    // Nack the event for retry
                    subscription.Fail(@event, PersistentSubscriptionNakEventAction.Retry, ex.Message);
                }
            });
            
        // Wait for cancellation
        await Task.Delay(-1, cancellationToken);
        
        // Disconnect on cancellation
        subscription.Dispose();
    }
}
```

## Monitoring and Profiling Techniques

### Performance Metrics

Track key performance metrics:

```csharp
public class MetricsCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly IMetrics _metrics;
    
    public MetricsCommandBus(ICommandBus innerBus, IMetrics metrics)
    {
        _innerBus = innerBus;
        _metrics = metrics;
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        var commandType = typeof(TCommand).Name;
        
        // Track command count
        _metrics.IncrementCounter($"commands.{commandType}.count");
        
        // Measure command processing time
        using (_metrics.MeasureDuration($"commands.{commandType}.duration"))
        {
            try
            {
                _innerBus.Send(command);
                
                // Track successful commands
                _metrics.IncrementCounter($"commands.{commandType}.success");
            }
            catch
            {
                // Track failed commands
                _metrics.IncrementCounter($"commands.{commandType}.failure");
                throw;
            }
        }
    }
}

public interface IMetrics
{
    void IncrementCounter(string name, long value = 1);
    IDisposable MeasureDuration(string name);
}
```

### Profiling

Profile application performance:

1. **Application Profiling**: Use tools like dotTrace or Visual Studio Profiler
2. **Database Profiling**: Monitor database performance with query analyzers
3. **Event Store Profiling**: Monitor EventStoreDB performance metrics

### Logging Performance Data

Log performance data for analysis:

```csharp
public class PerformanceLoggingRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly ILogger<PerformanceLoggingRepository> _logger;
    
    public PerformanceLoggingRepository(
        IRepository innerRepository,
        ILogger<PerformanceLoggingRepository> logger)
    {
        _innerRepository = innerRepository;
        _logger = logger;
    }
    
    public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) 
        where TAggregate : class, IEventSource
    {
        var stopwatch = Stopwatch.StartNew();
        var result = _innerRepository.TryGetById(id, out aggregate, version);
        stopwatch.Stop();
        
        _logger.LogInformation(
            "Repository.GetById<{AggregateType}>({AggregateId}) took {ElapsedMs}ms",
            typeof(TAggregate).Name,
            id,
            stopwatch.ElapsedMilliseconds);
            
        return result;
    }
    
    public void Save(IEventSource aggregate)
    {
        var stopwatch = Stopwatch.StartNew();
        var eventCount = aggregate.TakeEvents().Length;
        
        _innerRepository.Save(aggregate);
        
        stopwatch.Stop();
        
        _logger.LogInformation(
            "Repository.Save<{AggregateType}>({AggregateId}) with {EventCount} events took {ElapsedMs}ms",
            aggregate.GetType().Name,
            aggregate.Id,
            eventCount,
            stopwatch.ElapsedMilliseconds);
    }
    
    // ... other methods ...
}
```

## Benchmarking and Performance Testing

### Benchmark Framework

Create a benchmark framework for performance testing:

```csharp
public class RepositoryBenchmark
{
    private readonly IRepository _repository;
    private readonly int _iterations;
    
    public RepositoryBenchmark(IRepository repository, int iterations = 1000)
    {
        _repository = repository;
        _iterations = iterations;
    }
    
    public BenchmarkResult RunSaveBenchmark<TAggregate>() 
        where TAggregate : AggregateRoot, new()
    {
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < _iterations; i++)
        {
            var aggregate = new TAggregate();
            
            // Generate some events
            typeof(TAggregate).GetMethod("Initialize")?.Invoke(aggregate, new object[] { Guid.NewGuid().ToString() });
            
            // Save the aggregate
            _repository.Save(aggregate);
        }
        
        stopwatch.Stop();
        
        return new BenchmarkResult
        {
            OperationName = $"Save<{typeof(TAggregate).Name}>",
            Iterations = _iterations,
            TotalTimeMs = stopwatch.ElapsedMilliseconds,
            AverageTimeMs = (double)stopwatch.ElapsedMilliseconds / _iterations
        };
    }
    
    public BenchmarkResult RunGetByIdBenchmark<TAggregate>(IEnumerable<Guid> ids) 
        where TAggregate : class, IEventSource, new()
    {
        var idArray = ids.ToArray();
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < _iterations; i++)
        {
            var id = idArray[i % idArray.Length];
            _repository.TryGetById<TAggregate>(id, out var aggregate);
        }
        
        stopwatch.Stop();
        
        return new BenchmarkResult
        {
            OperationName = $"GetById<{typeof(TAggregate).Name}>",
            Iterations = _iterations,
            TotalTimeMs = stopwatch.ElapsedMilliseconds,
            AverageTimeMs = (double)stopwatch.ElapsedMilliseconds / _iterations
        };
    }
}

public class BenchmarkResult
{
    public string OperationName { get; set; }
    public int Iterations { get; set; }
    public long TotalTimeMs { get; set; }
    public double AverageTimeMs { get; set; }
    
    public override string ToString()
    {
        return $"{OperationName}: {Iterations} iterations, {TotalTimeMs}ms total, {AverageTimeMs:F2}ms average";
    }
}
```

### Load Testing

Implement load testing for performance validation:

```csharp
public class CommandLoadTest
{
    private readonly ICommandBus _commandBus;
    private readonly int _commandCount;
    private readonly int _concurrencyLevel;
    
    public CommandLoadTest(
        ICommandBus commandBus,
        int commandCount = 10000,
        int concurrencyLevel = 8)
    {
        _commandBus = commandBus;
        _commandCount = commandCount;
        _concurrencyLevel = concurrencyLevel;
    }
    
    public async Task<LoadTestResult> RunAsync<TCommand>(Func<int, TCommand> commandFactory) 
        where TCommand : class, ICommand
    {
        var stopwatch = Stopwatch.StartNew();
        var commandsPerTask = _commandCount / _concurrencyLevel;
        var tasks = new List<Task>();
        
        for (int i = 0; i < _concurrencyLevel; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < commandsPerTask; j++)
                {
                    var commandIndex = (taskId * commandsPerTask) + j;
                    var command = commandFactory(commandIndex);
                    _commandBus.Send(command);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        return new LoadTestResult
        {
            TestName = $"CommandLoadTest<{typeof(TCommand).Name}>",
            CommandCount = _commandCount,
            ConcurrencyLevel = _concurrencyLevel,
            TotalTimeMs = stopwatch.ElapsedMilliseconds,
            CommandsPerSecond = (double)_commandCount / stopwatch.ElapsedMilliseconds * 1000
        };
    }
}

public class LoadTestResult
{
    public string TestName { get; set; }
    public int CommandCount { get; set; }
    public int ConcurrencyLevel { get; set; }
    public long TotalTimeMs { get; set; }
    public double CommandsPerSecond { get; set; }
    
    public override string ToString()
    {
        return $"{TestName}: {CommandCount} commands, {ConcurrencyLevel} threads, {TotalTimeMs}ms total, {CommandsPerSecond:F2} commands/sec";
    }
}
```

### Performance Regression Testing

Implement performance regression testing:

1. **Baseline Measurements**: Establish performance baselines
2. **Automated Testing**: Automate performance tests in CI/CD pipeline
3. **Regression Detection**: Compare results against baselines to detect regressions

```csharp
public class PerformanceRegressionTest
{
    private readonly string _baselineFilePath;
    private readonly double _regressionThreshold;
    
    public PerformanceRegressionTest(
        string baselineFilePath = "performance-baseline.json",
        double regressionThreshold = 0.1)
    {
        _baselineFilePath = baselineFilePath;
        _regressionThreshold = regressionThreshold;
    }
    
    public void SaveBaseline(Dictionary<string, double> metrics)
    {
        File.WriteAllText(_baselineFilePath, JsonConvert.SerializeObject(metrics, Formatting.Indented));
    }
    
    public Dictionary<string, PerformanceComparison> CompareWithBaseline(Dictionary<string, double> currentMetrics)
    {
        var baseline = File.Exists(_baselineFilePath)
            ? JsonConvert.DeserializeObject<Dictionary<string, double>>(File.ReadAllText(_baselineFilePath))
            : new Dictionary<string, double>();
            
        var comparisons = new Dictionary<string, PerformanceComparison>();
        
        foreach (var metric in currentMetrics)
        {
            if (baseline.TryGetValue(metric.Key, out var baselineValue))
            {
                var percentChange = (metric.Value - baselineValue) / baselineValue;
                var isRegression = percentChange > _regressionThreshold;
                
                comparisons[metric.Key] = new PerformanceComparison
                {
                    MetricName = metric.Key,
                    BaselineValue = baselineValue,
                    CurrentValue = metric.Value,
                    PercentChange = percentChange,
                    IsRegression = isRegression
                };
            }
            else
            {
                comparisons[metric.Key] = new PerformanceComparison
                {
                    MetricName = metric.Key,
                    BaselineValue = null,
                    CurrentValue = metric.Value,
                    PercentChange = null,
                    IsRegression = false
                };
            }
        }
        
        return comparisons;
    }
}

public class PerformanceComparison
{
    public string MetricName { get; set; }
    public double? BaselineValue { get; set; }
    public double CurrentValue { get; set; }
    public double? PercentChange { get; set; }
    public bool IsRegression { get; set; }
    
    public override string ToString()
    {
        if (BaselineValue.HasValue)
        {
            var changeDirection = PercentChange > 0 ? "slower" : "faster";
            var changePercent = Math.Abs(PercentChange.Value) * 100;
            return $"{MetricName}: {CurrentValue:F2} ({changePercent:F2}% {changeDirection} than baseline {BaselineValue:F2})";
        }
        else
        {
            return $"{MetricName}: {CurrentValue:F2} (no baseline)";
        }
    }
}
```

[↑ Back to Top](#performance-optimization-guide) | [← Back to Table of Contents](README.md)
