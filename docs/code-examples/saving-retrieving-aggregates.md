# Saving and Retrieving Aggregates

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to save and retrieve aggregates using repositories in Reactive Domain, following current best practices.

## Repository Configuration with Dependency Injection

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Foundation;
using ReactiveDomain.EventStore;
using ReactiveDomain.Messaging;

namespace MyApp.Infrastructure
{
    public static class RepositoryConfiguration
    {
        public static IServiceCollection AddEventStore(this IServiceCollection services, IConfiguration configuration)
        {
            // Register event store connection
            services.AddSingleton<IStreamStoreConnection>(provider => 
            {
                var logger = provider.GetRequiredService<ILogger<IStreamStoreConnection>>();
                logger.LogInformation("Configuring EventStore connection");
                
                var connectionString = configuration.GetConnectionString("EventStore");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("EventStore connection string is not configured");
                }
                
                var connectionSettings = ConnectionSettings.Create()
                    .KeepReconnecting()
                    .KeepRetrying()
                    .SetDefaultUserCredentials(new UserCredentials(
                        configuration["EventStore:Username"] ?? "admin", 
                        configuration["EventStore:Password"] ?? "changeit"));
                    
                return new StreamStoreConnection(
                    "MyApp",
                    connectionSettings,
                    connectionString,
                    int.Parse(configuration["EventStore:TcpPort"] ?? "1113"));
            });
            
            // Register stream name builder
            services.AddSingleton<IStreamNameBuilder>(provider => 
            {
                var appPrefix = configuration["EventStore:StreamPrefix"] ?? "MyApp";
                return new PrefixedCamelCaseStreamNameBuilder(appPrefix);
            });
            
            // Register serializer
            services.AddSingleton<IEventSerializer, JsonMessageSerializer>();
            
            // Register repositories
            services.AddSingleton<IRepository>(provider => 
            {
                var connection = provider.GetRequiredService<IStreamStoreConnection>();
                var streamNameBuilder = provider.GetRequiredService<IStreamNameBuilder>();
                var serializer = provider.GetRequiredService<IEventSerializer>();
                
                return new StreamStoreRepository(
                    streamNameBuilder,
                    connection,
                    serializer);
            });
            
            // Register correlated repository
            services.AddSingleton<ICorrelatedRepository>(provider => 
            {
                var repository = provider.GetRequiredService<IRepository>();
                return new CorrelatedStreamStoreRepository(repository);
            });
            
            // Register generic repositories for specific aggregate types
            services.AddSingleton<IRepository<Account, Guid>>(provider => 
            {
                var repository = provider.GetRequiredService<IRepository>();
                return new TypedRepository<Account, Guid>(repository);
            });
            
