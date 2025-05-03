# Implementing Projections

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to implement projections in Reactive Domain to create read models from event streams.

## Read Model Base Class

```csharp
using System;

namespace MyApp.ReadModels
{
    public abstract class ReadModelBase
    {
        public Guid Id { get; }
        public long Version { get; protected set; }
        
        protected ReadModelBase(Guid id)
        {
            Id = id;
            Version = 0;
        }
        
        protected void IncrementVersion()
        {
            Version++;
        }
    }
}
```

## Account Summary Read Model

```csharp
using System;

namespace MyApp.ReadModels
{
    public class AccountSummary : ReadModelBase
    {
        public string AccountNumber { get; private set; }
        public string CustomerName { get; private set; }
        public decimal Balance { get; private set; }
        public bool IsClosed { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastUpdatedAt { get; private set; }
        
        public AccountSummary(Guid id) : base(id)
        {
            CreatedAt = DateTime.UtcNow;
        }
        
        public void Update(string accountNumber, string customerName, decimal balance, bool isClosed)
        {
            AccountNumber = accountNumber;
            CustomerName = customerName;
            Balance = balance;
            IsClosed = isClosed;
            LastUpdatedAt = DateTime.UtcNow;
            
            IncrementVersion();
        }
        
        public void UpdateBalance(decimal newBalance)
        {
            Balance = newBalance;
            LastUpdatedAt = DateTime.UtcNow;
            
            IncrementVersion();
        }
        
        public void MarkAsClosed()
        {
            IsClosed = true;
            LastUpdatedAt = DateTime.UtcNow;
            
            IncrementVersion();
        }
    }
}
```

## Transaction History Read Model

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.ReadModels
{
    public class TransactionHistory : ReadModelBase
    {
        private readonly List<Transaction> _transactions = new List<Transaction>();
        
        public IReadOnlyList<Transaction> Transactions => _transactions.AsReadOnly();
        public decimal CurrentBalance => _transactions.Sum(t => t.Amount);
        
        public TransactionHistory(Guid id) : base(id)
        {
        }
        
        public void AddTransaction(string type, decimal amount, string description, DateTime timestamp)
        {
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = Id,
                Type = type,
                Amount = amount,
                Description = description,
                Timestamp = timestamp
            };
            
            _transactions.Add(transaction);
            IncrementVersion();
        }
        
        public IEnumerable<Transaction> GetTransactionsByDateRange(DateTime start, DateTime end)
        {
            return _transactions
                .Where(t => t.Timestamp >= start && t.Timestamp <= end)
                .OrderByDescending(t => t.Timestamp);
        }
        
        public IEnumerable<Transaction> GetTransactionsByType(string type)
        {
            return _transactions
                .Where(t => t.Type == type)
                .OrderByDescending(t => t.Timestamp);
        }
    }
    
    public class Transaction
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
```

## Read Model Repository Interface

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.ReadModels
{
    public interface IReadModelRepository<T> where T : ReadModelBase
    {
        T GetById(Guid id);
        void Save(T item);
    }
    
    public interface IQueryableReadModelRepository<T> : IReadModelRepository<T> where T : ReadModelBase
    {
        IEnumerable<T> Query(Func<T, bool> predicate);
    }
}
```

