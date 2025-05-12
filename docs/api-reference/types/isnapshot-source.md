# ISnapshotSource

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ISnapshotSource` is an interface in Reactive Domain that extends the base `IEventSource` interface to add snapshot capabilities to event-sourced entities.

## Overview

In event-sourced systems, entities reconstruct their state by replaying all historical events. As the number of events grows, this process can become time-consuming. The `ISnapshotSource` interface provides a mechanism for creating and restoring from snapshots, which are point-in-time captures of an entity's state. This significantly improves loading performance for entities with long event histories.

Snapshots are not a replacement for events but rather an optimization technique. The complete event history is still maintained, but snapshots allow entities to be restored more efficiently by loading the most recent snapshot and then applying only the events that occurred after the snapshot was taken.

## Interface Definition

```csharp
public interface ISnapshotSource : IEventSource
{
    object CreateSnapshot();
    void RestoreFromSnapshot(object snapshot);
    long SnapshotVersion { get; set; }
}
```

## Key Features

- **Performance Optimization**: Significantly reduces loading time for entities with long event histories
- **Snapshot Creation**: Provides a mechanism for capturing the current state of an entity
- **Snapshot Restoration**: Enables restoring an entity's state from a snapshot
- **Version Tracking**: Tracks the version at which a snapshot was taken
- **Event Sourcing**: Maintains the full event history alongside snapshots

## Usage

### Implementing the Interface

Here's an example of implementing the `ISnapshotSource` interface in an aggregate:

```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    private decimal _balance;
    private bool _isActive;
    private string _accountNumber;
    private string _customerName;
    private List<Transaction> _recentTransactions;
    
    public long SnapshotVersion { get; set; }
    
    public Account(Guid id) : base(id)
    {
        _isActive = false;
        _balance = 0;
        _recentTransactions = new List<Transaction>();
    }
    
    // Snapshot methods
    public object CreateSnapshot()
    {
        return new AccountSnapshot
        {
            Balance = _balance,
            IsActive = _isActive,
            AccountNumber = _accountNumber,
            CustomerName = _customerName,
            RecentTransactions = _recentTransactions.ToList()
        };
    }
    
    public void RestoreFromSnapshot(object snapshot)
    {
        if (snapshot is AccountSnapshot accountSnapshot)
        {
            _balance = accountSnapshot.Balance;
            _isActive = accountSnapshot.IsActive;
            _accountNumber = accountSnapshot.AccountNumber;
            _customerName = accountSnapshot.CustomerName;
            _recentTransactions = accountSnapshot.RecentTransactions.ToList();
        }
        else
        {
            throw new ArgumentException($"Expected AccountSnapshot but got {snapshot.GetType().Name}");
        }
    }
    
    // Command handlers
    public void CreateAccount(string accountNumber, string customerName)
    {
        if (_isActive)
            throw new InvalidOperationException("Account already exists");
            
        RaiseEvent(new AccountCreated(Id, accountNumber, customerName));
    }
    
    public void Deposit(decimal amount)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        RaiseEvent(new FundsDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new FundsWithdrawn(Id, amount));
    }
    
    // Event handlers
    private void Apply(AccountCreated @event)
    {
        _isActive = true;
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
        _recentTransactions.Add(new Transaction
        {
            Type = TransactionType.Deposit,
            Amount = @event.Amount,
            Timestamp = DateTime.UtcNow
        });
        
        // Keep only the 10 most recent transactions
        if (_recentTransactions.Count > 10)
            _recentTransactions.RemoveAt(0);
    }
    
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
        _recentTransactions.Add(new Transaction
        {
            Type = TransactionType.Withdrawal,
            Amount = @event.Amount,
            Timestamp = DateTime.UtcNow
        });
        
        // Keep only the 10 most recent transactions
        if (_recentTransactions.Count > 10)
            _recentTransactions.RemoveAt(0);
    }
}

// Snapshot class
public class AccountSnapshot
{
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public List<Transaction> RecentTransactions { get; set; }
}

public class Transaction
{
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum TransactionType
{
    Deposit,
    Withdrawal
}
```

### Using with a Repository

Snapshots are typically managed by a repository that supports the `ISnapshotSource` interface:

```csharp
public class SnapshotRepository : IRepository
{
    private readonly IStreamStoreConnection _connection;
    private readonly ISnapshotStore _snapshotStore;
    private readonly int _snapshotFrequency;
    
    public SnapshotRepository(
        IStreamStoreConnection connection,
        ISnapshotStore snapshotStore,
        int snapshotFrequency = 100)
    {
        _connection = connection;
        _snapshotStore = snapshotStore;
        _snapshotFrequency = snapshotFrequency;
    }
    
