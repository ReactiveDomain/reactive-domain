# Workshop Materials

[← Back to Table of Contents](README.md)

This document provides materials for conducting workshops and training sessions on Reactive Domain.

## Table of Contents

- [Workshop Overview](#workshop-overview)
- [Prerequisites](#prerequisites)
- [Workshop Modules](#workshop-modules)
- [Exercises](#exercises)
- [Sample Solutions](#sample-solutions)
- [Presentation Slides](#presentation-slides)
- [Additional Resources](#additional-resources)

## Workshop Overview

This workshop is designed to provide hands-on experience with Reactive Domain and event sourcing concepts. It can be delivered as a one-day intensive workshop or spread across multiple sessions.

### Learning Objectives

By the end of this workshop, participants will be able to:

1. Understand the core concepts of event sourcing and CQRS
2. Implement event-sourced aggregates using Reactive Domain
3. Create and use repositories for storing and retrieving aggregates
4. Build read models and projections for querying data
5. Test event-sourced applications effectively
6. Apply best practices for production applications

### Target Audience

- .NET developers interested in event sourcing
- Software architects evaluating event sourcing for their projects
- Teams transitioning to event-sourced architectures

## Prerequisites

### Knowledge Prerequisites

- Familiarity with C# and .NET development
- Basic understanding of domain-driven design concepts
- Experience with object-oriented programming

### Technical Prerequisites

- .NET 7.0 SDK or later
- Visual Studio 2022, JetBrains Rider, or Visual Studio Code
- Docker for running EventStoreDB
- Git for accessing workshop materials

### Setup Instructions

Before the workshop, participants should:

1. Clone the workshop repository:
   ```bash
   git clone https://github.com/ReactiveDomain/reactive-domain-workshop.git
   ```

2. Install the .NET 7.0 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)

3. Install Docker from [docker.com](https://www.docker.com/products/docker-desktop)

4. Run EventStoreDB using Docker:
   ```bash
   docker run --name eventstore -d -p 2113:2113 -p 1113:1113 \
     -e EVENTSTORE_CLUSTER_SIZE=1 \
     -e EVENTSTORE_RUN_PROJECTIONS=All \
     -e EVENTSTORE_START_STANDARD_PROJECTIONS=true \
     -e EVENTSTORE_EXT_TCP_PORT=1113 \
     -e EVENTSTORE_HTTP_PORT=2113 \
     -e EVENTSTORE_INSECURE=true \
     -e EVENTSTORE_ENABLE_EXTERNAL_TCP=true \
     -e EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true \
     eventstore/eventstore:latest
   ```

5. Verify EventStoreDB is running by accessing http://localhost:2113 (credentials: admin/changeit)

## Workshop Modules

### Module 1: Introduction to Event Sourcing and Reactive Domain (1 hour)

#### Topics
- Event sourcing fundamentals
- Benefits and challenges of event sourcing
- Introduction to CQRS
- Overview of Reactive Domain
- Key components and architecture

#### Activities
- Presentation on event sourcing concepts
- Discussion of use cases for event sourcing
- Demonstration of Reactive Domain basics
- Q&A session

### Module 2: Setting Up a Reactive Domain Project (1 hour)

#### Topics
- Project structure for event-sourced applications
- Installing and configuring Reactive Domain
- Setting up EventStoreDB
- Basic configuration options

#### Activities
- Guided setup of a new project
- Installing required packages
- Configuring EventStoreDB connection
- Verifying the setup

### Module 3: Creating Aggregates, Commands, and Events (2 hours)

#### Topics
- Designing aggregates
- Defining events
- Creating commands
- Implementing business logic
- Handling validation

#### Activities
- Designing a domain model for a banking application
- Implementing the Account aggregate
- Creating events for account operations
- Implementing command handlers
- Testing the aggregate behavior

### Module 4: Working with Repositories and Event Stores (1.5 hours)

#### Topics
- Repository pattern in event sourcing
- Connecting to EventStoreDB
- Saving and loading aggregates
- Handling concurrency
- Stream naming strategies

#### Activities
- Implementing a repository
- Connecting to EventStoreDB
- Saving and loading aggregates
- Exploring events in EventStoreDB UI
- Handling concurrency conflicts

### Module 5: Building Read Models and Projections (2 hours)

#### Topics
- CQRS principles
- Designing read models
- Implementing projections
- Handling eventual consistency
- Optimizing for query performance

#### Activities
- Designing read models for the banking application
- Implementing event handlers for updating read models
- Creating query handlers
- Testing read model consistency
- Exploring projection strategies

### Module 6: Testing Event-Sourced Applications (1.5 hours)

#### Topics
- Unit testing aggregates
- Testing command handlers
- Testing event handlers
- Integration testing with EventStoreDB
- Test fixtures and helpers

#### Activities
- Writing unit tests for aggregates
- Testing command handlers
- Testing event handlers
- Setting up integration tests
- Using test fixtures and helpers

### Module 7: Advanced Patterns and Production Considerations (1 hour)

#### Topics
- Snapshots for performance optimization
- Versioning events
- Handling event schema evolution
- Deployment strategies
- Monitoring and observability

#### Activities
- Implementing snapshots
- Handling event versioning
- Discussing deployment strategies
- Planning for production readiness
- Q&A and wrap-up

## Exercises

### Exercise 1: Creating Your First Aggregate

#### Objective
Implement a simple `Account` aggregate with basic operations.

#### Tasks
1. Create an `Account` aggregate class that extends `AggregateRoot`
2. Define events: `AccountCreated`, `AmountDeposited`, `AmountWithdrawn`
3. Implement methods: `Create`, `Deposit`, `Withdraw`
4. Implement `Apply` methods for each event
5. Add business rules validation

#### Expected Outcome
A working `Account` aggregate that maintains its state through events.

#### Starter Code
```csharp
public class Account : AggregateRoot
{
    // TODO: Implement private state
    
    public Account(Guid id) : base(id)
    {
    }
    
    public void Create(string owner, decimal initialBalance)
    {
        // TODO: Implement validation and event raising
    }
    
    public void Deposit(decimal amount)
    {
        // TODO: Implement validation and event raising
    }
    
    public void Withdraw(decimal amount)
    {
        // TODO: Implement validation and event raising
    }
    
    // TODO: Implement Apply methods
}
```

### Exercise 2: Implementing a Repository

#### Objective
Create a repository for storing and retrieving Account aggregates.

#### Tasks
1. Create an `AccountRepository` class that implements `IRepository`
2. Configure connection to EventStoreDB
3. Implement methods to save and load aggregates
4. Test the repository with the Account aggregate

#### Expected Outcome
A working repository that can save and load Account aggregates.

#### Starter Code
```csharp
public class AccountRepository : IRepository
{
    // TODO: Implement repository fields
    
    public AccountRepository(IStreamStoreConnection connection)
    {
        // TODO: Initialize repository
    }
    
    public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) 
        where TAggregate : class, IEventSource
    {
        // TODO: Implement loading aggregate
    }
    
    public void Save(IEventSource aggregate)
    {
        // TODO: Implement saving aggregate
    }
    
    public void Delete(IEventSource aggregate)
    {
        // TODO: Implement deleting aggregate
    }
}
```

### Exercise 3: Building a Read Model

#### Objective
Create a read model for account balances and transactions.

#### Tasks
1. Define read model classes: `AccountDetails`, `TransactionSummary`
2. Create event handlers for updating read models
3. Implement a query service for retrieving account information
4. Store read models in memory (optional: use a database)

#### Expected Outcome
A working read model that provides account details and transaction history.

#### Starter Code
```csharp
public class AccountDetails
{
    public Guid Id { get; set; }
    public string Owner { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TransactionSummary> Transactions { get; set; } = new List<TransactionSummary>();
}

public class TransactionSummary
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AccountDetailsHandler : 
    IEventHandler<AccountCreated>,
    IEventHandler<AmountDeposited>,
    IEventHandler<AmountWithdrawn>
{
    // TODO: Implement event handlers
}
```

### Exercise 4: Testing Aggregates

#### Objective
Write unit tests for the Account aggregate.

#### Tasks
1. Create a test class for the Account aggregate
2. Write tests for the Create, Deposit, and Withdraw methods
3. Test business rule validations
4. Verify events are raised correctly

#### Expected Outcome
A comprehensive test suite for the Account aggregate.

#### Starter Code
```csharp
public class AccountTests
{
    [Fact]
    public void CanCreateAccount()
    {
        // TODO: Implement test
    }
    
    [Fact]
    public void CanDepositMoney()
    {
        // TODO: Implement test
    }
    
    [Fact]
    public void CanWithdrawMoney()
    {
        // TODO: Implement test
    }
    
    [Fact]
    public void CannotWithdrawMoreThanBalance()
    {
        // TODO: Implement test
    }
}
```

### Exercise 5: Implementing Snapshots

#### Objective
Implement snapshots for the Account aggregate to improve performance.

#### Tasks
1. Create a snapshot class for the Account aggregate
2. Implement the `ISnapshotSource` interface on the Account aggregate
3. Create a snapshot repository
4. Test loading and saving with snapshots

#### Expected Outcome
A working snapshot implementation that improves aggregate loading performance.

#### Starter Code
```csharp
public class AccountSnapshot
{
    public decimal Balance { get; set; }
    public long Version { get; set; }
}

public class Account : AggregateRoot, ISnapshotSource
{
    // Existing implementation...
    
    public void RestoreFromSnapshot(object snapshot)
    {
        // TODO: Implement snapshot restoration
    }
    
    public object TakeSnapshot()
    {
        // TODO: Implement snapshot creation
    }
}
```

## Sample Solutions

### Solution for Exercise 1: Creating Your First Aggregate

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    private bool _isCreated;
    
    public Account(Guid id) : base(id)
    {
    }
    
    public void Create(string owner, decimal initialBalance)
    {
        if (_isCreated)
            throw new InvalidOperationException("Account already created");
            
        if (initialBalance < 0)
            throw new ArgumentException("Initial balance cannot be negative");
            
        RaiseEvent(new AccountCreated(Id, owner, initialBalance));
    }
    
    public void Deposit(decimal amount)
    {
        EnsureCreated();
        
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
            
        RaiseEvent(new AmountDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        EnsureCreated();
        
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new AmountWithdrawn(Id, amount));
    }
    
    public decimal GetBalance()
    {
        EnsureCreated();
        return _balance;
    }
    
    private void EnsureCreated()
    {
        if (!_isCreated)
            throw new InvalidOperationException("Account not created");
    }
    
    private void Apply(AccountCreated @event)
    {
        _balance = @event.InitialBalance;
        _isCreated = true;
    }
    
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

### Solution for Exercise 2: Implementing a Repository

```csharp
public class AccountRepository : IRepository
{
    private readonly StreamStoreRepository _repository;
    
    public AccountRepository(IStreamStoreConnection connection)
    {
        var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        var serializer = new JsonMessageSerializer();
        _repository = new StreamStoreRepository(streamNameBuilder, connection, serializer);
    }
    
    public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) 
        where TAggregate : class, IEventSource
    {
        return _repository.TryGetById(id, out aggregate, version);
    }
    
    public void Save(IEventSource aggregate)
    {
        _repository.Save(aggregate);
    }
    
    public void Delete(IEventSource aggregate)
    {
        _repository.Delete(aggregate);
    }
}
```

### Solution for Exercise 3: Building a Read Model

```csharp
public class AccountDetailsHandler : 
    IEventHandler<AccountCreated>,
    IEventHandler<AmountDeposited>,
    IEventHandler<AmountWithdrawn>
{
    private readonly Dictionary<Guid, AccountDetails> _accounts = new Dictionary<Guid, AccountDetails>();
    
    public void Handle(AccountCreated @event)
    {
        var account = new AccountDetails
        {
            Id = @event.AccountId,
            Owner = @event.Owner,
            Balance = @event.InitialBalance,
            CreatedAt = DateTime.UtcNow
        };
        
        account.Transactions.Add(new TransactionSummary
        {
            Id = Guid.NewGuid(),
            Type = "Create",
            Amount = @event.InitialBalance,
            Timestamp = DateTime.UtcNow
        });
        
        _accounts[@event.AccountId] = account;
    }
    
    public void Handle(AmountDeposited @event)
    {
        if (_accounts.TryGetValue(@event.AccountId, out var account))
        {
            account.Balance += @event.Amount;
            
            account.Transactions.Add(new TransactionSummary
            {
                Id = Guid.NewGuid(),
                Type = "Deposit",
                Amount = @event.Amount,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public void Handle(AmountWithdrawn @event)
    {
        if (_accounts.TryGetValue(@event.AccountId, out var account))
        {
            account.Balance -= @event.Amount;
            
            account.Transactions.Add(new TransactionSummary
            {
                Id = Guid.NewGuid(),
                Type = "Withdraw",
                Amount = @event.Amount,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public AccountDetails GetAccount(Guid accountId)
    {
        return _accounts.TryGetValue(accountId, out var account) ? account : null;
    }
    
    public IEnumerable<AccountDetails> GetAllAccounts()
    {
        return _accounts.Values;
    }
}
```

### Solution for Exercise 4: Testing Aggregates

```csharp
public class AccountTests
{
    [Fact]
    public void CanCreateAccount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account(accountId);
        
        // Act
        account.Create("John Doe", 100);
        
        // Assert
        Assert.Equal(100, account.GetBalance());
        
        // Verify events
        var events = ((IEventSource)account).TakeEvents();
        Assert.Single(events);
        var @event = Assert.IsType<AccountCreated>(events[0]);
        Assert.Equal(accountId, @event.AccountId);
        Assert.Equal("John Doe", @event.Owner);
        Assert.Equal(100, @event.InitialBalance);
    }
    
    [Fact]
    public void CanDepositMoney()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account(accountId);
        account.Create("John Doe", 100);
        ((IEventSource)account).TakeEvents(); // Clear events
        
        // Act
        account.Deposit(50);
        
        // Assert
        Assert.Equal(150, account.GetBalance());
        
        // Verify events
        var events = ((IEventSource)account).TakeEvents();
        Assert.Single(events);
        var @event = Assert.IsType<AmountDeposited>(events[0]);
        Assert.Equal(accountId, @event.AccountId);
        Assert.Equal(50, @event.Amount);
    }
    
    [Fact]
    public void CanWithdrawMoney()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account(accountId);
        account.Create("John Doe", 100);
        ((IEventSource)account).TakeEvents(); // Clear events
        
        // Act
        account.Withdraw(30);
        
        // Assert
        Assert.Equal(70, account.GetBalance());
        
        // Verify events
        var events = ((IEventSource)account).TakeEvents();
        Assert.Single(events);
        var @event = Assert.IsType<AmountWithdrawn>(events[0]);
        Assert.Equal(accountId, @event.AccountId);
        Assert.Equal(30, @event.Amount);
    }
    
    [Fact]
    public void CannotWithdrawMoreThanBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account(accountId);
        account.Create("John Doe", 100);
        
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => account.Withdraw(150));
        Assert.Equal("Insufficient funds", exception.Message);
    }
}
```

### Solution for Exercise 5: Implementing Snapshots

```csharp
public class Account : AggregateRoot, ISnapshotSource
{
    private decimal _balance;
    private bool _isCreated;
    
    // Existing implementation...
    
    public void RestoreFromSnapshot(object snapshot)
    {
        var accountSnapshot = (AccountSnapshot)snapshot;
        _balance = accountSnapshot.Balance;
        _isCreated = true;
        ExpectedVersion = accountSnapshot.Version;
    }
    
    public object TakeSnapshot()
    {
        return new AccountSnapshot
        {
            Balance = _balance,
            Version = ExpectedVersion
        };
    }
}

public class SnapshotRepository : IRepository
{
    private readonly IRepository _innerRepository;
    private readonly ISnapshotStore _snapshotStore;
    
    public SnapshotRepository(IRepository innerRepository, ISnapshotStore snapshotStore)
    {
        _innerRepository = innerRepository;
        _snapshotStore = snapshotStore;
    }
    
    public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) 
        where TAggregate : class, IEventSource
    {
        if (typeof(ISnapshotSource).IsAssignableFrom(typeof(TAggregate)))
        {
            // Try to load from snapshot
            var snapshot = _snapshotStore.GetSnapshot(id, typeof(TAggregate));
            if (snapshot != null)
            {
                aggregate = (TAggregate)Activator.CreateInstance(typeof(TAggregate), id);
                ((ISnapshotSource)aggregate).RestoreFromSnapshot(snapshot.Data);
                
                // Load events after the snapshot
                _innerRepository.Update(ref aggregate, version);
                return true;
            }
        }
        
        // Fall back to loading all events
        return _innerRepository.TryGetById(id, out aggregate, version);
    }
    
    public void Save(IEventSource aggregate)
    {
        _innerRepository.Save(aggregate);
        
        // Take a snapshot if the aggregate supports it
        if (aggregate is ISnapshotSource snapshotSource)
        {
            var snapshot = snapshotSource.TakeSnapshot();
            _snapshotStore.SaveSnapshot(aggregate.Id, aggregate.GetType(), snapshot, aggregate.ExpectedVersion);
        }
    }
    
    public void Delete(IEventSource aggregate)
    {
        _innerRepository.Delete(aggregate);
    }
}
```

## Presentation Slides

The workshop includes a set of presentation slides covering the key concepts and techniques. These slides are available in the workshop repository in both PowerPoint and PDF formats.

### Slide Decks

1. **Introduction to Event Sourcing and Reactive Domain**
   - Event sourcing fundamentals
   - Benefits and challenges
   - CQRS overview
   - Reactive Domain architecture

2. **Aggregates, Commands, and Events**
   - Designing aggregates
   - Event design principles
   - Command handling patterns
   - Business rule implementation

3. **Repositories and Event Stores**
   - Repository pattern
   - EventStoreDB integration
   - Stream naming strategies
   - Concurrency handling

4. **Read Models and Projections**
   - CQRS implementation
   - Projection patterns
   - Read model design
   - Eventual consistency

5. **Testing Event-Sourced Applications**
   - Unit testing strategies
   - Integration testing
   - Test fixtures and helpers
   - Test-driven development

6. **Advanced Patterns and Production Considerations**
   - Snapshots
   - Event versioning
   - Deployment strategies
   - Monitoring and observability

## Additional Resources

### Code Samples

The workshop repository includes complete code samples for all exercises and additional examples:

- **BankingApp**: A simple banking application demonstrating basic event sourcing concepts
- **ECommerce**: A more complex e-commerce application showing advanced patterns
- **Snapshots**: Examples of implementing and using snapshots
- **Testing**: Comprehensive testing examples

### Reference Documentation

- [Reactive Domain Documentation](https://github.com/ReactiveDomain/reactive-domain/docs)
- [EventStoreDB Documentation](https://developers.eventstore.com/server/v21.10/docs/)
- [CQRS Journey by Microsoft](https://docs.microsoft.com/en-us/previous-versions/msp-n-p/jj554200(v=pandp.10))

### Recommended Reading

- **Domain-Driven Design** by Eric Evans
- **Implementing Domain-Driven Design** by Vaughn Vernon
- **CQRS Documents** by Greg Young
- **Event Sourcing and CQRS** by Martin Fowler

[↑ Back to Top](#workshop-materials) | [← Back to Table of Contents](README.md)