## In-Memory Read Model Repository

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.ReadModels
{
    public class InMemoryReadModelRepository<T> : IQueryableReadModelRepository<T> where T : ReadModelBase
    {
        private readonly Dictionary<Guid, T> _items = new Dictionary<Guid, T>();
        
        public T GetById(Guid id)
        {
            if (_items.TryGetValue(id, out var item))
            {
                return item;
            }
            
            return null;
        }
        
        public void Save(T item)
        {
            _items[item.Id] = item;
        }
        
        public IEnumerable<T> Query(Func<T, bool> predicate)
        {
            return _items.Values.Where(predicate);
        }
    }
}
```

## SQL Read Model Repository

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

namespace MyApp.ReadModels
{
    public class SqlAccountSummaryRepository : IQueryableReadModelRepository<AccountSummary>
    {
        private readonly string _connectionString;
        
        public SqlAccountSummaryRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }
        
        public AccountSummary GetById(Guid id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           CreatedAt, LastUpdatedAt, Version
                    FROM AccountSummaries
                    WHERE Id = @Id";
                    
                var account = connection.QuerySingleOrDefault<AccountSummaryDto>(sql, new { Id = id });
                
                if (account == null)
                {
                    return null;
                }
                
                return MapToAccountSummary(account);
            }
        }
        
        public void Save(AccountSummary item)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                var existingSql = "SELECT COUNT(1) FROM AccountSummaries WHERE Id = @Id";
                var exists = connection.ExecuteScalar<int>(existingSql, new { Id = item.Id }) > 0;
                
                if (exists)
                {
                    var updateSql = @"
                        UPDATE AccountSummaries
                        SET AccountNumber = @AccountNumber,
                            CustomerName = @CustomerName,
                            Balance = @Balance,
                            IsClosed = @IsClosed,
                            LastUpdatedAt = @LastUpdatedAt,
                            Version = @Version
                        WHERE Id = @Id";
                        
                    connection.Execute(updateSql, new
                    {
                        item.Id,
                        item.AccountNumber,
                        item.CustomerName,
                        item.Balance,
                        item.IsClosed,
                        LastUpdatedAt = DateTime.UtcNow,
                        item.Version
                    });
                }
                else
                {
                    var insertSql = @"
                        INSERT INTO AccountSummaries (Id, AccountNumber, CustomerName, Balance, 
                                                     IsClosed, CreatedAt, LastUpdatedAt, Version)
                        VALUES (@Id, @AccountNumber, @CustomerName, @Balance, 
                                @IsClosed, @CreatedAt, @LastUpdatedAt, @Version)";
                                
                    connection.Execute(insertSql, new
                    {
                        item.Id,
                        item.AccountNumber,
                        item.CustomerName,
                        item.Balance,
                        item.IsClosed,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow,
                        item.Version
                    });
                }
            }
        }
        
        public IEnumerable<AccountSummary> Query(Func<AccountSummary, bool> predicate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           CreatedAt, LastUpdatedAt, Version
                    FROM AccountSummaries";
                    
                var accounts = connection.Query<AccountSummaryDto>(sql);
                
                return accounts
                    .Select(MapToAccountSummary)
                    .Where(predicate);
            }
        }
        
        public IEnumerable<AccountSummary> GetAccountsWithBalanceAbove(decimal threshold)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           CreatedAt, LastUpdatedAt, Version
                    FROM AccountSummaries
                    WHERE Balance > @Threshold";
                    
                var accounts = connection.Query<AccountSummaryDto>(sql, new { Threshold = threshold });
                
                return accounts.Select(MapToAccountSummary);
            }
        }
        
        private AccountSummary MapToAccountSummary(AccountSummaryDto dto)
        {
            var account = new AccountSummary(dto.Id);
            
            // Use reflection to set private fields
            var type = typeof(AccountSummary);
            
            type.GetProperty("AccountNumber").SetValue(account, dto.AccountNumber);
            type.GetProperty("CustomerName").SetValue(account, dto.CustomerName);
            type.GetProperty("Balance").SetValue(account, dto.Balance);
            type.GetProperty("IsClosed").SetValue(account, dto.IsClosed);
            type.GetProperty("CreatedAt").SetValue(account, dto.CreatedAt);
            type.GetProperty("LastUpdatedAt").SetValue(account, dto.LastUpdatedAt);
            type.GetProperty("Version").SetValue(account, dto.Version);
            
            return account;
        }
        
        private class AccountSummaryDto
        {
            public Guid Id { get; set; }
            public string AccountNumber { get; set; }
            public string CustomerName { get; set; }
            public decimal Balance { get; set; }
            public bool IsClosed { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastUpdatedAt { get; set; }
            public long Version { get; set; }
        }
    }
}
```

## Projection Manager

