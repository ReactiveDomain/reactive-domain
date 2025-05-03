# Saving and Retrieving Aggregates

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to save and retrieve aggregates using repositories in Reactive Domain.

## Repository Configuration

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Persistence;
using ReactiveDomain.EventStore;

namespace MyApp.Infrastructure
{
    public class RepositoryConfiguration
    {
        public IRepository ConfigureRepository(string connectionString)
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
            
            // Create a repository
            var repository = new StreamStoreRepository(
                streamNameBuilder,
                connection,
                serializer);
                
            return repository;
        }
        
        public ICorrelatedRepository ConfigureCorrelatedRepository(IRepository repository)
        {
            // Create a correlated repository
            var correlatedRepository = new CorrelatedStreamStoreRepository(repository);
            
            return correlatedRepository;
        }
    }
}
```

## Basic Repository Operations

```csharp
using System;
using ReactiveDomain.Foundation;
using MyApp.Domain;

namespace MyApp.Application
{
    public class AccountRepository
    {
        private readonly IRepository _repository;
        
        public AccountRepository(IRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public void SaveAccount(Account account)
        {
            try
            {
                _repository.Save(account);
                Console.WriteLine($"Account {account.Id} saved successfully");
            }
            catch (AggregateVersionException ex)
            {
                Console.WriteLine($"Concurrency conflict: {ex.Message}");
                // Handle concurrency conflict
            }
        }
        
        public Account GetAccount(Guid accountId)
        {
            try
            {
                var account = _repository.GetById<Account>(accountId);
                return account;
            }
            catch (AggregateNotFoundException)
            {
                Console.WriteLine($"Account {accountId} not found");
                return null;
            }
            catch (AggregateDeletedException)
            {
                Console.WriteLine($"Account {accountId} has been deleted");
                return null;
            }
        }
        
        public bool TryGetAccount(Guid accountId, out Account account)
        {
            return _repository.TryGetById(accountId, out account);
        }
        
        public void UpdateAccount(ref Account account)
        {
            try
            {
                _repository.Update(ref account);
                Console.WriteLine($"Account {account.Id} updated successfully");
            }
            catch (AggregateVersionException ex)
            {
                Console.WriteLine($"Concurrency conflict: {ex.Message}");
                // Handle concurrency conflict
            }
        }
        
        public void DeleteAccount(Account account)
        {
            try
            {
                _repository.Delete(account);
                Console.WriteLine($"Account {account.Id} marked as deleted");
            }
            catch (AggregateVersionException ex)
            {
                Console.WriteLine($"Concurrency conflict: {ex.Message}");
                // Handle concurrency conflict
            }
        }
        
        public void HardDeleteAccount(Account account)
        {
            try
            {
                _repository.HardDelete(account);
                Console.WriteLine($"Account {account.Id} permanently deleted");
            }
            catch (AggregateVersionException ex)
            {
                Console.WriteLine($"Concurrency conflict: {ex.Message}");
                // Handle concurrency conflict
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
        
        public void SaveAccount(Account account, ICorrelatedMessage source)
        {
            try
            {
                _repository.Save(account, source);
                Console.WriteLine($"Account {account.Id} saved with correlation");
            }
            catch (AggregateVersionException ex)
            {
                Console.WriteLine($"Concurrency conflict: {ex.Message}");
                // Handle concurrency conflict
            }
        }
        
        public Account GetAccount(Guid accountId, ICorrelatedMessage source)
        {
            try
            {
                var account = _repository.GetById<Account>(accountId, source);
                return account;
            }
            catch (AggregateNotFoundException)
            {
                Console.WriteLine($"Account {accountId} not found");
                return null;
            }
            catch (AggregateDeletedException)
            {
                Console.WriteLine($"Account {accountId} has been deleted");
                return null;
            }
        }
        
        public void ProcessCreateAccountCommand(CreateAccount command)
        {
            // Create a new account with correlation
            var account = new Account(command.AccountId, command);
            
            // Initialize the account
            account.Create(command.AccountNumber, command.CustomerName);
            
            // Save the account with correlation
            _repository.Save(account, command);
        }
        
        public void ProcessDepositCommand(DepositFunds command)
        {
            try
            {
                // Load the account with correlation
                var account = _repository.GetById<Account>(command.AccountId, command);
                
                // Process the command
                account.Deposit(command.Amount);
                
                // Save the changes with correlation
                _repository.Save(account, command);
            }
            catch (AggregateNotFoundException)
            {
                // Handle not found case
                throw new InvalidOperationException($"Account {command.AccountId} not found");
            }
        }
    }
}
```

## Complete Example

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.Domain.Commands;
using MyApp.Infrastructure;
using MyApp.Application;

namespace MyApp.Examples
{
    public class RepositoryExample
    {
        public void DemonstrateRepositoryOperations()
        {
            // Configure repository
            var config = new RepositoryConfiguration();
            var repository = config.ConfigureRepository("localhost");
            var correlatedRepository = config.ConfigureCorrelatedRepository(repository);
            
            // Create repositories
            var accountRepo = new AccountRepository(repository);
            var correlatedAccountRepo = new CorrelatedAccountRepository(correlatedRepository);
            
            // Create a new account
            var accountId = Guid.NewGuid();
            var account = new Account(accountId);
            account.Create("ACC-123", "John Doe");
            
            // Save the account
            accountRepo.SaveAccount(account);
            
            // Retrieve the account
            var retrievedAccount = accountRepo.GetAccount(accountId);
            Console.WriteLine($"Retrieved account balance: {retrievedAccount.GetBalance()}");
            
            // Update the account
            retrievedAccount.Deposit(1000);
            accountRepo.SaveAccount(retrievedAccount);
            
            // Try to get an account
            Account anotherAccount;
            if (accountRepo.TryGetAccount(Guid.NewGuid(), out anotherAccount))
            {
                Console.WriteLine("Account found");
            }
            else
            {
                Console.WriteLine("Account not found");
            }
            
            // Using correlated repository
            var createCommand = new CreateAccount(Guid.NewGuid(), "ACC-456", "Jane Smith");
            correlatedAccountRepo.ProcessCreateAccountCommand(createCommand);
            
            var depositCommand = MessageBuilder.From(createCommand, () => 
                new DepositFunds(((CreateAccount)createCommand).AccountId, 500));
            correlatedAccountRepo.ProcessDepositCommand(depositCommand);
            
            // Delete an account
            accountRepo.DeleteAccount(retrievedAccount);
            
            // Hard delete an account
            // accountRepo.HardDeleteAccount(retrievedAccount);
        }
    }
}
```

## Key Concepts

### Repository Configuration

- **StreamNameBuilder**: Generates consistent stream names for aggregates
- **StreamStoreConnection**: Connects to the EventStoreDB
- **EventSerializer**: Serializes and deserializes events
- **StreamStoreRepository**: Implements the `IRepository` interface
- **CorrelatedStreamStoreRepository**: Implements the `ICorrelatedRepository` interface

### Basic Repository Operations

- **Save**: Persists new events from an aggregate to the event store
- **GetById**: Retrieves an aggregate by its ID
- **TryGetById**: Attempts to retrieve an aggregate by its ID
- **Update**: Updates an aggregate with the latest events from the event store
- **Delete**: Marks an aggregate as deleted (soft delete)
- **HardDelete**: Permanently deletes an aggregate (hard delete)

### Correlated Repository Operations

- **Save with Correlation**: Persists new events with correlation information
- **GetById with Correlation**: Retrieves an aggregate with correlation information
- **Command Processing**: Processes commands with correlation tracking

### Error Handling

- **AggregateNotFoundException**: Thrown when an aggregate is not found
- **AggregateDeletedException**: Thrown when an aggregate has been deleted
- **AggregateVersionException**: Thrown when there's a concurrency conflict

## Best Practices

1. **Error Handling**: Implement proper error handling for repository operations
2. **Correlation Tracking**: Use correlated repositories for better traceability
3. **Optimistic Concurrency**: Handle version conflicts appropriately
4. **Repository Abstraction**: Depend on the repository interfaces, not concrete implementations
5. **Connection Management**: Configure connections with appropriate retry and reconnect settings
6. **Stream Naming**: Use a consistent stream naming strategy

## Common Pitfalls

1. **Ignoring Concurrency**: Failing to handle `AggregateVersionException` can lead to lost updates
2. **Missing Error Handling**: Not properly handling repository exceptions
3. **Connection Issues**: Not configuring connections with appropriate retry settings
4. **Breaking Correlation**: Not maintaining correlation information across operations
5. **Hard Delete Overuse**: Using `HardDelete` when `Delete` would be more appropriate

---

**Navigation**:
- [← Previous: Handling Commands and Generating Events](handling-commands-events.md)
- [↑ Back to Top](#saving-and-retrieving-aggregates)
- [→ Next: Setting Up Event Listeners](event-listeners.md)
