# Handling Commands and Generating Events

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to handle commands and generate events in Reactive Domain.

## Command Definitions

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace MyApp.Domain.Commands
{
    public class CreateAccount : Command
    {
        public readonly Guid AccountId;
        public readonly string AccountNumber;
        public readonly string CustomerName;
        
        public CreateAccount(Guid accountId, string accountNumber, string customerName)
            : base()
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
        }
        
        // Constructor with explicit correlation
        public CreateAccount(Guid accountId, string accountNumber, string customerName,
                            Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
        }
    }
    
    public class DepositFunds : Command
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        
        public DepositFunds(Guid accountId, decimal amount)
            : base()
        {
            AccountId = accountId;
            Amount = amount;
        }
        
        // Constructor with explicit correlation
        public DepositFunds(Guid accountId, decimal amount,
                           Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
    
    public class WithdrawFunds : Command
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        
        public WithdrawFunds(Guid accountId, decimal amount)
            : base()
        {
            AccountId = accountId;
            Amount = amount;
        }
        
        // Constructor with explicit correlation
        public WithdrawFunds(Guid accountId, decimal amount,
                            Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
    
    public class CloseAccount : Command
    {
        public readonly Guid AccountId;
        
        public CloseAccount(Guid accountId)
            : base()
        {
            AccountId = accountId;
        }
        
        // Constructor with explicit correlation
        public CloseAccount(Guid accountId, Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
        }
    }
}
```

## Command Handlers

```csharp
using System;
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
        private readonly IRepository _repository;
        
        public AccountCommandHandler(IRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public void Handle(CreateAccount command)
        {
            // Create a new account with correlation
            var account = new Account(command.AccountId, command);
            
            // Initialize the account
            account.Create(command.AccountNumber, command.CustomerName);
            
            // Save the account
            _repository.Save(account);
        }
        
        public void Handle(DepositFunds command)
        {
            try
            {
                // Load the account
                var account = _repository.GetById<Account>(command.AccountId);
                
                // Process the command
                account.Deposit(command.Amount);
                
                // Save the changes
                _repository.Save(account);
            }
            catch (AggregateNotFoundException)
            {
                // Handle not found case
                throw new InvalidOperationException($"Account {command.AccountId} not found");
            }
        }
        
        public void Handle(WithdrawFunds command)
        {
            try
            {
                // Load the account
                var account = _repository.GetById<Account>(command.AccountId);
                
                // Process the command
                account.Withdraw(command.Amount);
                
                // Save the changes
                _repository.Save(account);
            }
            catch (AggregateNotFoundException)
            {
                // Handle not found case
                throw new InvalidOperationException($"Account {command.AccountId} not found");
            }
            catch (InvalidOperationException ex)
            {
                // Rethrow business rule violations
                throw;
            }
        }
        
        public void Handle(CloseAccount command)
        {
            try
            {
                // Load the account
                var account = _repository.GetById<Account>(command.AccountId);
                
                // Process the command
                account.Close();
                
                // Save the changes
                _repository.Save(account);
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

## Using MessageBuilder for Correlation

```csharp
using System;
using ReactiveDomain.Messaging;
using MyApp.Domain.Commands;

namespace MyApp.Domain.Examples
{
    public class MessageBuilderExample
    {
        public void DemonstrateMessageBuilder()
        {
            // Create a new command that starts a correlation chain
            var createCommand = MessageBuilder.New(() => new CreateAccount(
                Guid.NewGuid(),
                "ACC-123",
                "John Doe"
            ));
            
            // Create a command from an existing command (maintains correlation)
            var depositCommand = MessageBuilder.From(createCommand, () => new DepositFunds(
                ((CreateAccount)createCommand).AccountId,
                1000
            ));
            
            // Create another command in the same chain
            var withdrawCommand = MessageBuilder.From(depositCommand, () => new WithdrawFunds(
                ((DepositFunds)depositCommand).AccountId,
                500
            ));
            
            // Correlation IDs are maintained throughout the chain
            Console.WriteLine($"Create Command Correlation ID: {createCommand.CorrelationId}");
            Console.WriteLine($"Deposit Command Correlation ID: {depositCommand.CorrelationId}");
            Console.WriteLine($"Withdraw Command Correlation ID: {withdrawCommand.CorrelationId}");
            
            // Causation IDs form a chain
            Console.WriteLine($"Create Command Causation ID: {createCommand.CausationId}");
            Console.WriteLine($"Deposit Command Causation ID: {depositCommand.CausationId}");
            Console.WriteLine($"Withdraw Command Causation ID: {withdrawCommand.CausationId}");
        }
    }
}
```

## Registering Command Handlers

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain.Commands;
using MyApp.Domain.Handlers;

namespace MyApp.Infrastructure
{
    public class CommandBusSetup
    {
        public ICommandBus ConfigureCommandBus(IRepository repository)
        {
            // Create a command bus
            var commandBus = new CommandBus();
            
            // Create command handlers
            var accountCommandHandler = new AccountCommandHandler(repository);
            
            // Register command handlers
            commandBus.Subscribe<CreateAccount>(accountCommandHandler);
            commandBus.Subscribe<DepositFunds>(accountCommandHandler);
            commandBus.Subscribe<WithdrawFunds>(accountCommandHandler);
            commandBus.Subscribe<CloseAccount>(accountCommandHandler);
            
            return commandBus;
        }
    }
}
```

## Sending Commands

```csharp
using System;
using ReactiveDomain.Messaging;
using MyApp.Domain.Commands;

namespace MyApp.Application
{
    public class AccountService
    {
        private readonly ICommandBus _commandBus;
        
        public AccountService(ICommandBus commandBus)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
        }
        
        public Guid CreateAccount(string accountNumber, string customerName)
        {
            var accountId = Guid.NewGuid();
            
            var command = new CreateAccount(accountId, accountNumber, customerName);
            _commandBus.Send(command);
            
            return accountId;
        }
        
        public void DepositFunds(Guid accountId, decimal amount)
        {
            var command = new DepositFunds(accountId, amount);
            _commandBus.Send(command);
        }
        
        public void WithdrawFunds(Guid accountId, decimal amount)
        {
            var command = new WithdrawFunds(accountId, amount);
            _commandBus.Send(command);
        }
        
        public void CloseAccount(Guid accountId)
        {
            var command = new CloseAccount(accountId);
            _commandBus.Send(command);
        }
    }
}
```

## Key Concepts

### Command Structure

- Commands represent intentions to change the system state
- Commands are named in the imperative tense (e.g., `CreateAccount`, `DepositFunds`)
- Commands contain all the data needed to perform the operation
- Commands implement the `ICommand` interface or inherit from the `Command` base class

### Command Handlers

- Command handlers implement the `IHandleCommand<T>` interface
- They load the appropriate aggregate from the repository
- They invoke the appropriate method on the aggregate
- They save the aggregate back to the repository

### Correlation and Causation

- Commands can be correlated using `MessageBuilder`
- `MessageBuilder.New()` starts a new correlation chain
- `MessageBuilder.From()` continues an existing correlation chain
- Correlation IDs track related messages across the system

### Command Bus

- The command bus routes commands to their handlers
- Handlers are registered with the bus using the `Subscribe` method
- Commands are sent to the bus using the `Send` method

## Best Practices

1. **Single Responsibility**: Each command should represent a single operation
2. **Immutable Commands**: Make all command properties read-only
3. **Validation**: Validate commands before processing them
4. **Error Handling**: Implement proper error handling in command handlers
5. **Correlation**: Use `MessageBuilder` to maintain correlation chains
6. **Command Naming**: Use imperative verb phrases for command names

## Common Pitfalls

1. **Complex Commands**: Avoid commands that do too many things
2. **Missing Validation**: Ensure all commands are validated before processing
3. **Ignoring Errors**: Handle errors properly in command handlers
4. **Breaking Correlation**: Ensure correlation information is maintained throughout the system
5. **Business Logic in Handlers**: Keep business logic in aggregates, not in command handlers

---

**Navigation**:
- [← Previous: Creating a New Aggregate Root](creating-aggregate-root.md)
- [↑ Back to Top](#handling-commands-and-generating-events)
- [→ Next: Saving and Retrieving Aggregates](saving-retrieving-aggregates.md)
