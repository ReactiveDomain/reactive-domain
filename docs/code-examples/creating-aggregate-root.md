# Creating a New Aggregate Root

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to create a new aggregate root in Reactive Domain, following current best practices and patterns.

## Basic Aggregate Root Structure

```csharp
using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace MyApp.Domain
{
    public class Account : AggregateRoot<Guid>
    {
        // Private state fields
        private decimal _balance;
        private string _accountNumber;
        private string _customerName;
        private bool _isClosed;
        
        // Default constructor required for deserialization
        public Account() : base()
        {
            // Required for deserialization
        }
        
        // Constructor for creating a new aggregate
        public Account(Guid id) : base(id)
        {
            // Initialize with default state
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
                
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrWhiteSpace(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            // Generate and apply event
            Apply(new AccountCreated(Id, accountNumber, customerName));
        }
        
        public void Deposit(decimal amount)
        {
            // Validate command
            if (_isClosed)
                throw new InvalidOperationException("Account is closed");
                
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            // Generate and apply event
            Apply(new FundsDeposited(Id, amount));
        }
        
        public void Withdraw(decimal amount)
        {
            // Validate command
            if (_isClosed)
                throw new InvalidOperationException("Account is closed");
                
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            if (_balance < amount)
                throw new InvalidOperationException("Insufficient funds");
                
            // Generate and apply event
            Apply(new FundsWithdrawn(Id, amount));
        }
        
        public void Close()
        {
            // Validate command
            if (_isClosed)
                throw new InvalidOperationException("Account already closed");
                
            if (_balance > 0)
                throw new InvalidOperationException("Cannot close account with positive balance");
                
            // Generate and apply event
            Apply(new AccountClosed(Id));
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
        
        // Event handlers - these are called automatically by the base class
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
        
        // Override to handle snapshot restoration if needed
        protected override void RestoreFromSnapshot(object snapshot)
        {
            if (snapshot is AccountSnapshot s)
            {
                _accountNumber = s.AccountNumber;
                _customerName = s.CustomerName;
                _balance = s.Balance;
                _isClosed = s.IsClosed;
            }
        }
        
        // Override to create snapshots if needed
        protected override object CreateSnapshot()
        {
            return new AccountSnapshot
            {
                AccountNumber = _accountNumber,
                CustomerName = _customerName,
                Balance = _balance,
                IsClosed = _isClosed
            };
        }
        
        // Snapshot class for serialization
        [Serializable]
        private class AccountSnapshot
        {
            public string AccountNumber { get; set; }
            public string CustomerName { get; set; }
            public decimal Balance { get; set; }
            public bool IsClosed { get; set; }
        }
    }
}
```

## Event Definitions

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace MyApp.Domain.Events
{
    [Serializable]
    public class AccountCreated : Event
    {
        public readonly Guid AccountId;
        public readonly string AccountNumber;
        public readonly string CustomerName;
        public readonly DateTime Timestamp;
        
        public AccountCreated(Guid accountId, string accountNumber, string customerName)
            : base()
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with explicit correlation
        public AccountCreated(Guid accountId, string accountNumber, string customerName,
                             ICorrelatedMessage source)
            : base(source)
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class FundsDeposited : Event
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        public readonly DateTime Timestamp;
        
        public FundsDeposited(Guid accountId, decimal amount)
            : base()
        {
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with explicit correlation
        public FundsDeposited(Guid accountId, decimal amount, ICorrelatedMessage source)
            : base(source)
        {
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class FundsWithdrawn : Event
    {
        public readonly Guid AccountId;
        public readonly decimal Amount;
        public readonly DateTime Timestamp;
        
        public FundsWithdrawn(Guid accountId, decimal amount)
            : base()
        {
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with explicit correlation
        public FundsWithdrawn(Guid accountId, decimal amount, ICorrelatedMessage source)
            : base(source)
        {
            AccountId = accountId;
            Amount = amount;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class AccountClosed : Event
    {
        public readonly Guid AccountId;
        public readonly DateTime Timestamp;
        
        public AccountClosed(Guid accountId)
            : base()
        {
            AccountId = accountId;
            Timestamp = DateTime.UtcNow;
        }
        
        // Constructor with explicit correlation
        public AccountClosed(Guid accountId, ICorrelatedMessage source)
            : base(source)
        {
            AccountId = accountId;
            Timestamp = DateTime.UtcNow;
        }
    }
}
```

## Key Concepts

### Aggregate Structure

- **Type Parameter**: Modern Reactive Domain aggregates inherit from `AggregateRoot<TId>` where `TId` is the identifier type
- **Private State**: Aggregates maintain their state in private fields
- **Command Methods**: Public methods that validate commands and generate events
- **Query Methods**: Public methods that return information about the aggregate state
- **Event Handlers**: Private `Apply` methods that update the aggregate state
- **Snapshot Support**: Optional methods for creating and restoring from snapshots

### Constructors

- **Default Constructor**: Required for deserialization
- **ID Constructor**: Used when creating a new aggregate
- **Correlated Constructor**: Used when creating an aggregate from a command with correlation information

### Command Validation

- Commands are validated against the current state of the aggregate
- Business rules are enforced before generating events
- Exceptions are thrown when commands are invalid, with proper parameter names
- Input validation ensures data integrity

### Event Application

- Events are generated using the `Apply` method (not `RaiseEvent` in newer versions)
- Each event type has a corresponding `Apply` method
- The `Apply` method updates the aggregate state based on the event
- Events include timestamps for auditing and temporal queries

## Best Practices

1. **Keep Aggregates Small**: Focus on a single business concept and its invariants
2. **Use Strong Typing**: Specify the ID type parameter in `AggregateRoot<TId>`
3. **Validate Commands Thoroughly**: Check all preconditions and input parameters
4. **Immutable Events**: Make all event properties read-only and mark events as `[Serializable]`
5. **Include Timestamps**: Add creation timestamps to events for auditing
6. **Implement Snapshots**: For aggregates with many events, implement snapshot support
7. **Proper Correlation**: Use `ICorrelatedMessage` for tracking message chains
8. **Descriptive Naming**: Use past tense for events and imperative for commands

## Common Pitfalls

1. **Missing Default Constructor**: Forgetting the parameter-less constructor required for deserialization
2. **Mutable State**: Exposing setters for aggregate state
3. **Business Logic in Event Handlers**: Keep business logic in command methods, not in Apply methods
4. **Side Effects in Apply Methods**: Avoid I/O, external calls, or generating new events in Apply methods
5. **Overly Complex Aggregates**: Trying to model too many concepts in a single aggregate
6. **Inconsistent Validation**: Not validating all inputs or checking all business rules
7. **Ignoring Correlation**: Not properly maintaining correlation chains across messages

---

**Navigation**:
- [← Back to Code Examples](README.md)
- [↑ Back to Top](#creating-a-new-aggregate-root)
- [→ Next: Handling Commands and Generating Events](handling-commands-events.md)
