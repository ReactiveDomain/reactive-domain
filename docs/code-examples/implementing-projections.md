# Implementing Projections

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to implement projections in Reactive Domain to create read models from event streams, following current best practices.

## Read Model Base Class

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.ReadModels
{
    /// <summary>
    /// Base class for all read models providing common properties and behaviors
    /// </summary>
    public abstract class ReadModelBase<TId>
    {
        /// <summary>
        /// Unique identifier for the read model
        /// </summary>
        [Key]
        public TId Id { get; protected set; }
        
        /// <summary>
        /// Optimistic concurrency version
        /// </summary>
        [ConcurrencyCheck]
        public long Version { get; protected set; }
        
        /// <summary>
        /// When the read model was created
        /// </summary>
        public DateTime CreatedAt { get; protected set; }
        
        /// <summary>
        /// When the read model was last updated
        /// </summary>
        public DateTime? LastUpdatedAt { get; protected set; }
        
        /// <summary>
        /// Constructor for new read models
        /// </summary>
        /// <param name="id">The unique identifier</param>
        protected ReadModelBase(TId id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            
            Id = id;
            Version = 0;
            CreatedAt = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Protected constructor for ORM
        /// </summary>
        protected ReadModelBase()
        {
            // Required by some ORMs
        }
        
        /// <summary>
        /// Increments the version and updates the LastUpdatedAt timestamp
        /// </summary>
        protected void IncrementVersion()
        {
            Version++;
            LastUpdatedAt = DateTime.UtcNow;
        }
    }
}
```

## Account Summary Read Model

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.ReadModels
{
    /// <summary>
    /// Read model representing a summary view of an account
    /// </summary>
    [Table("AccountSummaries")]
    public class AccountSummary : ReadModelBase<Guid>
    {
        /// <summary>
        /// The account number (business identifier)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; private set; }
        
        /// <summary>
        /// The name of the customer who owns the account
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; private set; }
        
        /// <summary>
        /// The current balance of the account
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; private set; }
        
        /// <summary>
        /// Whether the account is closed
        /// </summary>
        public bool IsClosed { get; private set; }
        
        /// <summary>
        /// The date of the last transaction
        /// </summary>
        public DateTime? LastTransactionDate { get; private set; }
        
        /// <summary>
        /// The total number of transactions
        /// </summary>
        public int TransactionCount { get; private set; }
        
        /// <summary>
        /// Creates a new account summary
        /// </summary>
        /// <param name="id">The unique identifier of the account</param>
        public AccountSummary(Guid id) : base(id)
        {
            TransactionCount = 0;
            Balance = 0m;
            IsClosed = false;
        }
        
        /// <summary>
        /// Protected constructor for ORM
        /// </summary>
        protected AccountSummary() : base()
        {
            // Required by some ORMs
        }
        
        /// <summary>
        /// Updates the account details
        /// </summary>
        public void Update(string accountNumber, string customerName)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrWhiteSpace(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            AccountNumber = accountNumber;
            CustomerName = customerName;
            
            IncrementVersion();
        }
        
        /// <summary>
        /// Records a deposit to the account
        /// </summary>
        /// <param name="amount">The amount deposited</param>
        /// <param name="transactionDate">The date of the transaction</param>
        public void RecordDeposit(decimal amount, DateTime transactionDate)
        {
            if (amount <= 0)
                throw new ArgumentException("Deposit amount must be positive", nameof(amount));
                
            if (IsClosed)
                throw new InvalidOperationException("Cannot deposit to a closed account");
                
            Balance += amount;
            TransactionCount++;
            LastTransactionDate = transactionDate;
            
            IncrementVersion();
        }
        
        /// <summary>
        /// Records a withdrawal from the account
        /// </summary>
        /// <param name="amount">The amount withdrawn</param>
        /// <param name="transactionDate">The date of the transaction</param>
        public void RecordWithdrawal(decimal amount, DateTime transactionDate)
        {
            if (amount <= 0)
                throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
                
            if (IsClosed)
                throw new InvalidOperationException("Cannot withdraw from a closed account");
                
            if (Balance < amount)
                throw new InvalidOperationException("Insufficient funds");
                
            Balance -= amount;
            TransactionCount++;
            LastTransactionDate = transactionDate;
            
            IncrementVersion();
        }
        
        /// <summary>
        /// Marks the account as closed
        /// </summary>
        /// <param name="closureDate">The date the account was closed</param>
        public void MarkAsClosed(DateTime closureDate)
        {
            if (IsClosed)
                return; // Already closed
                
            IsClosed = true;
            LastTransactionDate = closureDate;
            
            IncrementVersion();
        }
    }
}
```

## Transaction History Read Model

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace MyApp.ReadModels
{
    /// <summary>
    /// Read model representing the transaction history for an account
    /// </summary>
    [Table("TransactionHistories")]
    public class TransactionHistory : ReadModelBase<Guid>
    {
        // Use backing field for EF Core to properly track the collection
        private readonly List<Transaction> _transactions = new List<Transaction>();
        
        /// <summary>
        /// All transactions for this account
        /// </summary>
        public virtual IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();
        
        /// <summary>
        /// The current balance calculated from all transactions
        /// </summary>
        [NotMapped] // This is a calculated property, not stored in the database
        public decimal CurrentBalance => _transactions.Sum(t => t.Amount);
        
        /// <summary>
        /// The total number of transactions
        /// </summary>
        public int TransactionCount => _transactions.Count;
        
        /// <summary>
        /// The date of the most recent transaction
        /// </summary>
        public DateTime? LastTransactionDate => _transactions.Any() ? 
            _transactions.Max(t => t.Timestamp) : null;
        
        /// <summary>
        /// Creates a new transaction history for an account
        /// </summary>
        /// <param name="id">The account ID</param>
        public TransactionHistory(Guid id) : base(id)
        {
        }
        
        /// <summary>
        /// Protected constructor for ORM
        /// </summary>
        protected TransactionHistory() : base()
        {
            // Required by some ORMs
        }
        
        /// <summary>
        /// Adds a new transaction to the history
        /// </summary>
        /// <param name="transactionId">Unique ID for the transaction</param>
        /// <param name="type">Type of transaction (e.g., DEPOSIT, WITHDRAWAL)</param>
        /// <param name="amount">Amount of the transaction (positive for deposits, negative for withdrawals)</param>
        /// <param name="description">Description of the transaction</param>
        /// <param name="timestamp">When the transaction occurred</param>
        /// <param name="correlationId">Optional correlation ID for tracking related operations</param>
        public void AddTransaction(
            Guid transactionId,
            string type,
            decimal amount,
            string description,
            DateTime timestamp,
            Guid? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Transaction type cannot be empty", nameof(type));
                
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Transaction description cannot be empty", nameof(description));
                
            var transaction = new Transaction
            {
                Id = transactionId,
                AccountId = Id,
                Type = type,
                Amount = amount,
                Description = description,
                Timestamp = timestamp,
                CorrelationId = correlationId
            };
            
            _transactions.Add(transaction);
            IncrementVersion();
        }
        
        /// <summary>
        /// Retrieves transactions within a specified date range
        /// </summary>
        /// <param name="start">Start date (inclusive)</param>
        /// <param name="end">End date (inclusive)</param>
        /// <returns>Transactions ordered by timestamp descending</returns>
        public IEnumerable<Transaction> GetTransactionsByDateRange(DateTime start, DateTime end)
        {
            return _transactions
                .Where(t => t.Timestamp >= start && t.Timestamp <= end)
                .OrderByDescending(t => t.Timestamp);
        }
        
        /// <summary>
        /// Retrieves transactions of a specific type
        /// </summary>
        /// <param name="type">Transaction type (e.g., DEPOSIT, WITHDRAWAL)</param>
        /// <returns>Transactions ordered by timestamp descending</returns>
        public IEnumerable<Transaction> GetTransactionsByType(string type)
        {
            return _transactions
                .Where(t => t.Type == type)
                .OrderByDescending(t => t.Timestamp);
        }
        
        /// <summary>
        /// Gets the most recent transactions
        /// </summary>
        /// <param name="count">Number of transactions to retrieve</param>
        /// <returns>Most recent transactions ordered by timestamp descending</returns>
        public IEnumerable<Transaction> GetRecentTransactions(int count)
        {
            return _transactions
                .OrderByDescending(t => t.Timestamp)
                .Take(count);
        }
        
        /// <summary>
        /// Gets transactions by correlation ID
        /// </summary>
        /// <param name="correlationId">The correlation ID to search for</param>
        /// <returns>Correlated transactions ordered by timestamp</returns>
        public IEnumerable<Transaction> GetTransactionsByCorrelationId(Guid correlationId)
        {
            return _transactions
                .Where(t => t.CorrelationId == correlationId)
                .OrderBy(t => t.Timestamp);
        }
    }
    
    /// <summary>
    /// Represents a single financial transaction
    /// </summary>
    [Table("Transactions")]
    public class Transaction
    {
        /// <summary>
        /// Unique identifier for the transaction
        /// </summary>
        [Key]
        public Guid Id { get; set; }
        
        /// <summary>
        /// The account this transaction belongs to
        /// </summary>
        public Guid AccountId { get; set; }
        
        /// <summary>
        /// Type of transaction (e.g., DEPOSIT, WITHDRAWAL)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Type { get; set; }
        
        /// <summary>
        /// Amount of the transaction (positive for deposits, negative for withdrawals)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        /// <summary>
        /// Description of the transaction
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Description { get; set; }
        
        /// <summary>
        /// When the transaction occurred
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Optional correlation ID for tracking related operations
        /// </summary>
        public Guid? CorrelationId { get; set; }
        
        /// <summary>
        /// Reference to the transaction history this transaction belongs to
        /// </summary>
        [ForeignKey("AccountId")]
        public virtual TransactionHistory TransactionHistory { get; set; }
    }
}

## Read Model Repository Interfaces

```csharp
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.ReadModels
{
    /// <summary>
    /// Base interface for read model repositories with async operations
    /// </summary>
    /// <typeparam name="T">Type of read model</typeparam>
    /// <typeparam name="TId">Type of read model ID</typeparam>
    public interface IReadModelRepository<T, TId> where T : ReadModelBase<TId>
    {
        /// <summary>
        /// Gets a read model by its ID
        /// </summary>
        /// <param name="id">The ID of the read model</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The read model or null if not found</returns>
        Task<T> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves a read model
        /// </summary>
        /// <param name="item">The read model to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SaveAsync(T item, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a read model
        /// </summary>
        /// <param name="item">The read model to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task DeleteAsync(T item, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if a read model with the specified ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the read model exists, false otherwise</returns>
        Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface for queryable read model repositories
    /// </summary>
    /// <typeparam name="T">Type of read model</typeparam>
    /// <typeparam name="TId">Type of read model ID</typeparam>
    public interface IQueryableReadModelRepository<T, TId> : IReadModelRepository<T, TId> where T : ReadModelBase<TId>
    {
        /// <summary>
        /// Queries read models using an expression
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Matching read models</returns>
        Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all read models
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>All read models</returns>
        Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a paged result of read models
        /// </summary>
        /// <param name="skip">Number of items to skip</param>
        /// <param name="take">Number of items to take</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paged read models</returns>
        Task<IEnumerable<T>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// In-memory implementation for testing and development
    /// </summary>
    /// <typeparam name="T">Type of read model</typeparam>
    /// <typeparam name="TId">Type of read model ID</typeparam>
    public class InMemoryReadModelRepository<T, TId> : IQueryableReadModelRepository<T, TId> 
        where T : ReadModelBase<TId>
        where TId : notnull
    {
        private readonly Dictionary<TId, T> _items = new Dictionary<TId, T>();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        
        public async Task<T> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_items.TryGetValue(id, out var item))
                {
                    return item;
                }
                
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public async Task SaveAsync(T item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _items[item.Id] = item;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public async Task DeleteAsync(T item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_items.ContainsKey(item.Id))
                {
                    _items.Remove(item.Id);
                }
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _items.ContainsKey(id);
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var compiledPredicate = predicate.Compile();
                return _items.Values.Where(compiledPredicate).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _items.Values.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public async Task<IEnumerable<T>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative");
            if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take), "Take must be positive");
            
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _items.Values.Skip(skip).Take(take).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }
        
        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}

## SQL Read Model Repository
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Dapper;

namespace MyApp.ReadModels
{
    /// <summary>
    /// SQL Server implementation of the account summary repository
    /// </summary>
    public class SqlAccountSummaryRepository : IQueryableReadModelRepository<AccountSummary, Guid>
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlAccountSummaryRepository> _logger;
        
        /// <summary>
        /// Creates a new SQL repository for account summaries
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="logger">Logger instance</param>
        public SqlAccountSummaryRepository(
            string connectionString,
            ILogger<SqlAccountSummaryRepository> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc/>
        public async Task<AccountSummary> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting account summary with ID {AccountId}", id);
            
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           TransactionCount, LastTransactionDate, CreatedAt, LastUpdatedAt, Version 
                    FROM AccountSummaries 
                    WHERE Id = @Id";
                
                var account = await connection.QuerySingleOrDefaultAsync<AccountSummaryDto>(
                    new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
                
                if (account == null)
                {
                    _logger.LogDebug("Account summary with ID {AccountId} not found", id);
                    return null;
                }
                
                var result = MapToAccountSummary(account);
                _logger.LogDebug("Successfully retrieved account summary with ID {AccountId}", id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account summary with ID {AccountId}", id);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task SaveAsync(AccountSummary item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            
            _logger.LogDebug("Saving account summary with ID {AccountId}", item.Id);
            
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                
                try
                {
                    var existingSql = "SELECT COUNT(1) FROM AccountSummaries WHERE Id = @Id";
                    var exists = await connection.ExecuteScalarAsync<int>(
                        new CommandDefinition(existingSql, new { Id = item.Id }, transaction, cancellationToken: cancellationToken)) > 0;
                    
                    if (exists)
                    {
                        // Check for concurrency conflicts
                        var versionSql = "SELECT Version FROM AccountSummaries WHERE Id = @Id";
                        var currentVersion = await connection.ExecuteScalarAsync<long>(
                            new CommandDefinition(versionSql, new { Id = item.Id }, transaction, cancellationToken: cancellationToken));
                        
                        if (currentVersion != item.Version)
                        {
                            throw new DbUpdateConcurrencyException(
                                $"Concurrency conflict detected for account summary with ID {item.Id}. " +
                                $"Current version: {currentVersion}, Attempted update version: {item.Version}");
                        }
                        
                        var updateSql = @"
                            UPDATE AccountSummaries
                            SET AccountNumber = @AccountNumber,
                                CustomerName = @CustomerName,
                                Balance = @Balance,
                                IsClosed = @IsClosed,
                                TransactionCount = @TransactionCount,
                                LastTransactionDate = @LastTransactionDate,
                                LastUpdatedAt = @LastUpdatedAt,
                                Version = @Version
                            WHERE Id = @Id AND Version = @CurrentVersion";
                        
                        var updateResult = await connection.ExecuteAsync(
                            new CommandDefinition(updateSql, new
                            {
                                item.Id,
                                item.AccountNumber,
                                item.CustomerName,
                                item.Balance,
                                item.IsClosed,
                                item.TransactionCount,
                                item.LastTransactionDate,
                                LastUpdatedAt = DateTime.UtcNow,
                                Version = item.Version + 1,
                                CurrentVersion = item.Version
                            }, transaction, cancellationToken: cancellationToken));
                        
                        if (updateResult == 0)
                        {
                            throw new DbUpdateConcurrencyException(
                                $"Concurrency conflict detected for account summary with ID {item.Id}");
                        }
                    }
                    else
                    {
                        var insertSql = @"
                            INSERT INTO AccountSummaries (
                                Id, AccountNumber, CustomerName, Balance, IsClosed, 
                                TransactionCount, LastTransactionDate, CreatedAt, LastUpdatedAt, Version)
                            VALUES (
                                @Id, @AccountNumber, @CustomerName, @Balance, @IsClosed, 
                                @TransactionCount, @LastTransactionDate, @CreatedAt, @LastUpdatedAt, @Version)";
                        
                        await connection.ExecuteAsync(
                            new CommandDefinition(insertSql, new
                            {
                                item.Id,
                                item.AccountNumber,
                                item.CustomerName,
                                item.Balance,
                                item.IsClosed,
                                item.TransactionCount,
                                item.LastTransactionDate,
                                item.CreatedAt,
                                LastUpdatedAt = DateTime.UtcNow,
                                Version = 1
                            }, transaction, cancellationToken: cancellationToken));
                    }
                    
                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogDebug("Successfully saved account summary with ID {AccountId}", item.Id);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict saving account summary with ID {AccountId}", item.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving account summary with ID {AccountId}", item.Id);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task DeleteAsync(AccountSummary item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            
            _logger.LogDebug("Deleting account summary with ID {AccountId}", item.Id);
            
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var sql = "DELETE FROM AccountSummaries WHERE Id = @Id AND Version = @Version";
                var result = await connection.ExecuteAsync(
                    new CommandDefinition(sql, new { Id = item.Id, Version = item.Version }, cancellationToken: cancellationToken));
                
                if (result == 0)
                {
                    throw new DbUpdateConcurrencyException(
                        $"Concurrency conflict detected for account summary with ID {item.Id}");
                }
                
                _logger.LogDebug("Successfully deleted account summary with ID {AccountId}", item.Id);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict deleting account summary with ID {AccountId}", item.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account summary with ID {AccountId}", item.Id);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var sql = "SELECT COUNT(1) FROM AccountSummaries WHERE Id = @Id";
                var count = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
                
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if account summary with ID {AccountId} exists", id);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<IEnumerable<AccountSummary>> QueryAsync(
            Expression<Func<AccountSummary, bool>> predicate, 
            CancellationToken cancellationToken = default)
        {
            // Note: This implementation doesn't translate the expression to SQL
            // In a real-world scenario, you would use a library like Dapper.Contrib or EF Core
            // that can translate expressions to SQL
            _logger.LogWarning("QueryAsync with expression is not optimized and will load all records");
            
            try
            {
                var allItems = await GetAllAsync(cancellationToken);
                var compiledPredicate = predicate.Compile();
                return allItems.Where(compiledPredicate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying account summaries with predicate");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<IEnumerable<AccountSummary>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           TransactionCount, LastTransactionDate, CreatedAt, LastUpdatedAt, Version 
                    FROM AccountSummaries";
                
                var accounts = await connection.QueryAsync<AccountSummaryDto>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken));
                
                return accounts.Select(MapToAccountSummary).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all account summaries");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<IEnumerable<AccountSummary>> GetPagedAsync(
            int skip, int take, CancellationToken cancellationToken = default)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative");
            if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take), "Take must be positive");
            
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           TransactionCount, LastTransactionDate, CreatedAt, LastUpdatedAt, Version 
                    FROM AccountSummaries
                    ORDER BY CreatedAt DESC
                    OFFSET @Skip ROWS
                    FETCH NEXT @Take ROWS ONLY";
                
                var accounts = await connection.QueryAsync<AccountSummaryDto>(
                    new CommandDefinition(sql, new { Skip = skip, Take = take }, cancellationToken: cancellationToken));
                
                return accounts.Select(MapToAccountSummary).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged account summaries (skip: {Skip}, take: {Take})", skip, take);
                throw;
            }
        }
        
        /// <summary>
        /// Gets accounts with a balance above the specified threshold
        /// </summary>
        /// <param name="threshold">Minimum balance threshold</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Matching account summaries</returns>
        public async Task<IEnumerable<AccountSummary>> GetAccountsWithBalanceAboveAsync(
            decimal threshold, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           TransactionCount, LastTransactionDate, CreatedAt, LastUpdatedAt, Version 
                    FROM AccountSummaries
                    WHERE Balance > @Threshold
                    ORDER BY Balance DESC";
                
                var accounts = await connection.QueryAsync<AccountSummaryDto>(
                    new CommandDefinition(sql, new { Threshold = threshold }, cancellationToken: cancellationToken));
                
                return accounts.Select(MapToAccountSummary).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts with balance above {Threshold}", threshold);
                throw;
            }
        }
        
        /// <summary>
        /// Gets accounts with recent activity
        /// </summary>
        /// <param name="days">Number of days to consider as recent</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Accounts with recent activity</returns>
        public async Task<IEnumerable<AccountSummary>> GetAccountsWithRecentActivityAsync(
            int days, CancellationToken cancellationToken = default)
        {
            if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days), "Days must be positive");
            
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                var cutoffDate = DateTime.UtcNow.AddDays(-days);
                
                var sql = @"
                    SELECT Id, AccountNumber, CustomerName, Balance, IsClosed, 
                           TransactionCount, LastTransactionDate, CreatedAt, LastUpdatedAt, Version 
                    FROM AccountSummaries
                    WHERE LastTransactionDate >= @CutoffDate
                    ORDER BY LastTransactionDate DESC";
                
                var accounts = await connection.QueryAsync<AccountSummaryDto>(
                    new CommandDefinition(sql, new { CutoffDate = cutoffDate }, cancellationToken: cancellationToken));
                
                return accounts.Select(MapToAccountSummary).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts with recent activity in the last {Days} days", days);
                throw;
            }
        }
        
        /// <summary>
        /// Maps a DTO to an AccountSummary entity
        /// </summary>
        private AccountSummary MapToAccountSummary(AccountSummaryDto dto)
        {
            // In a real implementation, consider using a mapping library like AutoMapper
            var account = new AccountSummary(dto.Id);
            
            // Use reflection to set private properties
            var type = typeof(AccountSummary);
            var baseType = typeof(ReadModelBase<Guid>);
            
            // Set properties from AccountSummary class
            type.GetProperty(nameof(AccountSummary.AccountNumber)).SetValue(account, dto.AccountNumber);
            type.GetProperty(nameof(AccountSummary.CustomerName)).SetValue(account, dto.CustomerName);
            type.GetProperty(nameof(AccountSummary.Balance)).SetValue(account, dto.Balance);
            type.GetProperty(nameof(AccountSummary.IsClosed)).SetValue(account, dto.IsClosed);
            type.GetProperty(nameof(AccountSummary.TransactionCount)).SetValue(account, dto.TransactionCount);
            type.GetProperty(nameof(AccountSummary.LastTransactionDate)).SetValue(account, dto.LastTransactionDate);
            
            // Set properties from base class
            baseType.GetProperty(nameof(ReadModelBase<Guid>.CreatedAt)).SetValue(account, dto.CreatedAt);
            baseType.GetProperty(nameof(ReadModelBase<Guid>.LastUpdatedAt)).SetValue(account, dto.LastUpdatedAt);
            baseType.GetProperty(nameof(ReadModelBase<Guid>.Version)).SetValue(account, dto.Version);
            
            return account;
        }
        
        /// <summary>
        /// DTO for mapping between the database and domain model
        /// </summary>
        private class AccountSummaryDto
        {
            public Guid Id { get; set; }
            public string AccountNumber { get; set; }
            public string CustomerName { get; set; }
            public decimal Balance { get; set; }
            public bool IsClosed { get; set; }
            public int TransactionCount { get; set; }
            public DateTime? LastTransactionDate { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastUpdatedAt { get; set; }
            public long Version { get; set; }
        }
    }
    
    /// <summary>
    /// Exception thrown when a concurrency conflict is detected
    /// </summary>
    public class DbUpdateConcurrencyException : Exception
    {
        public DbUpdateConcurrencyException(string message) : base(message) { }
        public DbUpdateConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
    }
}

## Projection Manager

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace MyApp.Projections
{
    /// <summary>
    /// Manages event projections by subscribing to the event bus and routing events to registered projectors
    /// </summary>
    public class ProjectionManager : BackgroundService
    {
        private readonly IMessageBus _bus;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionManager> _logger;
        private readonly ConcurrentDictionary<Type, List<IProjector>> _projectors = new();
        private readonly List<IDisposable> _subscriptions = new();
        private readonly SemaphoreSlim _projectionLock = new(1, 1);
        
        /// <summary>
        /// Creates a new projection manager
        /// </summary>
        /// <param name="bus">Message bus to subscribe to</param>
        /// <param name="serviceProvider">Service provider for resolving projectors</param>
        /// <param name="logger">Logger instance</param>
        public ProjectionManager(
            IMessageBus bus,
            IServiceProvider serviceProvider,
            ILogger<ProjectionManager> logger)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Starts the projection manager and registers all projectors
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting projection manager");
            
            try
            {   
                // Register all projectors from DI container
                var projectors = _serviceProvider.GetServices<IProjector>().ToList();
                
                foreach (var projector in projectors)
                {
                    await RegisterProjectorAsync(projector);
                }
                
                // Register TransactionHistoryProjector
                var transactionHistoryProjector = _serviceProvider.GetService<TransactionHistoryProjector>();
                await RegisterProjectorAsync(transactionHistoryProjector);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting projection manager");
                throw;
            }
        }
        
        /// <summary>
        /// Registers a projector with the projection manager
        /// </summary>
        private async Task RegisterProjectorAsync(IProjector projector)
        {
            _logger.LogDebug("Registering projector {ProjectorType}", projector.GetType().Name);
            
            try
            {
                // Get the event types handled by the projector
                var eventTypes = projector.GetHandledEventTypes();
                
                // Subscribe to each event type
                foreach (var eventType in eventTypes)
                {
                    var subscription = _bus.Subscribe(eventType, async (evt, token) =>
                    {
                        await ProjectEventAsync(projector, evt, token);
                    });
                    
                    _subscriptions.Add(subscription);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering projector {ProjectorType}", projector.GetType().Name);
                throw;
            }
        }
        
        /// <summary>
        /// Projects an event using the specified projector
        /// </summary>
        private async Task ProjectEventAsync(IProjector projector, object evt, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Projecting event {EventType} using projector {ProjectorType}", evt.GetType().Name, projector.GetType().Name);
            
            try
            {
                // Lock the projector to prevent concurrent execution
                await _projectionLock.WaitAsync(cancellationToken);
                
                try
                {
                    // Project the event
                    await projector.ProjectEventAsync(evt, cancellationToken);
                }
                finally
                {
                    _projectionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting event {EventType} using projector {ProjectorType}", evt.GetType().Name, projector.GetType().Name);
                throw;
            }
        }
    }
}

## Account Summary Projector

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Domain.Events;
using MyApp.ReadModels;
using ReactiveDomain.Messaging;

namespace MyApp.Projections
{
    /// <summary>
    /// Projector for maintaining the AccountSummary read model
    /// </summary>
    public class AccountSummaryProjector : 
        IProjectEvents<AccountCreated>,
        IProjectEvents<FundsDeposited>,
        IProjectEvents<FundsWithdrawn>,
        IProjectEvents<AccountClosed>
    {
        private readonly IQueryableReadModelRepository<AccountSummary, Guid> _repository;
        private readonly ILogger<AccountSummaryProjector> _logger;
        
        /// <summary>
        /// Creates a new account summary projector
        /// </summary>
        /// <param name="repository">Repository for account summaries</param>
        /// <param name="logger">Logger instance</param>
        public AccountSummaryProjector(
            IQueryableReadModelRepository<AccountSummary, Guid> repository,
            ILogger<AccountSummaryProjector> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc/>
        public IEnumerable<Type> GetHandledEventTypes()
        {
            return new[] 
            { 
                typeof(AccountCreated),
                typeof(FundsDeposited),
                typeof(FundsWithdrawn),
                typeof(AccountClosed)
            };
        }
        
        /// <summary>
        /// Projects the AccountCreated event
        /// </summary>
        public async Task ProjectEventAsync(AccountCreated evt)
        {
            _logger.LogDebug("Projecting AccountCreated event for account {AccountId}", evt.AggregateId);
            
            try
            {
                // Check if the account summary already exists
                var exists = await _repository.ExistsAsync(evt.AggregateId);
                
                if (exists)
                {
                    _logger.LogWarning("Account summary already exists for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Create a new account summary
                var accountSummary = new AccountSummary(evt.AggregateId)
                {
                    AccountNumber = evt.AccountNumber,
                    CustomerName = evt.CustomerName,
                    Balance = 0,
                    IsClosed = false,
                    TransactionCount = 0,
                    CreatedAt = evt.Timestamp
                };
                
                // Save the account summary
                await _repository.SaveAsync(accountSummary);
                
                _logger.LogInformation("Created account summary for account {AccountId}", evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting AccountCreated event for account {AccountId}", evt.AggregateId);
                throw;
            }
        }
        
        /// <summary>
        /// Projects the FundsDeposited event
        /// </summary>
        public async Task ProjectEventAsync(FundsDeposited evt)
        {
            _logger.LogDebug("Projecting FundsDeposited event for account {AccountId}", evt.AggregateId);
            
            try
            {
                // Get the account summary
                var accountSummary = await _repository.GetByIdAsync(evt.AggregateId);
                
                if (accountSummary == null)
                {
                    _logger.LogWarning("Account summary not found for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                if (accountSummary.IsClosed)
                {
                    _logger.LogWarning("Cannot deposit funds to closed account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Update the account summary
                accountSummary.RecordDeposit(evt.Amount, evt.Timestamp);
                
                // Save the updated account summary
                await _repository.SaveAsync(accountSummary);
                
                _logger.LogInformation("Updated account summary for deposit of {Amount} to account {AccountId}", 
                    evt.Amount, evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting FundsDeposited event for account {AccountId}", evt.AggregateId);
                throw;
            }
        }
        
        /// <summary>
        /// Projects the FundsWithdrawn event
        /// </summary>
        public async Task ProjectEventAsync(FundsWithdrawn evt)
        {
            _logger.LogDebug("Projecting FundsWithdrawn event for account {AccountId}", evt.AggregateId);
            
            try
            {
                // Get the account summary
                var accountSummary = await _repository.GetByIdAsync(evt.AggregateId);
                
                if (accountSummary == null)
                {
                    _logger.LogWarning("Account summary not found for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                if (accountSummary.IsClosed)
                {
                    _logger.LogWarning("Cannot withdraw funds from closed account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Update the account summary
                accountSummary.RecordWithdrawal(evt.Amount, evt.Timestamp);
                
                // Save the updated account summary
                await _repository.SaveAsync(accountSummary);
                
                _logger.LogInformation("Updated account summary for withdrawal of {Amount} from account {AccountId}", 
                    evt.Amount, evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting FundsWithdrawn event for account {AccountId}", evt.AggregateId);
                throw;
            }
        }
        
        /// <summary>
        /// Projects the AccountClosed event
        /// </summary>
        public async Task ProjectEventAsync(AccountClosed evt)
        {
            _logger.LogDebug("Projecting AccountClosed event for account {AccountId}", evt.AggregateId);
            
            try
            {
                // Get the account summary
                var accountSummary = await _repository.GetByIdAsync(evt.AggregateId);
                
                if (accountSummary == null)
                {
                    _logger.LogWarning("Account summary not found for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                if (accountSummary.IsClosed)
                {
                    _logger.LogWarning("Account {AccountId} is already closed", evt.AggregateId);
                    return;
                }
                
                // Update the account summary
                accountSummary.Close(evt.Timestamp);
                
                // Save the updated account summary
                await _repository.SaveAsync(accountSummary);
                
                _logger.LogInformation("Marked account {AccountId} as closed", evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting AccountClosed event for account {AccountId}", evt.AggregateId);
                throw;
            }
        }
    }
}

## Transaction History Projector

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Domain.Events;
using MyApp.ReadModels;
using ReactiveDomain.Messaging;

namespace MyApp.Projections
{
    /// <summary>
    /// Projector for maintaining the TransactionHistory read model
    /// </summary>
    public class TransactionHistoryProjector : 
        IProjectEvents<AccountCreated>,
        IProjectEvents<FundsDeposited>,
        IProjectEvents<FundsWithdrawn>,
        IProjectEvents<AccountClosed>
    {
        private readonly IReadModelRepository<TransactionHistory, Guid> _repository;
        private readonly ILogger<TransactionHistoryProjector> _logger;
        
        /// <summary>
        /// Creates a new transaction history projector
        /// </summary>
        /// <param name="repository">Repository for transaction histories</param>
        /// <param name="logger">Logger instance</param>
        public TransactionHistoryProjector(
            IReadModelRepository<TransactionHistory, Guid> repository,
            ILogger<TransactionHistoryProjector> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc/>
        public IEnumerable<Type> GetHandledEventTypes()
        {
            return new[] 
            { 
                typeof(AccountCreated),
                typeof(FundsDeposited),
                typeof(FundsWithdrawn),
                typeof(AccountClosed)
            };
        }
        
        /// <summary>
        /// Projects the AccountCreated event
        /// </summary>
        public async Task ProjectEventAsync(AccountCreated evt)
        {
            _logger.LogDebug("Projecting AccountCreated event for transaction history of account {AccountId}", evt.AggregateId);
            
            try
            {
                // Check if the transaction history already exists
                var exists = await _repository.ExistsAsync(evt.AggregateId);
                
                if (exists)
                {
                    _logger.LogWarning("Transaction history already exists for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Create a new transaction history
                var history = new TransactionHistory(evt.AggregateId)
                {
                    CreatedAt = evt.Timestamp
                };
                
                // Add the initial transaction
                history.AddTransaction(
                    Guid.NewGuid(),
                    "AccountCreated",
                    0,
                    $"Account {evt.AccountNumber} created for {evt.CustomerName}",
                    evt.Timestamp);
                
                // Save the transaction history
                await _repository.SaveAsync(history);
                
                _logger.LogInformation("Created transaction history for account {AccountId}", evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting AccountCreated event for transaction history of account {AccountId}", evt.AggregateId);
                throw;
            }
        }
        
        /// <summary>
        /// Projects the FundsDeposited event
        /// </summary>
        public async Task ProjectEventAsync(FundsDeposited evt)
        {
            _logger.LogDebug("Projecting FundsDeposited event for transaction history of account {AccountId}", evt.AggregateId);
            
            try
            {
                // Get the transaction history
                var history = await _repository.GetByIdAsync(evt.AggregateId);
                
                if (history == null)
                {
                    _logger.LogWarning("Transaction history not found for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Add the deposit transaction
                history.AddTransaction(
                    Guid.NewGuid(),
                    "Deposit",
                    evt.Amount,
                    $"Deposit of {evt.Amount:C} to account",
                    evt.Timestamp);
                
                // Save the updated transaction history
                await _repository.SaveAsync(history);
                
                _logger.LogInformation("Added deposit transaction of {Amount} to transaction history for account {AccountId}", 
                    evt.Amount, evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting FundsDeposited event for transaction history of account {AccountId}", evt.AggregateId);
                throw;
            }
        }
        
        /// <summary>
        /// Projects the FundsWithdrawn event
        /// </summary>
        public async Task ProjectEventAsync(FundsWithdrawn evt)
        {
            _logger.LogDebug("Projecting FundsWithdrawn event for transaction history of account {AccountId}", evt.AggregateId);
            
            try
            {
                // Get the transaction history
                var history = await _repository.GetByIdAsync(evt.AggregateId);
                
                if (history == null)
                {
                    _logger.LogWarning("Transaction history not found for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Add the withdrawal transaction
                history.AddTransaction(
                    Guid.NewGuid(),
                    "Withdrawal",
                    evt.Amount,
                    $"Withdrawal of {evt.Amount:C} from account",
                    evt.Timestamp);
                
                // Save the updated transaction history
                await _repository.SaveAsync(history);
                
                _logger.LogInformation("Added withdrawal transaction of {Amount} to transaction history for account {AccountId}", 
                    evt.Amount, evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting FundsWithdrawn event for transaction history of account {AccountId}", evt.AggregateId);
                throw;
            }
        }
        
        /// <summary>
        /// Projects the AccountClosed event
        /// </summary>
        public async Task ProjectEventAsync(AccountClosed evt)
        {
            _logger.LogDebug("Projecting AccountClosed event for transaction history of account {AccountId}", evt.AggregateId);
            
            try
            {
                // Get the transaction history
                var history = await _repository.GetByIdAsync(evt.AggregateId);
                
                if (history == null)
                {
                    _logger.LogWarning("Transaction history not found for account {AccountId}", evt.AggregateId);
                    return;
                }
                
                // Add the account closed transaction
                history.AddTransaction(
                    Guid.NewGuid(),
                    "AccountClosed",
                    0,
                    "Account closed",
                    evt.Timestamp);
                
                // Save the updated transaction history
                await _repository.SaveAsync(history);
                
                _logger.LogInformation("Added account closed transaction to transaction history for account {AccountId}", 
                    evt.AggregateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting AccountClosed event for transaction history of account {AccountId}", evt.AggregateId);
                throw;
            }
        }
    }
}
```

## Dependency Injection Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Domain;
using MyApp.Projections;
using MyApp.ReadModels;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace MyApp
{
    public static class ProjectionSetup
    {
        /// <summary>
        /// Registers all projection-related services with the DI container
        /// </summary>
        public static IServiceCollection AddProjections(this IServiceCollection services, string connectionString)
        {
            // Register repositories
            services.AddSingleton<IQueryableReadModelRepository<AccountSummary, Guid>>(
                provider => new SqlAccountSummaryRepository(
                    connectionString,
                    provider.GetRequiredService<ILogger<SqlAccountSummaryRepository>>()));
                    
            services.AddSingleton<IReadModelRepository<TransactionHistory, Guid>>(
                provider => new SqlTransactionHistoryRepository(
                    connectionString,
                    provider.GetRequiredService<ILogger<SqlTransactionHistoryRepository>>()));
            
            // Register projectors
            services.AddSingleton<AccountSummaryProjector>();
            services.AddSingleton<TransactionHistoryProjector>();
            
            // Register both projectors as IProjector for automatic discovery
            services.AddSingleton<IProjector>(provider => 
                provider.GetRequiredService<AccountSummaryProjector>());
                
            services.AddSingleton<IProjector>(provider => 
                provider.GetRequiredService<TransactionHistoryProjector>());
            
            // Register projection manager as a hosted service
            services.AddSingleton<ProjectionManager>();
            services.AddHostedService(provider => provider.GetRequiredService<ProjectionManager>());
            
            return services;
        }
    }
}
```

## Complete Example

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain.Events;
using MyApp.ReadModels;
using MyApp.Projections;

namespace MyApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Create and configure the host
            var host = CreateHostBuilder(args).Build();
            
            // Start the host (this will start the ProjectionManager as a hosted service)
            await host.StartAsync();
            
            try
            {
                // Get services
                var bus = host.Services.GetRequiredService<IMessageBus>();
                var accountSummaryRepo = host.Services.GetRequiredService<IQueryableReadModelRepository<AccountSummary, Guid>>();
                var transactionHistoryRepo = host.Services.GetRequiredService<IReadModelRepository<TransactionHistory, Guid>>();
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                
                // Create account
                var accountId = Guid.NewGuid();
                var accountNumber = "ACC-001";
                var customerName = "John Doe";
                
                logger.LogInformation("Creating account {AccountId} for {CustomerName}", accountId, customerName);
                
                // Publish events - in a real application, these would come from command handlers
                await PublishEventsAsync(bus, accountId, accountNumber, customerName);
                
                // Wait for projections to process events
                await Task.Delay(500); // In a real app, you would use proper synchronization
                
                // Get read models
                var accountSummary = await accountSummaryRepo.GetByIdAsync(accountId);
                var transactionHistory = await transactionHistoryRepo.GetByIdAsync(accountId);
                
                // Display results
                DisplayResults(accountSummary, transactionHistory, logger);
                
                // Wait for user input before shutting down
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                // Stop the host gracefully
                await host.StopAsync();
                host.Dispose();
            }
        }
        
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Register message bus
                    services.AddSingleton<IMessageBus, MessageBus>();
                    
                    // Register repositories (using in-memory implementations for the example)
                    services.AddSingleton<IQueryableReadModelRepository<AccountSummary, Guid>, InMemoryAccountSummaryRepository>();
                    services.AddSingleton<IReadModelRepository<TransactionHistory, Guid>, InMemoryTransactionHistoryRepository>();
                    
                    // Register projectors
                    services.AddSingleton<AccountSummaryProjector>();
                    services.AddSingleton<TransactionHistoryProjector>();
                    
                    // Register projectors as IProjector for automatic discovery
                    services.AddSingleton<IProjector>(provider => provider.GetRequiredService<AccountSummaryProjector>());
                    services.AddSingleton<IProjector>(provider => provider.GetRequiredService<TransactionHistoryProjector>());
                    
                    // Register projection manager as a hosted service
                    services.AddSingleton<ProjectionManager>();
                    services.AddHostedService(provider => provider.GetRequiredService<ProjectionManager>());
                });
        
        private static async Task PublishEventsAsync(IMessageBus bus, Guid accountId, string accountNumber, string customerName)
        {
            // Create account
            await bus.PublishAsync(new AccountCreated(accountId, accountNumber, customerName) { Timestamp = DateTime.UtcNow });
            
            // Deposit funds
            await Task.Delay(100); // Simulate some time passing
            await bus.PublishAsync(new FundsDeposited(accountId, 1000.00m) { Timestamp = DateTime.UtcNow });
            
            // Withdraw funds
            await Task.Delay(100); // Simulate some time passing
            await bus.PublishAsync(new FundsWithdrawn(accountId, 250.00m) { Timestamp = DateTime.UtcNow });
            
            // Another deposit
            await Task.Delay(100); // Simulate some time passing
            await bus.PublishAsync(new FundsDeposited(accountId, 500.00m) { Timestamp = DateTime.UtcNow });
        }
        
        private static void DisplayResults(AccountSummary accountSummary, TransactionHistory transactionHistory, ILogger logger)
        {
            Console.WriteLine("\nAccount Summary:");
            Console.WriteLine($"Account Number: {accountSummary.AccountNumber}");
            Console.WriteLine($"Customer: {accountSummary.CustomerName}");
            Console.WriteLine($"Balance: {accountSummary.Balance:C}");
            Console.WriteLine($"Transaction Count: {accountSummary.TransactionCount}");
            Console.WriteLine($"Created: {accountSummary.CreatedAt}");
            Console.WriteLine($"Last Updated: {accountSummary.LastUpdatedAt}");
            
            Console.WriteLine("\nTransaction History:");
            var transactions = transactionHistory.GetTransactions();
            
            foreach (var transaction in transactions)
            {
                Console.WriteLine($"{transaction.Timestamp:yyyy-MM-dd HH:mm:ss}: {transaction.Type} - {transaction.Amount:C} - {transaction.Description}");
            }
            
            logger.LogInformation("Retrieved account summary and transaction history with {TransactionCount} transactions", 
                transactions.Count);
        }
    }
}    
            eventBus.Publish(withdrawalEvent);
            
            // Check account summary read model
            var accountSummary = accountSummaryRepository.GetById(accountId);
            Console.WriteLine($"Account Summary: {accountSummary.AccountNumber}, Balance: {accountSummary.Balance:C}");
{{ ... }}
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
