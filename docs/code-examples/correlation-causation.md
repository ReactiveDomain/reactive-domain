# Handling Correlation and Causation

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to implement correlation and causation tracking in Reactive Domain to trace message flows through the system.

## Understanding Correlation and Causation

```csharp
/*
Correlation and Causation IDs are essential for tracing message flows:

- CorrelationId: Identifies a chain of related messages (same for all messages in a chain)
- CausationId: Identifies the direct cause of a message (points to the MessageId of the previous message)
- MessageId: Unique identifier for each message

Message Chain Example:
1. Command A (MessageId: A, CorrelationId: A, CausationId: 0)
2. Event B (MessageId: B, CorrelationId: A, CausationId: A)
3. Command C (MessageId: C, CorrelationId: A, CausationId: B)
4. Event D (MessageId: D, CorrelationId: A, CausationId: C)

This creates a traceable chain: A → B → C → D
All sharing the same CorrelationId (A), with CausationId forming the links.
*/
```

## ICorrelatedMessage Interface

```csharp
using System;

namespace ReactiveDomain.Messaging
{
    public interface ICorrelatedMessage
    {
        /// <summary>
        /// Unique identifier for this message
        /// </summary>
        Guid MessageId { get; }
        
        /// <summary>
        /// Identifier used to correlate related messages in a workflow
        /// </summary>
        Guid CorrelationId { get; }
        
        /// <summary>
        /// Identifier of the message that caused this message
        /// </summary>
        Guid CausationId { get; }
    }
}
```

## Correlated Message Base Class

```csharp
using System;

namespace ReactiveDomain.Messaging.Messages
{
    public abstract class CorrelatedMessage : ICorrelatedMessage
    {
        public Guid MessageId { get; }
        public Guid CorrelationId { get; }
        public Guid CausationId { get; }
        
        protected CorrelatedMessage()
        {
            MessageId = Guid.NewGuid();
            CorrelationId = MessageId;
            CausationId = Guid.Empty;
        }
        
        protected CorrelatedMessage(Guid correlationId, Guid causationId)
        {
            MessageId = Guid.NewGuid();
            CorrelationId = correlationId;
            CausationId = causationId;
        }
    }
}
```

## Command and Event Classes

```csharp
using System;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging.Messages
{
    public abstract class Command : CorrelatedMessage
    {
        protected Command() : base()
        {
        }
        
        protected Command(Guid correlationId, Guid causationId) 
            : base(correlationId, causationId)
        {
        }
    }
    
    public abstract class Event : CorrelatedMessage
    {
        protected Event() : base()
        {
        }
        
        protected Event(Guid correlationId, Guid causationId) 
            : base(correlationId, causationId)
        {
        }
    }
}
```

## MessageBuilder Factory

```csharp
using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Messaging
{
    public static class MessageBuilder
    {
        /// <summary>
        /// Creates a new message that starts a correlation chain
        /// </summary>
        public static T New<T>(Func<T> messageFactory) where T : ICorrelatedMessage
        {
            return messageFactory();
        }
        
        /// <summary>
        /// Creates a new message that continues a correlation chain from a source message
        /// </summary>
        public static T From<T>(ICorrelatedMessage source, Func<T> messageFactory) where T : ICorrelatedMessage
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
                
            var message = messageFactory();
            
            // If the message already has correlation info, don't override it
            if (message.CorrelationId == message.MessageId && message.CausationId == Guid.Empty)
            {
                // Use reflection to set the correlation and causation IDs
                var type = message.GetType();
                
                var correlationIdProperty = type.GetProperty("CorrelationId");
                if (correlationIdProperty != null && correlationIdProperty.CanWrite)
                {
                    correlationIdProperty.SetValue(message, source.CorrelationId);
                }
                
                var causationIdProperty = type.GetProperty("CausationId");
                if (causationIdProperty != null && causationIdProperty.CanWrite)
                {
                    causationIdProperty.SetValue(message, source.MessageId);
                }
            }
            
            return message;
        }
    }
}
```

## Correlated Command Example

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
        
        public DepositFunds(Guid accountId, decimal amount,
                           Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
}
```

## Correlated Event Example

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace MyApp.Domain.Events
{
    public class AccountCreated : Event
    {
        public readonly Guid AccountId;
        public readonly string AccountNumber;
        public readonly string CustomerName;
        
        public AccountCreated(Guid accountId, string accountNumber, string customerName)
            : base()
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
        }
        
        public AccountCreated(Guid accountId, string accountNumber, string customerName,
                             Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
        }
    }
    
    public class FundsDeposited : Event
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        
        public FundsDeposited(Guid accountId, decimal amount)
            : base()
        {
            AccountId = accountId;
            Amount = amount;
        }
        
        public FundsDeposited(Guid accountId, decimal amount,
                             Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
}
```