```csharp
using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.ReadModels;

namespace MyApp.Projections
{
    public class ProjectionManager
    {
        private readonly Dictionary<Type, List<Action<object>>> _projectors = 
            new Dictionary<Type, List<Action<object>>>();
            
        private readonly IEventBus _eventBus;
        
        public ProjectionManager(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }
        
        public void RegisterProjection<TEvent>(Action<TEvent> projector) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            
            if (!_projectors.ContainsKey(eventType))
            {
                _projectors[eventType] = new List<Action<object>>();
                
                // Subscribe to the event
                _eventBus.Subscribe<TEvent>(e => ProjectEvent(e));
            }
            
            // Add the projector
            _projectors[eventType].Add(e => projector((TEvent)e));
        }
        
        private void ProjectEvent<TEvent>(TEvent @event) where TEvent : IEvent
        {
            var eventType = @event.GetType();
            
            if (_projectors.TryGetValue(eventType, out var projectors))
            {
                foreach (var projector in projectors)
                {
                    try
                    {
                        projector(@event);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error projecting event {eventType.Name}: {ex.Message}");
                    }
                }
            }
        }
    }
}
```

## Account Summary Projection

```csharp
using System;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.ReadModels;
using MyApp.Projections;

namespace MyApp.Projections
{
    public class AccountSummaryProjection
    {
        private readonly IReadModelRepository<AccountSummary> _repository;
        
        public AccountSummaryProjection(IReadModelRepository<AccountSummary> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public void Register(ProjectionManager projectionManager)
        {
            projectionManager.RegisterProjection<AccountCreated>(When);
            projectionManager.RegisterProjection<FundsDeposited>(When);
            projectionManager.RegisterProjection<FundsWithdrawn>(When);
            projectionManager.RegisterProjection<AccountClosed>(When);
        }
        
        private void When(AccountCreated @event)
        {
            var accountSummary = new AccountSummary(@event.AccountId);
            accountSummary.Update(@event.AccountNumber, @event.CustomerName, 0, false);
            
            _repository.Save(accountSummary);
        }
        
        private void When(FundsDeposited @event)
        {
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary != null)
            {
                accountSummary.UpdateBalance(accountSummary.Balance + @event.Amount);
                _repository.Save(accountSummary);
            }
        }
        
        private void When(FundsWithdrawn @event)
        {
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary != null)
            {
                accountSummary.UpdateBalance(accountSummary.Balance - @event.Amount);
                _repository.Save(accountSummary);
            }
        }
        
        private void When(AccountClosed @event)
        {
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary != null)
            {
                accountSummary.MarkAsClosed();
                _repository.Save(accountSummary);
            }
        }
    }
}
```

## Transaction History Projection

```csharp
using System;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.ReadModels;
using MyApp.Projections;

namespace MyApp.Projections
{
    public class TransactionHistoryProjection
    {
        private readonly IReadModelRepository<TransactionHistory> _repository;
        
        public TransactionHistoryProjection(IReadModelRepository<TransactionHistory> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public void Register(ProjectionManager projectionManager)
        {
            projectionManager.RegisterProjection<AccountCreated>(When);
            projectionManager.RegisterProjection<FundsDeposited>(When);
            projectionManager.RegisterProjection<FundsWithdrawn>(When);
            projectionManager.RegisterProjection<AccountClosed>(When);
        }
        
        private void When(AccountCreated @event)
        {
            var history = new TransactionHistory(@event.AccountId);
            history.AddTransaction(
                "CREATED", 
                0, 
                $"Account created: {@event.AccountNumber}", 
                DateTime.UtcNow);
                
            _repository.Save(history);
        }
        
        private void When(FundsDeposited @event)
        {
            var history = _repository.GetById(@event.AccountId);
            if (history == null)
            {
                history = new TransactionHistory(@event.AccountId);
            }
            
            history.AddTransaction(
                "DEPOSIT", 
                @event.Amount, 
                $"Deposit: {@event.Amount:C}", 
                DateTime.UtcNow);
                
            _repository.Save(history);
        }
        
        private void When(FundsWithdrawn @event)
        {
            var history = _repository.GetById(@event.AccountId);
            if (history == null)
            {
                history = new TransactionHistory(@event.AccountId);
            }
            
            history.AddTransaction(
                "WITHDRAWAL", 
                -@event.Amount, 
                $"Withdrawal: {@event.Amount:C}", 
                DateTime.UtcNow);
                
            _repository.Save(history);
        }
        
        private void When(AccountClosed @event)
        {
            var history = _repository.GetById(@event.AccountId);
            if (history == null)
            {
                history = new TransactionHistory(@event.AccountId);
            }
            
            history.AddTransaction(
                "CLOSED", 
                0, 
                "Account closed", 
                DateTime.UtcNow);
                
            _repository.Save(history);
        }
    }
}
```

