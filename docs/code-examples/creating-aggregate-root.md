# Creating a New Aggregate Root

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to create a new aggregate root in Reactive Domain.

## Basic Aggregate Root Structure

```csharp
using System;
using ReactiveDomain.Foundation;

namespace MyApp.Domain
{
    public class Account : AggregateRoot
    {
        // Private state fields
        private decimal _balance;
        private string _accountNumber;
        private string _customerName;
        private bool _isClosed;
        
        // Constructor for creating a new aggregate
        public Account(Guid id) : base(id)
        {
            // Initialize with default state
        }
        
        // Constructor for loading from history
        protected Account(Guid id, IEnumerable<object> events) : base(id, events)
        {
            // Base constructor will call RestoreFromEvents
        }
        
        // Constructor with correlation
        public Account(Guid id, ICorrelatedMessage source) : base(id, source)
        {
            // Initialize with default state and maintain correlation
        }
        
        // Command methods
        public void Create(string accountNumber, string customerName)
        {
            // Validate command
            if (_accountNumber != null)
                throw new InvalidOperationException("Account already created");
                
            // Generate and apply event
            RaiseEvent(new AccountCreated(Id, accountNumber, customerName));
        }
        
        public void Deposit(decimal amount)
        {
            // Validate command
            if (_isClosed)
                throw new InvalidOperationException("Account is closed");
                
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive");
                
            // Generate and apply event
            RaiseEvent(new FundsDeposited(Id, amount));
        }
        
        public void Withdraw(decimal amount)
        {
            // Validate command
            if (_isClosed)
                throw new InvalidOperationException("Account is closed");
                
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive");
                
            if (_balance < amount)
                throw new InvalidOperationException("Insufficient funds");
                
            // Generate and apply event
            RaiseEvent(new FundsWithdrawn(Id, amount));
        }
        
        public void Close()
        {
            // Validate command
            if (_isClosed)
                throw new InvalidOperationException("Account already closed");
                
            if (_balance > 0)
                throw new InvalidOperationException("Cannot close account with positive balance");
                
            // Generate and apply event
            RaiseEvent(new AccountClosed(Id));
        }
        
        // Query methods
        public decimal GetBalance()
        {
            return _balance;
        }
        
        public bool IsClosed()
        {
            return _isClosed;
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

## Event Definitions

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace MyApp.Domain
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
    
    public class FundsWithdrawn : Event
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        
        public FundsWithdrawn(Guid accountId, decimal amount)
            : base()
        {
            AccountId = accountId;
            Amount = amount;
        }
        
        public FundsWithdrawn(Guid accountId, decimal amount, 
                             Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
    
    public class AccountClosed : Event
    {
        public readonly Guid AccountId;
        
        public AccountClosed(Guid accountId)
            : base()
        {
            AccountId = accountId;
        }
        
        public AccountClosed(Guid accountId, Guid correlationId, Guid causationId)
            : base(correlationId, causationId)
        {
            AccountId = accountId;
        }
    }
}
```

## Key Concepts

### Aggregate Structure

- **Private State**: Aggregates maintain their state in private fields
- **Command Methods**: Public methods that validate commands and generate events
- **Query Methods**: Public methods that return information about the aggregate state
- **Event Handlers**: Private `Apply` methods that update the aggregate state

### Constructors

- **Default Constructor**: Used when creating a new aggregate
- **History Constructor**: Used when loading an aggregate from its event history
- **Correlated Constructor**: Used when creating an aggregate from a command with correlation information

### Command Validation

- Commands are validated against the current state of the aggregate
- Business rules are enforced before generating events
- Exceptions are thrown when commands are invalid

### Event Application

- Events are generated using the `RaiseEvent` method
- Each event type has a corresponding `Apply` method
- The `Apply` method updates the aggregate state based on the event

## Best Practices

1. **Keep Aggregates Small**: Focus on a single business concept
2. **Validate Commands**: Ensure all commands are valid before generating events
3. **Immutable Events**: Make all event properties read-only
4. **Private State**: Keep aggregate state private and expose it through controlled methods
5. **Descriptive Event Names**: Use past tense for event names (e.g., `AccountCreated`, `FundsDeposited`)
6. **Correlation Support**: Implement constructors that support correlation tracking

## Common Pitfalls

1. **Large Aggregates**: Avoid creating aggregates that are too large or contain too many responsibilities
2. **Public State Modification**: Don't allow direct modification of aggregate state from outside
3. **Missing Business Rules**: Ensure all business rules are enforced in command methods
4. **Complex Apply Methods**: Keep event handlers simple and focused on updating state
5. **Side Effects in Apply Methods**: Avoid side effects like I/O operations in Apply methods

---

**Navigation**:
- [← Back to Code Examples](README.md)
- [↑ Back to Top](#creating-a-new-aggregate-root)
- [→ Next: Handling Commands and Generating Events](handling-commands-events.md)