## Aggregate Root with Correlation

```csharp
using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain.Events;

namespace MyApp.Domain
{
    public class Account : AggregateRoot
    {
        private string _accountNumber;
        private string _customerName;
        private decimal _balance;
        private bool _isClosed;
        
        public Account(Guid id) : base(id)
        {
        }
        
        // Constructor with correlation source
        public Account(Guid id, ICorrelatedMessage source) : base(id, source)
        {
        }
        
        public void Create(string accountNumber, string customerName)
        {
            if (string.IsNullOrEmpty(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrEmpty(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            // Apply the event with correlation
            ApplyChange(new AccountCreated(Id, accountNumber, customerName, CorrelationId, CausationId));
        }
        
        public void Deposit(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            if (_isClosed)
                throw new InvalidOperationException("Cannot deposit to a closed account");
                
            // Apply the event with correlation
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
                
            // Apply the event with correlation
            ApplyChange(new FundsWithdrawn(Id, amount, CorrelationId, CausationId));
        }
        
        public void Close()
        {
            if (_isClosed)
                throw new InvalidOperationException("Account is already closed");
                
            // Apply the event with correlation
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
    }
}
```

## Correlated Repository

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation
{
    public interface ICorrelatedRepository
    {
        TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : class, IEventSource;
        void Save(IEventSource aggregate, ICorrelatedMessage source);
        bool TryGetById<TAggregate>(Guid id, ICorrelatedMessage source, out TAggregate aggregate) where TAggregate : class, IEventSource;
    }
    
    public class CorrelatedStreamStoreRepository : ICorrelatedRepository
    {
        private readonly IRepository _repository;
        
        public CorrelatedStreamStoreRepository(IRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : class, IEventSource
        {
            var aggregate = _repository.GetById<TAggregate>(id);
            
            // Set correlation on the aggregate
            if (aggregate is ICorrelatedEventSource correlatedAggregate)
            {
                correlatedAggregate.SetCorrelationIds(source.CorrelationId, source.MessageId);
            }
            
            return aggregate;
        }
        
        public void Save(IEventSource aggregate, ICorrelatedMessage source)
        {
            // Set correlation on the aggregate if not already set
            if (aggregate is ICorrelatedEventSource correlatedAggregate && 
                correlatedAggregate.CorrelationId == Guid.Empty)
            {
                correlatedAggregate.SetCorrelationIds(source.CorrelationId, source.MessageId);
            }
            
            _repository.Save(aggregate);
        }
        
        public bool TryGetById<TAggregate>(Guid id, ICorrelatedMessage source, out TAggregate aggregate) where TAggregate : class, IEventSource
        {
            if (_repository.TryGetById(id, out aggregate))
            {
                // Set correlation on the aggregate
                if (aggregate is ICorrelatedEventSource correlatedAggregate)
                {
                    correlatedAggregate.SetCorrelationIds(source.CorrelationId, source.MessageId);
                }
                
                return true;
            }
            
            return false;
        }
    }
}
```

## Correlated Command Handler

```csharp
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.Domain.Commands;

namespace MyApp.Domain.Handlers
{
    public class CorrelatedAccountCommandHandler : 
        IHandleCommand<CreateAccount>,
        IHandleCommand<DepositFunds>,
        IHandleCommand<WithdrawFunds>,
        IHandleCommand<CloseAccount>
    {
        private readonly ICorrelatedRepository _repository;
        
        public CorrelatedAccountCommandHandler(ICorrelatedRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public void Handle(CreateAccount command)
        {
            // Create a new account with correlation
            var account = new Account(command.AccountId, command);
            
            // Initialize the account
            account.Create(command.AccountNumber, command.CustomerName);
            
            // Save the account with correlation
            _repository.Save(account, command);
        }
        
        public void Handle(DepositFunds command)
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
        
        public void Handle(WithdrawFunds command)
        {
            try
            {
                // Load the account with correlation
                var account = _repository.GetById<Account>(command.AccountId, command);
                
                // Process the command
                account.Withdraw(command.Amount);
                
                // Save the changes with correlation
                _repository.Save(account, command);
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
                // Load the account with correlation
                var account = _repository.GetById<Account>(command.AccountId, command);
                
                // Process the command
                account.Close();
                
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

## Using MessageBuilder for Correlation

```csharp
using System;
using ReactiveDomain.Messaging;
using MyApp.Domain.Commands;

namespace MyApp.Examples
{
    public class CorrelationExample
    {
        public void DemonstrateCorrelation()
        {
            // Create a new command that starts a correlation chain
            var createCommand = MessageBuilder.New(() => new CreateAccount(
                Guid.NewGuid(),
                "ACC-123",
                "John Doe"
            ));
            
            Console.WriteLine("Create Command:");
            Console.WriteLine($"  MessageId: {createCommand.MessageId}");
            Console.WriteLine($"  CorrelationId: {createCommand.CorrelationId}");
            Console.WriteLine($"  CausationId: {createCommand.CausationId}");
            
            // Create a command from an existing command (maintains correlation)
            var depositCommand = MessageBuilder.From(createCommand, () => new DepositFunds(
                ((CreateAccount)createCommand).AccountId,
                1000
            ));
            
            Console.WriteLine("\nDeposit Command:");
            Console.WriteLine($"  MessageId: {depositCommand.MessageId}");
            Console.WriteLine($"  CorrelationId: {depositCommand.CorrelationId}");
            Console.WriteLine($"  CausationId: {depositCommand.CausationId}");
            
            // Create another command in the same chain
            var withdrawCommand = MessageBuilder.From(depositCommand, () => new WithdrawFunds(
                ((DepositFunds)depositCommand).AccountId,
                500
            ));
            
            Console.WriteLine("\nWithdraw Command:");
            Console.WriteLine($"  MessageId: {withdrawCommand.MessageId}");
            Console.WriteLine($"  CorrelationId: {withdrawCommand.CorrelationId}");
            Console.WriteLine($"  CausationId: {withdrawCommand.CausationId}");
            
            // Verify correlation chain
            Console.WriteLine("\nCorrelation Chain:");
            Console.WriteLine($"  All messages have the same CorrelationId: {createCommand.CorrelationId == depositCommand.CorrelationId && depositCommand.CorrelationId == withdrawCommand.CorrelationId}");
            Console.WriteLine($"  Deposit CausationId matches Create MessageId: {depositCommand.CausationId == createCommand.MessageId}");
            Console.WriteLine($"  Withdraw CausationId matches Deposit MessageId: {withdrawCommand.CausationId == depositCommand.MessageId}");
        }
    }
}
```

## Complete Example with Tracing

```csharp
using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.Domain.Commands;
using MyApp.Domain.Events;

namespace MyApp.Examples
{
    public class CorrelationTracingExample
    {
        private readonly Dictionary<Guid, List<ICorrelatedMessage>> _messagesByCorrelation = 
            new Dictionary<Guid, List<ICorrelatedMessage>>();
            
        private readonly Dictionary<Guid, ICorrelatedMessage> _messagesById = 
            new Dictionary<Guid, ICorrelatedMessage>();
            
        public void TraceMessageFlow()
        {
            // Set up message tracking
            var commandBus = new CommandBus();
            var eventBus = new EventBus();
            
            // Subscribe to all commands
            commandBus.Subscribe<ICommand>(cmd => TrackMessage(cmd));
            
            // Subscribe to all events
            eventBus.Subscribe<IEvent>(evt => TrackMessage(evt));
            
            // Create a new account command
            var accountId = Guid.NewGuid();
            var createCommand = MessageBuilder.New(() => new CreateAccount(
                accountId,
                "ACC-123",
                "John Doe"
            ));
            
            // Track and send the command
            TrackMessage(createCommand);
            
            // Simulate command handler producing events
            var accountCreatedEvent = MessageBuilder.From(createCommand, () => new AccountCreated(
                accountId,
                "ACC-123",
                "John Doe"
            ));
            
            // Track and publish the event
            TrackMessage(accountCreatedEvent);
            
            // Create a deposit command
            var depositCommand = MessageBuilder.From(accountCreatedEvent, () => new DepositFunds(
                accountId,
                1000
            ));
            
            // Track and send the command
            TrackMessage(depositCommand);
            
            // Simulate command handler producing events
            var fundsDepositedEvent = MessageBuilder.From(depositCommand, () => new FundsDeposited(
                accountId,
                1000
            ));
            
            // Track and publish the event
            TrackMessage(fundsDepositedEvent);
            
            // Create a withdraw command
            var withdrawCommand = MessageBuilder.From(fundsDepositedEvent, () => new WithdrawFunds(
                accountId,
                500
            ));
            
            // Track and send the command
            TrackMessage(withdrawCommand);
            
            // Simulate command handler producing events
            var fundsWithdrawnEvent = MessageBuilder.From(withdrawCommand, () => new FundsWithdrawn(
                accountId,
                500
            ));
            
            // Track and publish the event
            TrackMessage(fundsWithdrawnEvent);
            
            // Print the correlation trace
            PrintCorrelationTrace(createCommand.CorrelationId);
        }
        
        private void TrackMessage(ICorrelatedMessage message)
        {
            // Track by correlation ID
            if (!_messagesByCorrelation.ContainsKey(message.CorrelationId))
            {
                _messagesByCorrelation[message.CorrelationId] = new List<ICorrelatedMessage>();
            }
            
            _messagesByCorrelation[message.CorrelationId].Add(message);
            
            // Track by message ID
            _messagesById[message.MessageId] = message;
        }
        
        private void PrintCorrelationTrace(Guid correlationId)
        {
            Console.WriteLine($"\nTrace for Correlation ID: {correlationId}");
            Console.WriteLine("======================================");
            
            if (!_messagesByCorrelation.ContainsKey(correlationId))
            {
                Console.WriteLine("No messages found with this correlation ID");
                return;
            }
            
            // Build the causation chain
            var messageChain = BuildCausationChain(correlationId);
            
            // Print the chain
            foreach (var message in messageChain)
            {
                string messageType = message.GetType().Name;
                string messageCategory = message is ICommand ? "Command" : "Event";
                
                Console.WriteLine($"{messageCategory}: {messageType}");
                Console.WriteLine($"  MessageId: {message.MessageId}");
                Console.WriteLine($"  CorrelationId: {message.CorrelationId}");
                Console.WriteLine($"  CausationId: {message.CausationId}");
                Console.WriteLine();
            }
        }
        
        private List<ICorrelatedMessage> BuildCausationChain(Guid correlationId)
        {
            var result = new List<ICorrelatedMessage>();
            var messagesByCorrelation = _messagesByCorrelation[correlationId];
            
            // Find the first message (with empty causation ID)
            var firstMessage = messagesByCorrelation.Find(m => m.CausationId == Guid.Empty);
            if (firstMessage == null)
            {
                return result;
            }
            
            // Start with the first message
            result.Add(firstMessage);
            var currentMessageId = firstMessage.MessageId;
            
            // Build the chain by following causation IDs
            while (true)
            {
                var nextMessages = messagesByCorrelation.FindAll(m => m.CausationId == currentMessageId);
                if (nextMessages.Count == 0)
                {
                    break;
                }
                
                // Add the next message in the chain
                var nextMessage = nextMessages[0];
                result.Add(nextMessage);
                currentMessageId = nextMessage.MessageId;
            }
            
            return result;
        }
    }
}
```

## Key Concepts

### Correlation and Causation IDs

- **MessageId**: Unique identifier for each message
- **CorrelationId**: Identifies a chain of related messages (same for all messages in a chain)
- **CausationId**: Identifies the direct cause of a message (points to the MessageId of the previous message)

### Message Chain

A typical message chain follows this pattern:
1. Command A (MessageId: A, CorrelationId: A, CausationId: 0)
2. Event B (MessageId: B, CorrelationId: A, CausationId: A)
3. Command C (MessageId: C, CorrelationId: A, CausationId: B)
4. Event D (MessageId: D, CorrelationId: A, CausationId: C)

### MessageBuilder

- **MessageBuilder.New()**: Creates a new message that starts a correlation chain
- **MessageBuilder.From()**: Creates a new message that continues a correlation chain

### Correlated Repository

- Maintains correlation information when loading and saving aggregates
- Sets correlation IDs on aggregates based on the source message

## Best Practices

1. **Use MessageBuilder**: Always use MessageBuilder to create correlated messages
2. **Consistent Correlation**: Maintain the same correlation ID throughout a business transaction
3. **Proper Causation**: Set the causation ID to the message ID of the triggering message
4. **Correlated Repositories**: Use correlated repositories to maintain correlation chains
5. **Correlation in Aggregates**: Pass correlation information to aggregates when handling commands
6. **Tracing**: Implement tracing to visualize message flows

## Common Pitfalls

1. **Breaking Correlation Chains**: Creating messages without using MessageBuilder
2. **Missing Correlation**: Not passing correlation information to aggregates
3. **Incorrect Causation**: Setting the wrong causation ID
4. **Lost Correlation**: Not maintaining correlation when crossing system boundaries
5. **Correlation Overload**: Using correlation for purposes other than tracing

---

**Navigation**:
- [← Previous: Implementing Projections](implementing-projections.md)
- [↑ Back to Top](#handling-correlation-and-causation)
- [→ Next: Implementing Snapshots](implementing-snapshots.md)