            return services;
        }
    }
    
    // Type-safe repository wrapper
    public class TypedRepository<TAggregate, TId> : IRepository<TAggregate, TId> 
        where TAggregate : AggregateRoot<TId>
    {
        private readonly IRepository _repository;
        
        public TypedRepository(IRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public async Task<TAggregate> GetByIdAsync(TId id)
        {
            return await Task.FromResult(_repository.GetById<TAggregate>(id));
        }
        
        public async Task<bool> TryGetByIdAsync(TId id, out TAggregate aggregate)
        {
            var result = _repository.TryGetById(id, out var untypedAggregate);
            aggregate = untypedAggregate as TAggregate;
            return await Task.FromResult(result);
        }
        
        public async Task SaveAsync(TAggregate aggregate)
        {
            _repository.Save(aggregate);
            await Task.CompletedTask;
        }
        
        public async Task UpdateAsync(ref TAggregate aggregate)
        {
            _repository.Update(ref aggregate);
            await Task.CompletedTask;
        }
        
        public async Task DeleteAsync(TAggregate aggregate)
        {
            _repository.Delete(aggregate);
            await Task.CompletedTask;
        }
        
        public async Task HardDeleteAsync(TAggregate aggregate)
        {
            _repository.HardDelete(aggregate);
            await Task.CompletedTask;
        }
    }
    
    // Generic repository interface with strong typing
    public interface IRepository<TAggregate, TId> 
        where TAggregate : AggregateRoot<TId>
    {
        Task<TAggregate> GetByIdAsync(TId id);
        Task<bool> TryGetByIdAsync(TId id, out TAggregate aggregate);
        Task SaveAsync(TAggregate aggregate);
        Task UpdateAsync(ref TAggregate aggregate);
        Task DeleteAsync(TAggregate aggregate);
        Task HardDeleteAsync(TAggregate aggregate);
    }
}
```

## Basic Repository Operations with Strongly Typed Repository

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Foundation;
using MyApp.Domain;
using MyApp.Infrastructure;

namespace MyApp.Application
{
    public class AccountService
    {
        private readonly IRepository<Account, Guid> _repository;
        private readonly ILogger<AccountService> _logger;
        
        public AccountService(
            IRepository<Account, Guid> repository,
            ILogger<AccountService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<Account> CreateAccountAsync(string accountNumber, string customerName)
        {
            _logger.LogInformation("Creating new account for customer {CustomerName}", customerName);
            
            try
            {
                // Generate a unique ID for the account
                var accountId = Guid.NewGuid();
                
                // Create a new account
                var account = new Account(accountId);
                account.Create(accountNumber, customerName);
                
                // Save the account
                await _repository.SaveAsync(account);
                
                _logger.LogInformation("Account {AccountId} created successfully", accountId);
                
                return account;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account for customer {CustomerName}", customerName);
                throw;
            }
        }
        
        public async Task<Account> GetAccountAsync(Guid accountId)
        {
            _logger.LogDebug("Retrieving account {AccountId}", accountId);
            
            try
            {
                var account = await _repository.GetByIdAsync(accountId);
                return account;
            }
            catch (AggregateNotFoundException)
            {
                _logger.LogWarning("Account {AccountId} not found", accountId);
                return null;
            }
            catch (AggregateDeletedException)
            {
                _logger.LogWarning("Account {AccountId} has been deleted", accountId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account {AccountId}", accountId);
                throw;
            }
        }
        
        public async Task<bool> TryGetAccountAsync(Guid accountId, out Account account)
        {
            _logger.LogDebug("Attempting to retrieve account {AccountId}", accountId);
            
            try
            {
                return await _repository.TryGetByIdAsync(accountId, out account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attempting to retrieve account {AccountId}", accountId);
                account = null;
                return false;
            }
        }
        
        public async Task UpdateAccountAsync(Account account)
        {
            _logger.LogInformation("Updating account {AccountId}", account.Id);
            
            try
            {
                await _repository.SaveAsync(account);
                _logger.LogInformation("Account {AccountId} updated successfully", account.Id);
            }
            catch (AggregateVersionException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating account {AccountId}", account.Id);
                throw new ConcurrencyException($"The account has been modified by another process", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account {AccountId}", account.Id);
                throw;
            }
        }
        
        public async Task DeleteAccountAsync(Account account)
        {
            _logger.LogInformation("Soft deleting account {AccountId}", account.Id);
            
            try
            {
                await _repository.DeleteAsync(account);
                _logger.LogInformation("Account {AccountId} deleted successfully", account.Id);
            }
            catch (AggregateVersionException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict deleting account {AccountId}", account.Id);
                throw new ConcurrencyException($"The account has been modified by another process", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account {AccountId}", account.Id);
                throw;
            }
        }
        
        public async Task HardDeleteAccountAsync(Account account)
        {
            _logger.LogWarning("Hard deleting account {AccountId} - THIS OPERATION CANNOT BE UNDONE", account.Id);
            
            try
            {
                await _repository.HardDeleteAsync(account);
                _logger.LogInformation("Account {AccountId} hard deleted successfully", account.Id);
            }
            catch (AggregateVersionException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict hard deleting account {AccountId}", account.Id);
                throw new ConcurrencyException($"The account has been modified by another process", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting account {AccountId}", account.Id);
                throw;
            }
        }
        
        // Example of handling a transaction with optimistic concurrency
        public async Task<decimal> DepositAsync(Guid accountId, decimal amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Deposit amount must be positive", nameof(amount));
            }
            
            _logger.LogInformation("Depositing {Amount} to account {AccountId}", amount, accountId);
            
            int retryCount = 0;
            const int maxRetries = 3;
            
            while (true)
            {
                try
                {
                    // Get the latest version of the account
                    var account = await _repository.GetByIdAsync(accountId);
                    
                    // Apply the business operation
                    account.Deposit(amount);
                    
                    // Save the changes
                    await _repository.SaveAsync(account);
                    
                    _logger.LogInformation("Successfully deposited {Amount} to account {AccountId}", 
                        amount, accountId);
                    
                    // Return the new balance
                    return account.GetBalance();
                }
                catch (AggregateVersionException ex)
                {
                    // Handle concurrency conflict with retry logic
                    retryCount++;
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Failed to deposit after {RetryCount} attempts due to concurrency conflicts", 
                            retryCount);
                        throw new ConcurrencyException(
                            $"Failed to deposit after {retryCount} attempts due to concurrency conflicts", ex);
                    }
                    
                    _logger.LogWarning(ex, "Concurrency conflict depositing to account {AccountId}, retry attempt {RetryCount}", 
                        accountId, retryCount);
                    
                    // Wait before retrying (with exponential backoff)
                    await Task.Delay(100 * (int)Math.Pow(2, retryCount));
                }
                catch (AggregateNotFoundException)
                {
                    _logger.LogWarning("Account {AccountId} not found for deposit", accountId);
                    throw new AccountNotFoundException($"Account {accountId} not found");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error depositing {Amount} to account {AccountId}", amount, accountId);
                    throw;
                }
            }
        }
    }
}
```

