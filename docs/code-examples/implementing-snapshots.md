# Implementing Snapshots

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to implement snapshots in Reactive Domain to improve the performance of loading aggregates with long event histories.

## Snapshot Interface

```csharp
using System;

namespace ReactiveDomain.Foundation
{
    public interface ISnapshot
    {
        Guid AggregateId { get; }
        long Version { get; }
    }
}
```

## Snapshot Base Class

```csharp
using System;

namespace ReactiveDomain.Foundation
{
    public abstract class Snapshot : ISnapshot
    {
        public Guid AggregateId { get; }
        public long Version { get; }
        
        protected Snapshot(Guid aggregateId, long version)
        {
            AggregateId = aggregateId;
            Version = version;
        }
    }
}
```

## Account Snapshot

```csharp
using System;
using ReactiveDomain.Foundation;

namespace MyApp.Domain.Snapshots
{
    [Serializable]
    public class AccountSnapshot : Snapshot
    {
        public string AccountNumber { get; }
        public string CustomerName { get; }
        public decimal Balance { get; }
        public bool IsClosed { get; }
        
        public AccountSnapshot(
            Guid aggregateId,
            long version,
            string accountNumber,
            string customerName,
            decimal balance,
            bool isClosed)
            : base(aggregateId, version)
        {
            AccountNumber = accountNumber;
            CustomerName = customerName;
            Balance = balance;
            IsClosed = isClosed;
        }
    }
}
```

## Snapshotable Aggregate Root

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain.Events;
using MyApp.Domain.Snapshots;

namespace MyApp.Domain
{
    public class Account : AggregateRoot, ISnapshotable
    {
        private string _accountNumber;
        private string _customerName;
        private decimal _balance;
        private bool _isClosed;
        
        // Default constructor for creating new aggregates
        public Account(Guid id) : base(id)
        {
        }
        
        // Constructor with correlation source
        public Account(Guid id, ICorrelatedMessage source) : base(id, source)
        {
        }
        
        // Constructor for restoring from snapshot
        public Account(Guid id, AccountSnapshot snapshot) : base(id)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
                
            _accountNumber = snapshot.AccountNumber;
            _customerName = snapshot.CustomerName;
            _balance = snapshot.Balance;
            _isClosed = snapshot.IsClosed;
            
            Version = snapshot.Version;
        }
        