## Complete Example

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.ReadModels;
using MyApp.Projections;

namespace MyApp.Examples
{
    public class ProjectionExample
    {
        public void DemonstrateProjections()
        {
            // Create an event bus
            var eventBus = new EventBus();
            
            // Create read model repositories
            var accountSummaryRepository = new InMemoryReadModelRepository<AccountSummary>();
            var transactionHistoryRepository = new InMemoryReadModelRepository<TransactionHistory>();
            
            // Create projection manager
            var projectionManager = new ProjectionManager(eventBus);
            
            // Register projections
            var accountSummaryProjection = new AccountSummaryProjection(accountSummaryRepository);
            accountSummaryProjection.Register(projectionManager);
            
            var transactionHistoryProjection = new TransactionHistoryProjection(transactionHistoryRepository);
            transactionHistoryProjection.Register(projectionManager);
            
            // Create and publish events
            var accountId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var causationId = Guid.NewGuid();
            
            // Account created event
            var accountCreatedEvent = new AccountCreated(
                accountId,
                "ACC-123",
                "John Doe",
                correlationId,
                causationId);
                
            eventBus.Publish(accountCreatedEvent);
            
            // Deposit event
            var depositEvent = new FundsDeposited(
                accountId,
                1000,
                correlationId,
                accountCreatedEvent.MessageId);
                
            eventBus.Publish(depositEvent);
            
            // Withdrawal event
            var withdrawalEvent = new FundsWithdrawn(
                accountId,
                500,
                correlationId,
                depositEvent.MessageId);
                
            eventBus.Publish(withdrawalEvent);
            
            // Check account summary read model
            var accountSummary = accountSummaryRepository.GetById(accountId);
            Console.WriteLine($"Account Summary: {accountSummary.AccountNumber}, Balance: {accountSummary.Balance:C}");
            
            // Check transaction history read model
            var transactionHistory = transactionHistoryRepository.GetById(accountId);
            Console.WriteLine($"Transaction Count: {transactionHistory.Transactions.Count}");
            Console.WriteLine($"Current Balance: {transactionHistory.CurrentBalance:C}");
            
            // Query transactions by type
            var deposits = transactionHistory.GetTransactionsByType("DEPOSIT");
            foreach (var deposit in deposits)
            {
                Console.WriteLine($"Deposit: {deposit.Amount:C}, Description: {deposit.Description}");
            }
        }
    }
}
```

## Key Concepts

### Read Models

- Read models are optimized for querying
- They are updated in response to domain events
- They can be stored in any format or database that suits the query requirements
- They are eventually consistent with the event-sourced aggregates

### Projections

- Projections transform events into read models
- They handle specific event types and update corresponding read models
- They can create multiple read models from the same events
- They are idempotent and can be replayed

### Read Model Repositories

- Store and retrieve read models
- Can be implemented using various storage technologies
- Provide query capabilities appropriate for the storage technology
- Do not need to be transactional with the event store

### Projection Manager

- Coordinates the registration of projections
- Routes events to the appropriate projectors
- Handles errors in projection processing

## Best Practices

1. **Separation of Concerns**: Keep read models separate from domain models
2. **Idempotency**: Design projections to be idempotent
3. **Error Handling**: Implement proper error handling in projections
4. **Optimized Queries**: Design read models for the specific queries they need to support
5. **Eventual Consistency**: Accept that read models will be eventually consistent with the domain model
6. **Versioning**: Include version information in read models for optimistic concurrency

## Common Pitfalls

1. **Complex Projections**: Avoid complex business logic in projections
2. **Missing Events**: Ensure all relevant events are handled by projections
3. **Performance Issues**: Be mindful of performance in projections, especially for high-volume events
4. **Tight Coupling**: Avoid tight coupling between projections and domain logic
5. **Overloaded Read Models**: Don't try to make a single read model support too many different query patterns

---

**Navigation**:
- [← Previous: Setting Up Event Listeners](event-listeners.md)
- [↑ Back to Top](#implementing-projections)
- [→ Next: Handling Correlation and Causation](correlation-causation.md)