## Correlated Repository Operations

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.Domain.Commands;

namespace MyApp.Application
{
    public class CorrelatedAccountRepository
    {
        private readonly ICorrelatedRepository _repository;
        
        public CorrelatedAccountRepository(ICorrelatedRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public async Task<Account> GetAccountAsync(Guid accountId, ICorrelatedMessage source)
        {
            _logger.LogDebug("Retrieving account {AccountId} with correlation {CorrelationId}", 
                accountId, source.CorrelationId);
            
            try
            {
                // We need to wrap the synchronous repository call in a Task to maintain the async pattern
                var account = await Task.FromResult(_repository.GetById<Account>(accountId, source));
                return account;
            }
            catch (AggregateNotFoundException)
            {
                _logger.LogWarning("Account {AccountId} not found (Correlation: {CorrelationId})", 
                    accountId, source.CorrelationId);
                return null;
            }
            catch (AggregateDeletedException)
            {
                _logger.LogWarning("Account {AccountId} has been deleted (Correlation: {CorrelationId})", 
                    accountId, source.CorrelationId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account {AccountId} (Correlation: {CorrelationId})", 
                    accountId, source.CorrelationId);
                throw;
            }
        }
        
        public async Task ProcessCreateAccountCommandAsync(CreateAccount command)
        {
            _logger.LogInformation("Processing create account command for customer {CustomerName} (Correlation: {CorrelationId})", 
                command.CustomerName, command.CorrelationId);
            
            try
            {
                // Create a new account with correlation
                var account = new Account(command.AccountId, command);
                
                // Initialize the account
                account.Create(command.AccountNumber, command.CustomerName);
                
                // Save the account with correlation
                await Task.FromResult(_repository.Save(account, command));
                
                _logger.LogInformation("Account {AccountId} created successfully (Correlation: {CorrelationId})", 
                    command.AccountId, command.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account for customer {CustomerName} (Correlation: {CorrelationId})", 
                    command.CustomerName, command.CorrelationId);
                throw;
            }
        }
        
        public async Task ProcessDepositCommandAsync(DepositFunds command)
        {
            _logger.LogInformation("Processing deposit of {Amount} to account {AccountId} (Correlation: {CorrelationId})", 
                command.Amount, command.AccountId, command.CorrelationId);
            
            try
            {
                // Load the account with correlation
                var account = await Task.FromResult(_repository.GetById<Account>(command.AccountId, command));
                
                // Process the command
                account.Deposit(command.Amount);
                
                // Save the account with correlation
                await Task.FromResult(_repository.Save(account, command));
                
                _logger.LogInformation("Successfully deposited {Amount} to account {AccountId} (Correlation: {CorrelationId})", 
                    command.Amount, command.AccountId, command.CorrelationId);
            }
            catch (AggregateNotFoundException)
            {
                _logger.LogWarning("Account {AccountId} not found for deposit (Correlation: {CorrelationId})", 
                    command.AccountId, command.CorrelationId);
                throw new AccountNotFoundException($"Account {command.AccountId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error depositing {Amount} to account {AccountId} (Correlation: {CorrelationId})", 
                    command.Amount, command.AccountId, command.CorrelationId);
                throw;
            }
        }
        
        // Example of handling a transaction with retry logic and correlation
        public async Task<decimal> WithdrawWithRetryAsync(WithdrawFunds command, int maxRetries = 3)
        {
            _logger.LogInformation("Withdrawing {Amount} from account {AccountId} (Correlation: {CorrelationId})", 
                command.Amount, command.AccountId, command.CorrelationId);
            
            int retryCount = 0;
            
            while (true)
            {
                try
                {
                    // Load the account with correlation
                    var account = await Task.FromResult(_repository.GetById<Account>(command.AccountId, command));
                    
                    // Process the command
                    account.Withdraw(command.Amount);
                    
                    // Save the account with correlation
                    await Task.FromResult(_repository.Save(account, command));
                    
                    _logger.LogInformation("Successfully withdrew {Amount} from account {AccountId} (Correlation: {CorrelationId})", 
                        command.Amount, command.AccountId, command.CorrelationId);
                    
                    // Return the new balance
                    return account.GetBalance();
                }
                catch (AggregateVersionException ex)
                {
                    // Handle concurrency conflict with retry logic
                    retryCount++;
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Failed to withdraw after {RetryCount} attempts due to concurrency conflicts (Correlation: {CorrelationId})", 
                            retryCount, command.CorrelationId);
                        throw new ConcurrencyException(
                            $"Failed to withdraw after {retryCount} attempts due to concurrency conflicts", ex);
                    }
                    
                    _logger.LogWarning(ex, "Concurrency conflict withdrawing from account {AccountId}, retry attempt {RetryCount} (Correlation: {CorrelationId})", 
                        command.AccountId, retryCount, command.CorrelationId);
                    
                    // Wait before retrying (with exponential backoff)
                    await Task.Delay(100 * (int)Math.Pow(2, retryCount));
                }
                catch (AggregateNotFoundException)
                {
                    _logger.LogWarning("Account {AccountId} not found for withdrawal (Correlation: {CorrelationId})", 
                        command.AccountId, command.CorrelationId);
                    throw new AccountNotFoundException($"Account {command.AccountId} not found");
                }
                catch (InvalidOperationException ex)
                {
                    // Business rule violations (like insufficient funds) are expected exceptions
                    _logger.LogWarning(ex, "Business rule violation when withdrawing {Amount} from account {AccountId} (Correlation: {CorrelationId})", 
                        command.Amount, command.AccountId, command.CorrelationId);
                    throw; // Rethrow for proper handling upstream
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error withdrawing {Amount} from account {AccountId} (Correlation: {CorrelationId})", 
                        command.Amount, command.AccountId, command.CorrelationId);
                    throw;
                }
            }
        }
    }
}
```

## Complete Example with Dependency Injection

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.Domain.Commands;
using MyApp.Infrastructure;
using MyApp.Application;

namespace MyApp.Examples
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Create and configure the host
            using var host = CreateHostBuilder(args).Build();
            
            // Start the host
            await host.StartAsync();
            
            // Get the example service and run it
            var example = host.Services.GetRequiredService<RepositoryExample>();
            await example.DemonstrateRepositoryOperationsAsync();
            
            // Stop the host
            await host.StopAsync();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register event store and repositories
                    services.AddEventStore(hostContext.Configuration);
                    
                    // Register application services
                    services.AddScoped<AccountService>();
                    services.AddScoped<CorrelatedAccountService>();
                    
                    // Register the example
                    services.AddScoped<RepositoryExample>();
                });
    }
    
    public class RepositoryExample
    {
        private readonly AccountService _accountService;
        private readonly CorrelatedAccountService _correlatedAccountService;
        private readonly ILogger<RepositoryExample> _logger;
        
        public RepositoryExample(
            AccountService accountService,
            CorrelatedAccountService correlatedAccountService,
            ILogger<RepositoryExample> logger)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _correlatedAccountService = correlatedAccountService ?? throw new ArgumentNullException(nameof(correlatedAccountService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task DemonstrateRepositoryOperationsAsync()
        {
            _logger.LogInformation("Starting repository operations demonstration");
            
            try
            {
                // Create a new account
                _logger.LogInformation("Creating a new account");
                var account = await _accountService.CreateAccountAsync("ACC-123", "John Doe");
                var accountId = account.Id;
                
                // Retrieve the account
                _logger.LogInformation("Retrieving the account");
                var retrievedAccount = await _accountService.GetAccountAsync(accountId);
                _logger.LogInformation("Retrieved account balance: {Balance}", retrievedAccount.GetBalance());
                
                // Update the account
                _logger.LogInformation("Depositing funds to the account");
                await _accountService.DepositAsync(accountId, 1000);
                
                // Try to get a non-existent account
                _logger.LogInformation("Trying to get a non-existent account");
                Account anotherAccount;
                if (await _accountService.TryGetAccountAsync(Guid.NewGuid(), out anotherAccount))
                {
                    _logger.LogInformation("Account found");
                }
                else
                {
                    _logger.LogInformation("Account not found");
                }
                
                // Using correlated repository
                _logger.LogInformation("Demonstrating correlated repository operations");
                
                // Create a command with a new correlation ID
                var createCommand = MessageBuilder.New(() => new CreateAccount(
                    Guid.NewGuid(),
                    "ACC-456",
                    "Jane Smith"
                ));
                
                // Process the create command
                await _correlatedAccountService.ProcessCreateAccountCommandAsync(createCommand);
                
                // Create a deposit command that continues the correlation chain
                var depositCommand = MessageBuilder.From(createCommand, () => 
                    new DepositFunds(createCommand.AccountId, 500));
                
                // Process the deposit command
                await _correlatedAccountService.ProcessDepositCommandAsync(depositCommand);
                
                // Create a withdraw command that continues the correlation chain
                var withdrawCommand = MessageBuilder.From(depositCommand, () => 
                    new WithdrawFunds(depositCommand.AccountId, 200));
                
                // Process the withdraw command with retry logic
                var newBalance = await _correlatedAccountService.WithdrawWithRetryAsync(withdrawCommand);
                _logger.LogInformation("New balance after withdrawal: {Balance}", newBalance);
                
                // Soft delete an account
                _logger.LogInformation("Soft deleting the account");
                await _accountService.DeleteAccountAsync(retrievedAccount);
                
                // Hard delete is typically not used in production but shown here for completeness
                _logger.LogInformation("Hard delete is available but not demonstrated");
                // await _accountService.HardDeleteAccountAsync(retrievedAccount);
                
                _logger.LogInformation("Repository operations demonstration completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during repository operations demonstration");
            }
        }
    }
}
```

