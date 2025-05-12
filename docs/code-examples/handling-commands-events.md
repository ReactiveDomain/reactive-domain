# Handling Commands and Generating Events

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to handle commands and generate events in Reactive Domain, following current best practices.

## Command Definitions

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace MyApp.Domain.Commands
{
    [Serializable]
    public class CreateAccount : Command<Guid>
    {
        public readonly Guid AccountId;
        public readonly string AccountNumber;
        public readonly string CustomerName;
        public readonly DateTime Timestamp;
        
        // Constructor for creating a new command
        public CreateAccount(Guid accountId, string accountNumber, string customerName)
            : base(accountId) // Pass ID to base constructor
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrWhiteSpace(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with correlation from source message
        public CreateAccount(Guid accountId, string accountNumber, string customerName, ICorrelatedMessage source)
            : base(accountId, source) // Pass ID and source for correlation
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrWhiteSpace(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class DepositFunds : Command<Guid>
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        public readonly DateTime Timestamp;
        
        public DepositFunds(Guid accountId, decimal amount)
            : base(accountId)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with correlation from source message
        public DepositFunds(Guid accountId, decimal amount, ICorrelatedMessage source)
            : base(accountId, source)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class WithdrawFunds : Command<Guid>
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        public readonly DateTime Timestamp;
        
        public WithdrawFunds(Guid accountId, decimal amount)
            : base(accountId)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with correlation from source message
        public WithdrawFunds(Guid accountId, decimal amount, ICorrelatedMessage source)
            : base(accountId, source)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class CloseAccount : Command<Guid>
    {
        public readonly Guid AccountId;
        public readonly DateTime Timestamp;
        
        public CloseAccount(Guid accountId)
            : base(accountId)
        {
            AccountId = accountId;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with correlation from source message
        public CloseAccount(Guid accountId, ICorrelatedMessage source)
            : base(accountId, source)
        {
            AccountId = accountId;
            Timestamp = DateTime.UtcNow;
        }
    }
}
```

## Command Handlers

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain.Commands;

namespace MyApp.Domain.Handlers
{
    public class AccountCommandHandler : 
        IHandleCommand<CreateAccount>,
        IHandleCommand<DepositFunds>,
        IHandleCommand<WithdrawFunds>,
        IHandleCommand<CloseAccount>
    {
        private readonly IRepository<Account, Guid> _repository;
        private readonly ILogger<AccountCommandHandler> _logger;
        
        public AccountCommandHandler(
            IRepository<Account, Guid> repository,
            ILogger<AccountCommandHandler> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task HandleAsync(CreateAccount command)
        {
            _logger.LogInformation("Creating account {AccountId} for customer {CustomerName}", 
                command.AccountId, command.CustomerName);
                
            try
            {
                // Create a new account with correlation
                var account = new Account(command.AccountId, command);
                
                // Initialize the account
                account.Create(command.AccountNumber, command.CustomerName);
                
                // Save the account
                await _repository.SaveAsync(account);
                
                _logger.LogInformation("Successfully created account {AccountId}", command.AccountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account {AccountId}", command.AccountId);
                throw;
            }
        }
        
        public async Task HandleAsync(DepositFunds command)
        {
            _logger.LogInformation("Depositing {Amount} to account {AccountId}", 
                command.Amount, command.AccountId);
                
            try
            {
                // Load the account
                var account = await _repository.GetByIdAsync(command.AccountId);
                
                // Process the command
                account.Deposit(command.Amount);
                
                // Save the account
                await _repository.SaveAsync(account);
                
                _logger.LogInformation("Successfully deposited {Amount} to account {AccountId}", 
                    command.Amount, command.AccountId);
            }
            catch (AggregateNotFoundException)
            {
                _logger.LogWarning("Account {AccountId} not found for deposit", command.AccountId);
                throw new InvalidOperationException($"Account {command.AccountId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error depositing {Amount} to account {AccountId}", 
                    command.Amount, command.AccountId);
                throw;
            }
        }
        
        public async Task HandleAsync(WithdrawFunds command)
        {
            _logger.LogInformation("Withdrawing {Amount} from account {AccountId}", 
                command.Amount, command.AccountId);
                
            try
            {
                // Load the account
                var account = await _repository.GetByIdAsync(command.AccountId);
                
                // Process the command
                account.Withdraw(command.Amount);
                
                // Save the account
                await _repository.SaveAsync(account);
                
                _logger.LogInformation("Successfully withdrew {Amount} from account {AccountId}", 
                    command.Amount, command.AccountId);
            }
            catch (AggregateNotFoundException)
            {
                _logger.LogWarning("Account {AccountId} not found for withdrawal", command.AccountId);
                throw new InvalidOperationException($"Account {command.AccountId} not found");
            }
            catch (InvalidOperationException ex)
            {
                // Business rule violations (like insufficient funds) are expected exceptions
                _logger.LogWarning(ex, "Business rule violation when withdrawing {Amount} from account {AccountId}", 
                    command.Amount, command.AccountId);
                throw; // Rethrow for proper handling upstream
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing {Amount} from account {AccountId}", 
                    command.Amount, command.AccountId);
                throw;
            }
        }
        
        public async Task HandleAsync(CloseAccount command)
        {
            _logger.LogInformation("Closing account {AccountId}", command.AccountId);
                
            try
            {
                // Load the account
                var account = await _repository.GetByIdAsync(command.AccountId);
                
                // Process the command
                account.Close();
                
                // Save the account
                await _repository.SaveAsync(account);
                
                _logger.LogInformation("Successfully closed account {AccountId}", command.AccountId);
            }
            catch (AggregateNotFoundException)
            {
                _logger.LogWarning("Account {AccountId} not found for closing", command.AccountId);
                throw new InvalidOperationException($"Account {command.AccountId} not found");
            }
            catch (InvalidOperationException ex)
            {
                // Business rule violations are expected exceptions
                _logger.LogWarning(ex, "Business rule violation when closing account {AccountId}", command.AccountId);
                throw; // Rethrow for proper handling upstream
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing account {AccountId}", command.AccountId);
                throw;
            }
        }
    }
}
```

## Using MessageBuilder for Correlation

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Messaging;
using MyApp.Domain.Commands;

namespace MyApp.Domain.Examples
{
    public class MessageBuilderExample
    {
        private readonly ICommandBus _commandBus;
        private readonly ILogger<MessageBuilderExample> _logger;
        
        public MessageBuilderExample(
            ICommandBus commandBus,
            ILogger<MessageBuilderExample> logger)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task DemonstrateMessageBuilderAsync()
        {
            // Generate a unique account ID
            var accountId = Guid.NewGuid();
            
            // Create a new command that starts a correlation chain
            var createCommand = MessageBuilder.New(() => new CreateAccount(
                accountId,
                "ACC-123",
                "John Doe"
            ));
            
            // Log correlation information
            _logger.LogInformation("Starting correlation chain with ID {CorrelationId}", 
                createCommand.CorrelationId);
            
            // Send the command
            await _commandBus.SendAsync(createCommand);
            
            // Create a new command that continues the correlation chain
            var depositCommand = MessageBuilder.From(createCommand, () => new DepositFunds(
                accountId,
                100.00m
            ));
            
            // Send the command
            await _commandBus.SendAsync(depositCommand);
            
            // Create another command in the chain
            var withdrawCommand = MessageBuilder.From(depositCommand, () => new WithdrawFunds(
                accountId,
                50.00m
            ));
            
            // Send the command
            await _commandBus.SendAsync(withdrawCommand);
            
            // Log correlation information
            _logger.LogInformation("Command chain completed with correlation ID {CorrelationId}", 
                createCommand.CorrelationId);
            
            // Demonstrate correlation ID tracking
            _logger.LogDebug("Correlation chain details:");
            _logger.LogDebug("Create Command: CorrelationId={CorrelationId}, CausationId={CausationId}", 
                createCommand.CorrelationId, createCommand.CausationId);
            _logger.LogDebug("Deposit Command: CorrelationId={CorrelationId}, CausationId={CausationId}", 
                depositCommand.CorrelationId, depositCommand.CausationId);
            _logger.LogDebug("Withdraw Command: CorrelationId={CorrelationId}, CausationId={CausationId}", 
                withdrawCommand.CorrelationId, withdrawCommand.CausationId);
        }
    }
}
```

## Registering Command Handlers with Dependency Injection

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain.Commands;
using MyApp.Domain.Handlers;

namespace MyApp.Infrastructure
{
    public static class CommandHandlerRegistration
    {
        public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
        {
            // Register command bus
            services.AddSingleton<ICommandBus, CommandBus>();
            
            // Register command handlers
            services.AddScoped<AccountCommandHandler>();
            
            // Register handler registrations
            services.AddSingleton<ICommandHandlerRegistration, AccountCommandHandlerRegistration>();
            
            return services;
        }
    }
    
    public class AccountCommandHandlerRegistration : ICommandHandlerRegistration
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccountCommandHandlerRegistration> _logger;
        
        public AccountCommandHandlerRegistration(
            IServiceProvider serviceProvider,
            ILogger<AccountCommandHandlerRegistration> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        
        public void RegisterHandlers(ICommandBus commandBus)
        {
            _logger.LogInformation("Registering account command handlers");
            
            // Use factory method to create handler with scoped lifetime
            commandBus.Subscribe<CreateAccount>(cmd => {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<AccountCommandHandler>();
                return handler.HandleAsync(cmd);
            });
            
            commandBus.Subscribe<DepositFunds>(cmd => {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<AccountCommandHandler>();
                return handler.HandleAsync(cmd);
            });
            
            commandBus.Subscribe<WithdrawFunds>(cmd => {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<AccountCommandHandler>();
                return handler.HandleAsync(cmd);
            });
            
            commandBus.Subscribe<CloseAccount>(cmd => {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<AccountCommandHandler>();
                return handler.HandleAsync(cmd);
            });
            
            _logger.LogInformation("Account command handlers registered successfully");
        }
    }
    
    // Interface for command handler registration
    public interface ICommandHandlerRegistration
    {
        void RegisterHandlers(ICommandBus commandBus);
    }
}
```

## Application Startup with Dependency Injection

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.Infrastructure;

namespace MyApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register event store
                    services.AddSingleton<IEventStore>(provider => 
                    {
                        var logger = provider.GetRequiredService<ILogger<Program>>();
                        logger.LogInformation("Configuring event store");
                        
                        var connectionString = hostContext.Configuration.GetConnectionString("EventStore");
                        return new EventStore(connectionString);
                    });
                    
                    // Register repository
                    services.AddSingleton<IRepository<Account, Guid>>(provider =>
                    {
                        var eventStore = provider.GetRequiredService<IEventStore>();
                        return new Repository<Account, Guid>(eventStore);
                    });
                    
                    // Register command handlers
                    services.AddCommandHandlers();
                    
                    // Register command bus initialization
                    services.AddHostedService<CommandBusInitializer>();
                });
    }
    
    public class CommandBusInitializer : IHostedService
    {
        private readonly ICommandBus _commandBus;
        private readonly IEnumerable<ICommandHandlerRegistration> _registrations;
        private readonly ILogger<CommandBusInitializer> _logger;
        
        public CommandBusInitializer(
            ICommandBus commandBus,
            IEnumerable<ICommandHandlerRegistration> registrations,
            ILogger<CommandBusInitializer> logger)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing command bus");
            
            foreach (var registration in _registrations)
            {
                registration.RegisterHandlers(_commandBus);
            }
            
            _logger.LogInformation("Command bus initialized successfully");
            
            return Task.CompletedTask;
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
```

## Sending Commands

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain.Commands;

namespace MyApp.Examples
{
    public class AccountService
    {
        private readonly ICommandBus _commandBus;
        private readonly ILogger<AccountService> _logger;
        
        public AccountService(
            ICommandBus commandBus,
            ILogger<AccountService> logger)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<Guid> CreateNewAccountAsync(string accountNumber, string customerName)
        {
            // Generate a unique ID for the account
            var accountId = Guid.NewGuid();
            
            _logger.LogInformation("Creating new account for customer {CustomerName}", customerName);
            
            // Create a new account command
            var createCommand = new CreateAccount(
                accountId,
                accountNumber,
                customerName
            );
            
            // Send the command and await the result
            await _commandBus.SendAsync(createCommand);
            
            _logger.LogInformation("Account {AccountId} created successfully", accountId);
            
            return accountId;
        }
        
        public async Task DepositFundsAsync(Guid accountId, decimal amount, ICorrelatedMessage sourceMessage = null)
        {            
            _logger.LogInformation("Depositing {Amount} to account {AccountId}", amount, accountId);
            
            // Create deposit command with correlation if source message provided
            DepositFunds depositCommand;
            
            if (sourceMessage != null)
            {
                depositCommand = new DepositFunds(accountId, amount, sourceMessage);
            }
            else
            {
                depositCommand = new DepositFunds(accountId, amount);
            }
            
            // Send the command and await the result
            await _commandBus.SendAsync(depositCommand);
            
            _logger.LogInformation("Successfully deposited {Amount} to account {AccountId}", amount, accountId);
        }
        
        public async Task WithdrawFundsAsync(Guid accountId, decimal amount, ICorrelatedMessage sourceMessage = null)
        {            
            _logger.LogInformation("Withdrawing {Amount} from account {AccountId}", amount, accountId);
            
            try
            {
                // Create withdraw command with correlation if source message provided
                WithdrawFunds withdrawCommand;
                
                if (sourceMessage != null)
                {
                    withdrawCommand = new WithdrawFunds(accountId, amount, sourceMessage);
                }
                else
                {
                    withdrawCommand = new WithdrawFunds(accountId, amount);
                }
                
                // Send the command and await the result
                await _commandBus.SendAsync(withdrawCommand);
                
                _logger.LogInformation("Successfully withdrew {Amount} from account {AccountId}", amount, accountId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Business rule violation when withdrawing from account {AccountId}", accountId);
                throw; // Rethrow for proper handling upstream
            }
        }
        
        public async Task CloseAccountAsync(Guid accountId, ICorrelatedMessage sourceMessage = null)
        {            
            _logger.LogInformation("Closing account {AccountId}", accountId);
            
            try
            {
                // Create close command with correlation if source message provided
                CloseAccount closeCommand;
                
                if (sourceMessage != null)
                {
                    closeCommand = new CloseAccount(accountId, sourceMessage);
                }
                else
                {
                    closeCommand = new CloseAccount(accountId);
                }
                
                // Send the command and await the result
                await _commandBus.SendAsync(closeCommand);
                
                _logger.LogInformation("Successfully closed account {AccountId}", accountId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Business rule violation when closing account {AccountId}", accountId);
                throw; // Rethrow for proper handling upstream
            }
        }
        
        // Example of a business operation that uses multiple commands with correlation
        public async Task ProcessMonthlyFeeAsync(Guid accountId, decimal feeAmount)
        {
            _logger.LogInformation("Processing monthly fee of {Amount} for account {AccountId}", 
                feeAmount, accountId);
            
            // Create the initial command
            var withdrawCommand = new WithdrawFunds(accountId, feeAmount);
            
            // Send the command and maintain correlation
            await _commandBus.SendAsync(withdrawCommand);
            
            // Log the successful fee processing
            _logger.LogInformation("Monthly fee processed successfully for account {AccountId}", accountId);
        }
    }
}

## Key Concepts

### Command Structure

- Commands represent intentions to change the system state
- Commands are named in the imperative tense (e.g., `CreateAccount`, `DepositFunds`)
- Commands contain all the data needed to perform the operation
- Commands implement the `ICommand` interface or inherit from the `Command` base class

### Command Design

1. **Strongly Typed Commands**: Commands now inherit from `Command<TId>` where `TId` is the type of the aggregate ID. This provides type safety and makes the code more maintainable.

2. **Immutability**: Commands are immutable data structures with readonly properties, ensuring they cannot be changed after creation.

3. **Validation at Creation**: Commands validate their parameters at construction time, failing fast if invalid data is provided.

4. **Timestamps**: Including timestamps in commands provides valuable metadata for auditing and debugging.

5. **Serialization Attribute**: The `[Serializable]` attribute ensures commands can be properly serialized for storage or transmission across process boundaries.

### Command Correlation

1. **Source-Based Correlation**: Commands accept an `ICorrelatedMessage` source in their constructors, automatically propagating correlation and causation IDs.

2. **MessageBuilder Pattern**: The `MessageBuilder` class simplifies creating correlated message chains, ensuring proper correlation ID propagation.

3. **Correlation Chain**: Correlation IDs remain constant throughout a business transaction, while causation IDs form a chain showing the sequence of messages.

### Command Handling

1. **Asynchronous Processing**: Command handlers use `async/await` with `Task` return types for non-blocking I/O operations.

2. **Strongly Typed Repository**: The repository is now generic (`IRepository<TAggregate, TId>`), providing type safety and eliminating casting.

3. **Comprehensive Error Handling**: Command handlers include structured exception handling with specific handling for different error types.

4. **Logging**: Extensive logging provides visibility into the command handling process, including information, warnings, and errors.

### Dependency Injection

1. **Service Registration**: Command handlers and dependencies are registered with the DI container, promoting loose coupling.

2. **Scoped Lifetime**: Command handlers use scoped lifetime, ensuring proper resource management and isolation.

3. **Factory Pattern**: Command handler factories create handlers with the appropriate scope when needed.

4. **Hosted Service**: The `CommandBusInitializer` runs at application startup to register all command handlers.

### Application Services

1. **Task-Based Interface**: Application services expose `async` methods returning `Task` or `Task<T>`, allowing for non-blocking calls.

2. **Correlation Support**: Services accept optional `ICorrelatedMessage` parameters to maintain correlation across operations.

3. **Business Operations**: Higher-level methods encapsulate business operations that may involve multiple commands.

4. **Structured Logging**: Services use structured logging with semantic logging patterns for better observability.

### Best Practices

1. **Fail Fast**: Validate inputs early and throw appropriate exceptions rather than allowing invalid state.

2. **Separation of Concerns**: Commands, command handlers, and application services each have clear, distinct responsibilities.

3. **Explicit Dependencies**: Dependencies are explicitly declared and injected, making the code more testable and maintainable.

4. **Consistent Error Handling**: Exceptions are caught, logged, and rethrown at appropriate levels of the stack.

5. **Async All the Way**: Asynchronous programming patterns are used consistently throughout the codebase.

6. **Structured Logging**: Logging uses structured formats with semantic information rather than string concatenation.

7. **Strong Typing**: Generic type parameters and strong typing are used throughout to catch errors at compile time rather than runtime.

### Correlation and Causation

- **Correlation ID**: A unique identifier that remains constant throughout a business transaction or user request, allowing you to trace all related messages across system boundaries.

- **Causation ID**: A unique identifier that creates a direct link between a message and the message that caused it, forming a chain of causality.

- **Message ID**: Each message has its own unique identifier that can be used as the causation ID for subsequent messages.

- **Correlation Flow**:
  1. The first message in a chain generates a new correlation ID (typically a GUID)
  2. Subsequent messages inherit the same correlation ID
  3. Each message's ID becomes the causation ID for the next message
  4. This creates a traceable path through the system

- **ICorrelatedMessage Interface**: All commands and events implement this interface, which provides:
  - `MessageId`: Unique identifier for this specific message
  - `CorrelationId`: Identifier linking related messages in a transaction
  - `CausationId`: Identifier of the message that caused this one

- **MessageBuilder Class**: A utility that simplifies creating properly correlated message chains:
  - `MessageBuilder.New()`: Creates a new message with fresh correlation information
  - `MessageBuilder.From()`: Creates a new message that continues an existing correlation chain

- **Benefits**:
  - Distributed tracing across microservices
  - Debugging complex workflows
  - Auditing capabilities
  - Performance monitoring
  - Root cause analysis
- Correlation IDs track related messages across the system

### Command Bus

- **Purpose**: The command bus routes commands to their appropriate handlers, decoupling the command sender from the command handler implementation.

- **Asynchronous API**: Modern command buses support asynchronous processing with `SendAsync` methods returning `Task` or `Task<TResult>` for command results.

- **Dependency Injection Integration**: Command buses are registered with the DI container and handlers are resolved at runtime.

- **Scoped Handlers**: Command handlers are typically created with scoped lifetime to ensure proper resource management and isolation.

- **Factory Pattern**: Handler factories create handlers with the appropriate scope when needed, allowing for proper disposal of resources.

- **Middleware Support**: Command buses can support middleware for cross-cutting concerns such as:
  - Logging
  - Validation
  - Authentication/Authorization
  - Transaction management
  - Exception handling
  - Retry policies
  - Circuit breakers
  - Performance monitoring

- **Error Handling**: Command buses provide structured error handling with specific handling for different error types.

- **Correlation Propagation**: Command buses automatically propagate correlation and causation IDs through the message chain.

- **Command Results**: Modern command buses can return results from command execution, useful for returning generated IDs or other information.

## Best Practices

### Command Design

1. **Strong Typing**: Use generic `Command<TId>` base classes to ensure type safety and make the code more maintainable.

2. **Immutability**: Design commands as immutable data structures with readonly properties to prevent unexpected changes.

3. **Validation at Creation**: Validate command parameters at construction time to fail fast if invalid data is provided.

4. **Include Timestamps**: Add timestamps to commands for auditing, debugging, and time-based business rules.

5. **Single Responsibility**: Each command should represent a single operation with a clear intent.

6. **Serialization Support**: Ensure commands can be properly serialized for storage or transmission across process boundaries.

### Command Handling

1. **Asynchronous Processing**: Use `async/await` with `Task` return types for non-blocking I/O operations.

2. **Comprehensive Error Handling**: Implement structured exception handling with specific handling for different error types.

3. **Structured Logging**: Use structured logging with semantic information rather than string concatenation.

4. **Separate Command Handling from Business Logic**: Command handlers should delegate to the domain model for business logic implementation.

5. **Transactional Integrity**: Ensure that command handling is atomic - either all changes are applied or none.

6. **Idempotency**: Design command handlers to be idempotent where possible, allowing safe retries.

### Dependency Management

1. **Explicit Dependencies**: Declare dependencies explicitly and inject them, making the code more testable and maintainable.

2. **Appropriate Lifetimes**: Use the correct lifetime scope for each dependency (singleton, scoped, transient).

3. **Factory Pattern**: Use factory methods when dependencies need to be created with specific scopes or configurations.

4. **Interface-Based Design**: Program to interfaces rather than concrete implementations to support testability and flexibility.

### Error Handling

1. **Domain Exceptions**: Create specific exception types for domain rule violations to distinguish them from technical errors.

2. **Fail Fast**: Validate inputs early and throw appropriate exceptions rather than allowing invalid state.

3. **Consistent Approach**: Handle exceptions consistently across all command handlers.

4. **Error Logging**: Log errors with appropriate context to aid debugging and monitoring.

5. **Don't Swallow Exceptions**: Avoid catching exceptions without proper handling or re-throwing.

### Performance Considerations

1. **Async All the Way**: Use asynchronous programming patterns consistently throughout the codebase.

2. **Minimize Database Calls**: Structure command handlers to minimize the number of database operations.

3. **Consider Batching**: For high-volume scenarios, consider batching commands or using bulk operations.

4. **Optimize Repository Access**: Use efficient repository implementations with appropriate caching strategies.

5. **Monitor Performance**: Implement performance monitoring to identify bottlenecks.

### Testing

1. **Unit Test Command Validation**: Test that commands properly validate their inputs.

2. **Unit Test Command Handlers**: Test command handlers with mocked dependencies.

3. **Integration Test Command Flow**: Test the full command flow from sending to handling.

4. **Test Error Scenarios**: Ensure error handling works as expected by testing failure scenarios.

5. **Test Correlation**: Verify that correlation IDs are properly propagated.

### Security

1. **Authorization**: Implement proper authorization checks before processing commands.

2. **Input Validation**: Validate all command inputs to prevent injection attacks.

3. **Audit Logging**: Log command execution for audit purposes.

4. **Principle of Least Privilege**: Ensure command handlers only have access to the resources they need.

5. **Secure Communication**: Use secure channels for transmitting commands between system boundaries.

## Common Pitfalls

### Design Pitfalls

1. **Complex Commands**: Creating commands that do too many things or change multiple aggregates, violating the single responsibility principle.

2. **Anemic Commands**: Commands that lack proper validation or don't include all necessary data, requiring additional lookups.

3. **Mutable Commands**: Allowing commands to be modified after creation, leading to inconsistent state and race conditions.

4. **Missing Strong Typing**: Using primitive types for IDs instead of strongly-typed IDs, reducing type safety and increasing the chance of errors.

5. **Inconsistent Naming**: Using inconsistent naming conventions for commands, making the codebase harder to understand and maintain.

### Implementation Pitfalls

1. **Ignoring Correlation**: Failing to maintain correlation chains, making it difficult to trace related operations across the system.

2. **Synchronous Processing**: Blocking I/O operations in command handlers, reducing system throughput and responsiveness.

3. **Poor Error Handling**: Catching exceptions without proper logging or re-throwing, hiding errors and making debugging difficult.

4. **Direct Repository Access**: Bypassing the repository pattern and accessing the data store directly, breaking encapsulation.

5. **Forgetting to Save**: Modifying aggregates but forgetting to save them to the repository, losing changes.

### Architectural Pitfalls

1. **Tight Coupling**: Directly instantiating dependencies instead of using dependency injection, making the code harder to test and maintain.

2. **Business Logic in Handlers**: Implementing business logic in command handlers instead of in the domain model, violating separation of concerns.

3. **Inconsistent Async Patterns**: Mixing synchronous and asynchronous code, leading to potential deadlocks and performance issues.

4. **Missing Validation**: Not validating command data, allowing invalid state to propagate through the system.

5. **Ignoring Idempotency**: Not designing for idempotency, making it unsafe to retry failed operations.

### Performance Pitfalls

1. **N+1 Query Problem**: Loading related entities one by one instead of in a single query, causing performance issues.

2. **Excessive Logging**: Logging too much information or at too high a level, impacting performance and generating noise.

3. **Inefficient Repository Implementation**: Using inefficient repository implementations without proper caching or optimization.

4. **Chatty Interfaces**: Creating many small commands instead of batching related operations, increasing network overhead.

5. **Blocking Threads**: Using `.Result` or `.Wait()` on tasks, potentially causing thread pool starvation or deadlocks.

---

**Navigation**:
- [← Previous: Creating a New Aggregate Root](creating-aggregate-root.md)
- [↑ Back to Top](#handling-commands-and-generating-events)
- [→ Next: Saving and Retrieving Aggregates](saving-retrieving-aggregates.md)