    public T GetById<T>(Guid id) where T : IEventSource
    {
        // Try to get the latest snapshot
        var snapshot = _snapshotStore.GetLatestSnapshot(id);
        
        // Create a new instance of the aggregate
        var aggregate = Activator.CreateInstance(typeof(T), id) as T;
        
        if (snapshot != null && aggregate is ISnapshotSource snapshotSource)
        {
            // Restore from snapshot
            snapshotSource.RestoreFromSnapshot(snapshot.State);
            snapshotSource.SnapshotVersion = snapshot.Version;
            
            // Get events after the snapshot
            var events = _connection.ReadStreamEventsForward(
                id.ToString(),
                snapshot.Version + 1,
                int.MaxValue);
                
            // Apply events after the snapshot
            aggregate.UpdateWithEvents(events, snapshot.Version);
        }
        else
        {
            // No snapshot, load all events
            var events = _connection.ReadStreamEventsForward(
                id.ToString(),
                0,
                int.MaxValue);
                
            // Apply all events
            aggregate.RestoreFromEvents(events);
        }
        
        return aggregate;
    }
    
    public void Save<T>(T aggregate) where T : IEventSource
    {
        // Get new events
        var newEvents = aggregate.TakeEvents();
        
        if (newEvents.Length > 0)
        {
            // Save events
            _connection.AppendToStream(
                aggregate.Id.ToString(),
                aggregate.ExpectedVersion,
                newEvents);
                
            // Update expected version
            aggregate.ExpectedVersion += newEvents.Length;
            
            // Check if we should create a snapshot
            if (aggregate is ISnapshotSource snapshotSource &&
                aggregate.ExpectedVersion >= snapshotSource.SnapshotVersion + _snapshotFrequency)
            {
                // Create and save snapshot
                var snapshot = snapshotSource.CreateSnapshot();
                _snapshotStore.SaveSnapshot(
                    aggregate.Id,
                    aggregate.ExpectedVersion,
                    snapshot);
                    
                // Update snapshot version
                snapshotSource.SnapshotVersion = aggregate.ExpectedVersion;
            }
        }
    }
}
```

## Snapshot Store Implementation

A simple in-memory snapshot store implementation:

```csharp
public interface ISnapshotStore
{
    SnapshotInfo GetLatestSnapshot(Guid aggregateId);
    void SaveSnapshot(Guid aggregateId, long version, object state);
}

public class SnapshotInfo
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public object State { get; set; }
}

public class InMemorySnapshotStore : ISnapshotStore
{
    private readonly Dictionary<Guid, List<SnapshotInfo>> _snapshots = 
        new Dictionary<Guid, List<SnapshotInfo>>();
    
    public SnapshotInfo GetLatestSnapshot(Guid aggregateId)
    {
        if (_snapshots.TryGetValue(aggregateId, out var snapshots) && snapshots.Count > 0)
        {
            return snapshots.OrderByDescending(s => s.Version).First();
        }
        
        return null;
    }
    
    public void SaveSnapshot(Guid aggregateId, long version, object state)
    {
        if (!_snapshots.TryGetValue(aggregateId, out var snapshots))
        {
            snapshots = new List<SnapshotInfo>();
            _snapshots[aggregateId] = snapshots;
        }
        
        snapshots.Add(new SnapshotInfo
        {
            AggregateId = aggregateId,
            Version = version,
            State = state
        });
    }
}
```

## Best Practices

1. **Snapshot Frequency**: Create snapshots at appropriate intervals based on entity update frequency
2. **Immutable Snapshots**: Make snapshot objects immutable to prevent accidental modifications
3. **Versioning**: Include version information in snapshots to handle schema evolution
4. **Serialization**: Ensure snapshot objects are serializable for storage
5. **Error Handling**: Implement proper error handling for snapshot creation and restoration
6. **Cleanup Policy**: Implement a policy for cleaning up old snapshots
7. **Testing**: Test both with and without snapshots to ensure consistent behavior
8. **Monitoring**: Monitor snapshot creation and restoration performance
9. **Storage Selection**: Choose appropriate storage for snapshots based on size and access patterns
10. **Snapshot Validation**: Validate snapshots before using them to restore entity state

## Common Pitfalls

1. **Snapshot Overuse**: Creating snapshots too frequently can lead to storage and performance issues
2. **Snapshot Underuse**: Not creating snapshots frequently enough reduces their performance benefit
3. **Complex Snapshots**: Overly complex snapshot objects can be difficult to serialize and maintain
4. **Missing Versioning**: Without proper versioning, snapshots can become incompatible after schema changes
5. **Circular References**: Circular references in snapshot objects can cause serialization issues
6. **Large Snapshots**: Very large snapshots can negate the performance benefits they aim to provide
7. **Ignoring Errors**: Failing to handle snapshot errors can lead to inconsistent entity state

## Related Components

- [IEventSource](./ievent-source.md): The base interface for event-sourced entities
- [AggregateRoot](./aggregate-root.md): Base class for domain entities that often implements `ISnapshotSource`
- [IRepository](./irepository.md): Interface for repositories that load and save event-sourced entities
- [Event](./event.md): Messages that represent state changes in event-sourced entities
- [EventRecorder](./event-recorder.md): Component that records events for event-sourced entities

---

**Navigation**:
- [← Previous: ICorrelatedEventSource](./icorrelated-event-source.md)
- [↑ Back to Top](#isnapshosource)
- [→ Next: EventRecorder](./event-recorder.md)