## Key Concepts

### Repository Configuration with Dependency Injection

- **Strongly Typed Repositories**: Generic repositories with type parameters for aggregate and ID types
- **Dependency Injection**: Registering repositories and dependencies with the DI container
- **Configuration Management**: Using `IConfiguration` for connection settings and credentials
- **Logging Integration**: Structured logging with semantic information
- **Connection Management**: Configuring connections with appropriate retry and reconnect settings
- **Stream Name Builders**: Generating consistent stream names for aggregates

### Asynchronous Repository Operations

- **Task-Based Asynchronous Pattern**: Using `async`/`await` with `Task` return types
- **SaveAsync**: Persists new events from an aggregate to the event store asynchronously
- **GetByIdAsync**: Retrieves an aggregate by its ID asynchronously
- **TryGetByIdAsync**: Attempts to retrieve an aggregate by its ID asynchronously
- **UpdateAsync**: Updates an aggregate with the latest events asynchronously
- **DeleteAsync**: Marks an aggregate as deleted (soft delete) asynchronously
- **HardDeleteAsync**: Permanently deletes an aggregate (hard delete) asynchronously

### Correlated Repository Operations

- **Save with Correlation**: Persists new events with correlation information
- **GetById with Correlation**: Retrieves an aggregate with correlation information
- **Command Processing**: Processes commands with correlation tracking
- **Correlation Chain**: Maintaining correlation IDs across related operations