        public void Create(string accountNumber, string customerName)
        {
            if (string.IsNullOrEmpty(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrEmpty(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            ApplyChange(new AccountCreated(Id, accountNumber, customerName, CorrelationId, CausationId));
        }
        
        public void Deposit(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            if (_isClosed)
                throw new InvalidOperationException("Cannot deposit to a closed account");
                
            ApplyChange(new FundsDeposited(Id, amount, CorrelationId, CausationId));
        }
        
        public void Withdraw(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            if (_isClosed)
                throw new InvalidOperationException("Cannot withdraw from a closed account");
                
            if (_balance < amount)
                throw new InvalidOperationException("Insufficient funds");
                
            ApplyChange(new FundsWithdrawn(Id, amount, CorrelationId, CausationId));
        }
        
        public void Close()
        {
            if (_isClosed)
                throw new InvalidOperationException("Account is already closed");
                
            ApplyChange(new AccountClosed(Id, CorrelationId, CausationId));
        }
        
        public decimal GetBalance()
        {
            return _balance;
        }
        
        // Event handlers
        private void Apply(AccountCreated @event)
        {
            _accountNumber = @event.AccountNumber;
            _customerName = @event.CustomerName;
            _balance = 0;
            _isClosed = false;
        }
        
        private void Apply(FundsDeposited @event)
        {
            _balance += @event.Amount;
        }
        
        private void Apply(FundsWithdrawn @event)
        {
            _balance -= @event.Amount;
        }
        
        private void Apply(AccountClosed @event)
        {
            _isClosed = true;
        }
        
        // ISnapshotable implementation
        public ISnapshot CreateSnapshot()
        {
            return new AccountSnapshot(
                Id,
                Version,
                _accountNumber,
                _customerName,
                _balance,
                _isClosed);
        }
        
        public void RestoreFromSnapshot(ISnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
                
            if (!(snapshot is AccountSnapshot accountSnapshot))
                throw new ArgumentException("Invalid snapshot type", nameof(snapshot));
                
            _accountNumber = accountSnapshot.AccountNumber;
            _customerName = accountSnapshot.CustomerName;
            _balance = accountSnapshot.Balance;
            _isClosed = accountSnapshot.IsClosed;
            
            Version = snapshot.Version;
        }
    }
}
```

## Snapshot Store Interface

```csharp
using System;
using System.Threading.Tasks;
using ReactiveDomain.Foundation;

namespace ReactiveDomain.Foundation
{
    public interface ISnapshotStore
    {
        Task<ISnapshot> GetLatestSnapshotAsync(Guid aggregateId, Type aggregateType);
        Task SaveSnapshotAsync(ISnapshot snapshot, Type aggregateType);
    }
}
```

## In-Memory Snapshot Store

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReactiveDomain.Foundation;

namespace MyApp.Infrastructure
{
    public class InMemorySnapshotStore : ISnapshotStore
    {
        private readonly Dictionary<string, List<ISnapshot>> _snapshots = 
            new Dictionary<string, List<ISnapshot>>();
            
        public Task<ISnapshot> GetLatestSnapshotAsync(Guid aggregateId, Type aggregateType)
        {
            var key = GetKey(aggregateId, aggregateType);
            
            if (_snapshots.TryGetValue(key, out var snapshotList) && snapshotList.Any())
            {
                return Task.FromResult(snapshotList.OrderByDescending(s => s.Version).First());
            }
            
            return Task.FromResult<ISnapshot>(null);
        }
        
        public Task SaveSnapshotAsync(ISnapshot snapshot, Type aggregateType)
        {
            var key = GetKey(snapshot.AggregateId, aggregateType);
            
            if (!_snapshots.ContainsKey(key))
            {
                _snapshots[key] = new List<ISnapshot>();
            }
            
            _snapshots[key].Add(snapshot);
            
            return Task.CompletedTask;
        }
        
        private string GetKey(Guid aggregateId, Type aggregateType)
        {
            return $"{aggregateType.FullName}-{aggregateId}";
        }
    }
}
```

## SQL Snapshot Store

```csharp
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Dapper;
using ReactiveDomain.Foundation;

namespace MyApp.Infrastructure
{
    public class SqlSnapshotStore : ISnapshotStore
    {
        private readonly string _connectionString;
        
        public SqlSnapshotStore(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }
        
        public async Task<ISnapshot> GetLatestSnapshotAsync(Guid aggregateId, Type aggregateType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                var sql = @"
                    SELECT TOP 1 Data
                    FROM Snapshots
                    WHERE AggregateId = @AggregateId
                    AND AggregateType = @AggregateType
                    ORDER BY Version DESC";
                    
                var result = await connection.QueryFirstOrDefaultAsync<byte[]>(sql, new
                {
                    AggregateId = aggregateId,
                    AggregateType = aggregateType.FullName
                });
                
                if (result == null)
                {
                    return null;
                }
                
                return DeserializeSnapshot(result);
            }
        }
        
        public async Task SaveSnapshotAsync(ISnapshot snapshot, Type aggregateType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                var sql = @"
                    INSERT INTO Snapshots (AggregateId, AggregateType, Version, Data, CreatedAt)
                    VALUES (@AggregateId, @AggregateType, @Version, @Data, @CreatedAt)";
                    
                await connection.ExecuteAsync(sql, new
                {
                    AggregateId = snapshot.AggregateId,
                    AggregateType = aggregateType.FullName,
                    Version = snapshot.Version,
                    Data = SerializeSnapshot(snapshot),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        
        private byte[] SerializeSnapshot(ISnapshot snapshot)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, snapshot);
                return stream.ToArray();
            }
        }
        
        private ISnapshot DeserializeSnapshot(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                var formatter = new BinaryFormatter();
                return (ISnapshot)formatter.Deserialize(stream);
            }
        }
    }
}
```

## Snapshot Repository

```csharp
using System;
using System.Threading.Tasks;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;

namespace MyApp.Infrastructure
{
    public class SnapshotRepository : IRepository
    {
        private readonly IRepository _repository;
        private readonly ISnapshotStore _snapshotStore;
        private readonly int _snapshotFrequency;
        
        public SnapshotRepository(
            IRepository repository,
            ISnapshotStore snapshotStore,
            int snapshotFrequency = 100)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
            _snapshotFrequency = snapshotFrequency;
        }
        
        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource
        {
            // Try to get the latest snapshot
            var snapshot = _snapshotStore.GetLatestSnapshotAsync(id, typeof(TAggregate)).Result;
            
            if (snapshot != null)
            {
                // Create the aggregate from the snapshot
                var aggregate = CreateFromSnapshot<TAggregate>(id, snapshot);
                
                // Get events after the snapshot version
                var events = _repository.GetEventsAfterVersion<TAggregate>(id, snapshot.Version);
                
                // Apply the events
                if (events != null && events.Length > 0)
                {
                    aggregate.RestoreFromEvents(events);
                }
                
                return aggregate;
            }
            
            // No snapshot found, load the aggregate normally
            return _repository.GetById<TAggregate>(id);
        }
        
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource
        {
            try
            {
                aggregate = GetById<TAggregate>(id);
                return true;
            }
            catch (AggregateNotFoundException)
            {
                aggregate = null;
                return false;
            }
        }
        
        public void Save(IEventSource aggregate)
        {
            // Save the aggregate
            _repository.Save(aggregate);
            
            // Check if we should create a snapshot
            if (ShouldCreateSnapshot(aggregate))
            {
                CreateSnapshot(aggregate);
            }
        }
        
        public void Update<TAggregate>(ref TAggregate aggregate) where TAggregate : class, IEventSource
        {
            _repository.Update(ref aggregate);
        }
        
        public void Delete(IEventSource aggregate)
        {
            _repository.Delete(aggregate);
        }
        
        public void HardDelete(IEventSource aggregate)
        {
            _repository.HardDelete(aggregate);
        }
        
        private bool ShouldCreateSnapshot(IEventSource aggregate)
        {
            // Only create snapshots for snapshotable aggregates
            if (!(aggregate is ISnapshotable))
            {
                return false;
            }
            
            // Create a snapshot every _snapshotFrequency events
            return aggregate.Version % _snapshotFrequency == 0;
        }
        
        private void CreateSnapshot(IEventSource aggregate)
        {
            if (aggregate is ISnapshotable snapshotable)
            {
                var snapshot = snapshotable.CreateSnapshot();
                _snapshotStore.SaveSnapshotAsync(snapshot, aggregate.GetType()).Wait();
            }
        }
        
        private TAggregate CreateFromSnapshot<TAggregate>(Guid id, ISnapshot snapshot) where TAggregate : class, IEventSource
        {
            // Create the aggregate using reflection
            var aggregateType = typeof(TAggregate);
            
            // Try to find a constructor that takes (Guid, ISnapshot)
            var constructor = aggregateType.GetConstructor(new[] { typeof(Guid), snapshot.GetType() });
            
            if (constructor != null)
            {
                return (TAggregate)constructor.Invoke(new object[] { id, snapshot });
            }
            
            // Try to create the aggregate and restore from snapshot
            var aggregate = (TAggregate)Activator.CreateInstance(aggregateType, id);
            
            if (aggregate is ISnapshotable snapshotable)
            {
                snapshotable.RestoreFromSnapshot(snapshot);
                return aggregate;
            }
            
            throw new InvalidOperationException($"Aggregate type {aggregateType.Name} does not support snapshots");
        }
    }
}
```

## Snapshot Configuration

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Persistence;
using ReactiveDomain.EventStore;
using MyApp.Infrastructure;

namespace MyApp.Infrastructure
{
    public class SnapshotConfiguration
    {
        public IRepository ConfigureSnapshotRepository(string connectionString)
        {
            // Create a stream name builder
            var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("MyApp");
            
            // Create an event store connection
            var connectionSettings = ConnectionSettings.Create()
                .KeepReconnecting()
                .KeepRetrying()
                .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"));
                
            var connection = new StreamStoreConnection(
                "MyApp",
                connectionSettings,
                connectionString,
                1113);
                
            // Create a serializer
            var serializer = new JsonMessageSerializer();
            
            // Create a base repository
            var baseRepository = new StreamStoreRepository(
                streamNameBuilder,
                connection,
                serializer);
                
            // Create a snapshot store
            var snapshotStore = new InMemorySnapshotStore();
            
            // Create a snapshot repository
            var snapshotRepository = new SnapshotRepository(
                baseRepository,
                snapshotStore,
                100); // Create a snapshot every 100 events
                
            return snapshotRepository;
        }
    }
}
```

## Complete Example

```csharp
using System;
using System.Diagnostics;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.Domain.Snapshots;
using MyApp.Infrastructure;

namespace MyApp.Examples
{
    public class SnapshotExample
    {
        public void DemonstrateSnapshots()
        {
            // Configure repositories
            var config = new SnapshotConfiguration();
            var snapshotRepository = config.ConfigureSnapshotRepository("localhost");
            
            // Create a new account
            var accountId = Guid.NewGuid();
            var account = new Account(accountId);
            account.Create("ACC-123", "John Doe");
            
            // Save the account
            snapshotRepository.Save(account);
            
            // Perform many operations to trigger snapshots
            for (int i = 0; i < 500; i++)
            {
                account.Deposit(100);
                snapshotRepository.Save(account);
                
                account.Withdraw(50);
                snapshotRepository.Save(account);
            }
            
            Console.WriteLine($"Account version: {account.Version}");
            
            // Measure time to load without snapshots
            var baseRepository = GetBaseRepository();
            var stopwatch = Stopwatch.StartNew();
            var accountWithoutSnapshot = baseRepository.GetById<Account>(accountId);
            stopwatch.Stop();
            
            Console.WriteLine($"Time to load without snapshots: {stopwatch.ElapsedMilliseconds}ms");
            
            // Measure time to load with snapshots
            stopwatch.Restart();
            var accountWithSnapshot = snapshotRepository.GetById<Account>(accountId);
            stopwatch.Stop();
            
            Console.WriteLine($"Time to load with snapshots: {stopwatch.ElapsedMilliseconds}ms");
            
            // Verify both accounts are in the same state
            Console.WriteLine($"Account balance without snapshot: {accountWithoutSnapshot.GetBalance()}");
            Console.WriteLine($"Account balance with snapshot: {accountWithSnapshot.GetBalance()}");
            Console.WriteLine($"States match: {accountWithoutSnapshot.GetBalance() == accountWithSnapshot.GetBalance()}");
        }
        
        private IRepository GetBaseRepository()
        {
            // This is a simplified version just for the example
            // In a real application, you would get this from your DI container
            var config = new RepositoryConfiguration();
            return config.ConfigureRepository("localhost");
        }
    }
}
```

## Key Concepts

### Snapshots

- Snapshots are point-in-time captures of aggregate state
- They reduce the number of events that need to be loaded and replayed
- They improve performance for aggregates with long event histories
- They are an optimization technique, not a primary storage mechanism

### ISnapshotable Interface

- Aggregates that support snapshots implement the `ISnapshotable` interface
- They provide methods to create snapshots and restore from snapshots
- They maintain their internal state in a way that can be captured in a snapshot

### Snapshot Store

- Stores and retrieves snapshots
- Can be implemented using various storage technologies
- Provides a consistent interface for snapshot operations

### Snapshot Repository

- Wraps a standard repository
- Tries to load the latest snapshot before loading events
- Only loads events that occurred after the snapshot
- Creates new snapshots at specified intervals

## Best Practices

1. **Snapshot Frequency**: Create snapshots at appropriate intervals (e.g., every 100 events)
2. **Serializable State**: Ensure all aggregate state can be serialized in snapshots
3. **Versioning**: Include version information in snapshots for optimistic concurrency
4. **Snapshot Pruning**: Implement a strategy to remove old snapshots
5. **Error Handling**: Handle snapshot loading failures gracefully
6. **Testing**: Test that aggregates can be correctly restored from snapshots

## Common Pitfalls

1. **Non-Serializable State**: Including non-serializable objects in aggregate state
2. **Snapshot Overhead**: Creating snapshots too frequently
3. **Missing Events**: Not loading events that occurred after the snapshot
4. **Versioning Issues**: Not handling snapshot version compatibility
5. **Snapshot Dependency**: Relying on snapshots for correctness rather than just performance

---

**Navigation**:
- [← Previous: Handling Correlation and Causation](correlation-causation.md)
- [↑ Back to Top](#implementing-snapshots)
- [→ Next: Testing Aggregates and Event Handlers](testing.md)