### Error Handling and Retry Logic

- **Structured Exception Handling**: Specific handling for different error types
- **Retry Patterns**: Implementing retry logic for transient failures
- **Exponential Backoff**: Increasing wait times between retries
- **Logging**: Comprehensive logging of errors and retry attempts
- **Custom Exceptions**: Domain-specific exceptions for better error handling

### Domain Service Layer

- **Service Pattern**: Encapsulating repository operations in domain services
- **Business Logic**: Implementing business rules and validations
- **Transaction Management**: Ensuring atomic operations
- **Correlation Tracking**: Maintaining correlation across service operations

## Best Practices

### Repository Design

1. **Strong Typing**: Use generic repositories with type parameters for aggregate and ID types
2. **Async All the Way**: Use asynchronous programming patterns consistently
3. **Interface Segregation**: Define focused repository interfaces for specific needs
4. **Dependency Injection**: Register repositories with the DI container
5. **Testability**: Design repositories to be easily mocked for testing

### Error Handling

1. **Comprehensive Exception Handling**: Handle all possible exceptions
2. **Retry Logic**: Implement retry logic for transient failures
3. **Logging**: Log errors with appropriate context and severity
4. **Custom Exceptions**: Create domain-specific exceptions
5. **Fail Fast**: Validate inputs early and throw appropriate exceptions

### Concurrency Management

1. **Optimistic Concurrency**: Handle version conflicts appropriately
2. **Retry Strategies**: Implement retry logic for concurrency conflicts
3. **Exponential Backoff**: Increase wait times between retries
4. **Max Retry Limit**: Set a maximum number of retries
5. **Conflict Resolution**: Implement strategies for resolving conflicts

### Correlation and Tracing

1. **Correlation Tracking**: Use correlated repositories for better traceability
2. **Message Builder**: Use `MessageBuilder` to maintain correlation chains
3. **Logging with Correlation**: Include correlation IDs in log messages
4. **End-to-End Tracing**: Track operations across system boundaries
5. **Audit Trails**: Use correlation for audit purposes

### Performance Optimization

1. **Connection Pooling**: Reuse connections to the event store
2. **Batch Operations**: Group related operations when possible
3. **Caching**: Implement appropriate caching strategies
4. **Snapshots**: Use snapshots for aggregates with many events
5. **Monitoring**: Implement performance monitoring

## Common Pitfalls

### Repository Implementation

1. **Ignoring Concurrency**: Failing to handle `AggregateVersionException` can lead to lost updates
2. **Synchronous Operations**: Blocking the thread with synchronous calls
3. **Missing Error Handling**: Not properly handling repository exceptions
4. **Tight Coupling**: Depending on concrete implementations instead of interfaces
5. **Connection Leaks**: Not properly managing connections

### Error Handling Issues

1. **Swallowing Exceptions**: Catching exceptions without proper handling or re-throwing
2. **Generic Exception Handling**: Using catch-all exception handlers
3. **Missing Retry Logic**: Not implementing retry for transient failures
4. **Infinite Retries**: Not setting a maximum retry limit
5. **Poor Logging**: Not logging enough context for debugging

### Concurrency Problems

1. **Lost Updates**: Not handling concurrency conflicts properly
2. **Deadlocks**: Improper use of locks or synchronization
3. **Race Conditions**: Not accounting for concurrent access
4. **Stale Data**: Working with outdated aggregate state
5. **Optimistic Lock Timeout**: Not setting appropriate timeouts for retries

### Correlation Issues

1. **Breaking Correlation**: Not maintaining correlation information across operations
2. **Missing Correlation IDs**: Not including correlation IDs in logs
3. **Correlation Leaks**: Using the wrong correlation ID for unrelated operations
4. **Missing Causation**: Not tracking the cause-effect relationship between operations
5. **Correlation Overuse**: Including correlation IDs in places where they're not needed

### Operational Concerns

1. **Hard Delete Overuse**: Using `HardDelete` when `Delete` would be more appropriate
2. **Connection Issues**: Not configuring connections with appropriate retry settings
3. **Missing Monitoring**: Not implementing proper monitoring and alerting
4. **Poor Diagnostics**: Not including enough information for troubleshooting
5. **Configuration Hardcoding**: Hardcoding connection strings and credentials

---

**Navigation**:
- [← Previous: Handling Commands and Generating Events](handling-commands-events.md)
- [↑ Back to Top](#saving-and-retrieving-aggregates)
- [→ Next: Setting Up Event Listeners](event-listeners.md)
