# Video Tutorial Script

[← Back to Table of Contents](README.md)

This document provides scripts and outlines for creating video tutorials about the Reactive Domain library.

## Tutorial Series Overview

A comprehensive series of video tutorials covering Reactive Domain from basics to advanced topics:

1. **Introduction to Reactive Domain and Event Sourcing** (15 minutes)
2. **Setting Up Your First Reactive Domain Project** (20 minutes)
3. **Creating Aggregates, Commands, and Events** (25 minutes)
4. **Working with Repositories and Event Stores** (20 minutes)
5. **Building Read Models and Projections** (25 minutes)
6. **Testing Event-Sourced Applications** (20 minutes)
7. **Advanced Patterns and Best Practices** (30 minutes)

## Tutorial 1: Introduction to Reactive Domain and Event Sourcing

### Opening (1 minute)
```
Welcome to this tutorial series on Reactive Domain, an open-source framework for implementing event sourcing in .NET projects.

I'm [Your Name], and in this series, we'll explore how to build robust, scalable applications using event sourcing and reactive programming principles.

In this first video, we'll cover the fundamentals of event sourcing and introduce the Reactive Domain library.
```

### What is Event Sourcing? (3 minutes)
```
Before diving into Reactive Domain, let's understand what event sourcing is.

Traditional applications store the current state of entities in a database. When you need to change something, you directly update that state.

[SHOW DIAGRAM: Traditional CRUD model]

Event sourcing takes a different approach. Instead of storing the current state, we store a sequence of events that led to that state.

[SHOW DIAGRAM: Event Sourcing model]

Think of it like a ledger in accounting. Rather than just knowing your current bank balance, you have a record of every deposit and withdrawal that led to that balance.

Key benefits of event sourcing include:

1. Complete Audit Trail: You have a history of every change
2. Temporal Queries: You can determine the state at any point in time
3. Business Insights: Events represent business activities directly
4. Separation of Concerns: Clear separation between write and read models
```

### Introduction to Reactive Domain (4 minutes)
```
Reactive Domain is a .NET framework that makes implementing event sourcing straightforward.

[SHOW DIAGRAM: Reactive Domain architecture]

The library provides several key components:

1. AggregateRoot: Base class for your domain aggregates
2. IEventSource: Interface for event-sourced entities
3. IRepository: Interface for storing and retrieving aggregates
4. Event Store Integration: Built-in support for EventStoreDB
5. Messaging Framework: Support for commands, events, and queries

What sets Reactive Domain apart is its focus on developer experience and practical implementations of event sourcing patterns.

Let's take a quick look at some code to get a feel for how Reactive Domain works.
```

### Code Overview (5 minutes)
```
[SHOW CODE: Basic aggregate example]

Here's a simple example of an account aggregate in Reactive Domain:

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    public Account(Guid id) : base(id)
    {
    }
    
    public void Create(string owner, decimal initialBalance)
    {
        if (initialBalance < 0)
            throw new ArgumentException("Initial balance cannot be negative");
            
        RaiseEvent(new AccountCreated(Id, owner, initialBalance));
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
            
        RaiseEvent(new AmountDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new AmountWithdrawn(Id, amount));
    }
    
    private void Apply(AccountCreated @event)
    {
        _balance = @event.InitialBalance;
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

Let's break down what's happening here:

1. Our Account class extends AggregateRoot, which provides event sourcing capabilities
2. We define methods that represent business operations (Create, Deposit, Withdraw)
3. These methods validate business rules and then raise events
4. Private Apply methods update the aggregate's state based on events
5. The state (_balance) is never directly modified, only through events

This pattern ensures that all state changes are captured as events, giving us a complete history.
```

### Setting Up for the Next Tutorial (2 minutes)
```
In the next tutorial, we'll set up a new project with Reactive Domain and create our first working example.

To prepare, you'll need:
1. Visual Studio or Visual Studio Code
2. .NET 7.0 or later
3. Docker (for running EventStoreDB)

You can find all the code examples and documentation at our GitHub repository: [URL]

Thanks for watching this introduction to Reactive Domain. If you have any questions, please leave them in the comments below.

See you in the next tutorial!
```

## Tutorial 2: Setting Up Your First Reactive Domain Project

### Opening (1 minute)
```
Welcome back to our Reactive Domain tutorial series. In this video, we'll set up a new project with Reactive Domain and create a simple working example.

By the end of this tutorial, you'll have a functioning event-sourced application that can create accounts, deposit and withdraw funds, and query account balances.
```

### Project Setup (5 minutes)
```
Let's start by creating a new solution with multiple projects:

[SHOW TERMINAL/IDE]

```bash
dotnet new sln -n BankingApp
dotnet new classlib -n BankingApp.Domain
dotnet new classlib -n BankingApp.Infrastructure
dotnet new classlib -n BankingApp.Application
dotnet new console -n BankingApp.Console
dotnet new xunit -n BankingApp.Tests

dotnet sln add BankingApp.Domain
dotnet sln add BankingApp.Infrastructure
dotnet sln add BankingApp.Application
dotnet sln add BankingApp.Console
dotnet sln add BankingApp.Tests
```

Now, let's add the Reactive Domain packages:

```bash
dotnet add BankingApp.Domain package ReactiveDomain.Core
dotnet add BankingApp.Domain package ReactiveDomain.Foundation
dotnet add BankingApp.Infrastructure package ReactiveDomain.Persistence
dotnet add BankingApp.Application package ReactiveDomain.Messaging
dotnet add BankingApp.Tests package ReactiveDomain.Testing
```

Let's also set up the project references:

```bash
dotnet add BankingApp.Infrastructure reference BankingApp.Domain
dotnet add BankingApp.Application reference BankingApp.Domain
dotnet add BankingApp.Application reference BankingApp.Infrastructure
dotnet add BankingApp.Console reference BankingApp.Application
dotnet add BankingApp.Tests reference BankingApp.Domain
dotnet add BankingApp.Tests reference BankingApp.Infrastructure
dotnet add BankingApp.Tests reference BankingApp.Application
```
```

### Setting Up EventStoreDB (3 minutes)
```
Before we start coding, let's set up EventStoreDB using Docker:

[SHOW TERMINAL]

```bash
docker run --name eventstore -it -p 2113:2113 -p 1113:1113 \
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

Once it's running, you can access the EventStore UI at http://localhost:2113 with the default credentials admin/changeit.

[SHOW BROWSER WITH EVENTSTORE UI]

This is where we'll be able to see all the events that our application generates.
```

### Creating the Domain Model (5 minutes)
```
Now, let's create our domain model in the BankingApp.Domain project:

[SHOW IDE]

First, let's define our events:

```csharp
// BankingApp.Domain/Events/AccountEvents.cs
using System;

namespace BankingApp.Domain.Events
{
    public class AccountCreated
    {
        public Guid AccountId { get; }
        public string Owner { get; }
        public decimal InitialBalance { get; }
        
        public AccountCreated(Guid accountId, string owner, decimal initialBalance)
        {
            AccountId = accountId;
            Owner = owner;
            InitialBalance = initialBalance;
        }
    }
    
    public class AmountDeposited
    {
        public Guid AccountId { get; }
        public decimal Amount { get; }
        
        public AmountDeposited(Guid accountId, decimal amount)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
    
    public class AmountWithdrawn
    {
        public Guid AccountId { get; }
        public decimal Amount { get; }
        
        public AmountWithdrawn(Guid accountId, decimal amount)
        {
            AccountId = accountId;
            Amount = amount;
        }
    }
}
```

Next, let's create our Account aggregate:

```csharp
// BankingApp.Domain/Aggregates/Account.cs
using System;
using BankingApp.Domain.Events;
using ReactiveDomain.Foundation;

namespace BankingApp.Domain.Aggregates
{
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
}
```
```

### Setting Up the Infrastructure (3 minutes)
```
Now, let's set up the infrastructure to connect to EventStoreDB:

[SHOW IDE]

```csharp
// BankingApp.Infrastructure/Repositories/AccountRepository.cs
using System;
using BankingApp.Domain.Aggregates;
using ReactiveDomain.Foundation;
using ReactiveDomain.Persistence;
using ReactiveDomain.Messaging;

namespace BankingApp.Infrastructure.Repositories
{
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
}
```

Now, let's create a connection factory:

```csharp
// BankingApp.Infrastructure/EventStore/EventStoreConnectionFactory.cs
using System;
using ReactiveDomain.Persistence;

namespace BankingApp.Infrastructure.EventStore
{
    public class EventStoreConnectionFactory
    {
        public static IStreamStoreConnection Create(string connectionString)
        {
            var connection = new EventStoreConnection(connectionString);
            connection.Connect();
            return connection;
        }
    }
}
```
```

### Creating the Application Services (3 minutes)
```
Let's create our application services:

[SHOW IDE]

```csharp
// BankingApp.Application/Services/AccountService.cs
using System;
using BankingApp.Domain.Aggregates;
using ReactiveDomain.Foundation;

namespace BankingApp.Application.Services
{
    public class AccountService
    {
        private readonly IRepository _repository;
        
        public AccountService(IRepository repository)
        {
            _repository = repository;
        }
        
        public Guid CreateAccount(string owner, decimal initialBalance)
        {
            var accountId = Guid.NewGuid();
            var account = new Account(accountId);
            
            account.Create(owner, initialBalance);
            _repository.Save(account);
            
            return accountId;
        }
        
        public void Deposit(Guid accountId, decimal amount)
        {
            if (!_repository.TryGetById<Account>(accountId, out var account))
                throw new InvalidOperationException($"Account {accountId} not found");
                
            account.Deposit(amount);
            _repository.Save(account);
        }
        
        public void Withdraw(Guid accountId, decimal amount)
        {
            if (!_repository.TryGetById<Account>(accountId, out var account))
                throw new InvalidOperationException($"Account {accountId} not found");
                
            account.Withdraw(amount);
            _repository.Save(account);
        }
        
        public decimal GetBalance(Guid accountId)
        {
            if (!_repository.TryGetById<Account>(accountId, out var account))
                throw new InvalidOperationException($"Account {accountId} not found");
                
            return account.GetBalance();
        }
    }
}
```
```

### Creating the Console Application (3 minutes)
```
Finally, let's create a simple console application to test our implementation:

[SHOW IDE]

```csharp
// BankingApp.Console/Program.cs
using System;
using BankingApp.Application.Services;
using BankingApp.Infrastructure.EventStore;
using BankingApp.Infrastructure.Repositories;

namespace BankingApp.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create connection to EventStoreDB
            var connectionString = "tcp://admin:changeit@localhost:1113";
            var connection = EventStoreConnectionFactory.Create(connectionString);
            
            // Create repository
            var repository = new AccountRepository(connection);
            
            // Create account service
            var accountService = new AccountService(repository);
            
            // Create a new account
            var accountId = accountService.CreateAccount("John Doe", 100);
            System.Console.WriteLine($"Created account {accountId} with balance {accountService.GetBalance(accountId)}");
            
            // Deposit funds
            accountService.Deposit(accountId, 50);
            System.Console.WriteLine($"Deposited 50, new balance: {accountService.GetBalance(accountId)}");
            
            // Withdraw funds
            accountService.Withdraw(accountId, 30);
            System.Console.WriteLine($"Withdrew 30, new balance: {accountService.GetBalance(accountId)}");
            
            // Try to withdraw too much
            try
            {
                accountService.Withdraw(accountId, 1000);
            }
            catch (InvalidOperationException ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
            }
            
            System.Console.WriteLine("Press any key to exit...");
            System.Console.ReadKey();
        }
    }
}
```
```

### Running the Application (3 minutes)
```
Now, let's run our application and see it in action:

[SHOW TERMINAL]

```bash
cd BankingApp.Console
dotnet run
```

[SHOW CONSOLE OUTPUT]

Great! Our application is working as expected. Let's check EventStoreDB to see the events that were generated:

[SHOW BROWSER WITH EVENTSTORE UI]

Here we can see all the events that our application generated:
1. AccountCreated
2. AmountDeposited
3. AmountWithdrawn

This demonstrates the power of event sourcing - we have a complete history of all changes to our account.
```

### Conclusion and Next Steps (2 minutes)
```
In this tutorial, we've set up a basic Reactive Domain project and implemented a simple banking application using event sourcing.

We've covered:
1. Setting up a multi-project solution
2. Installing Reactive Domain packages
3. Running EventStoreDB with Docker
4. Creating domain events and aggregates
5. Implementing a repository
6. Creating application services
7. Building a console application to test our implementation

In the next tutorial, we'll dive deeper into creating more complex aggregates, commands, and events, and explore how to handle more sophisticated business rules.

Thanks for watching, and see you in the next video!
```

## Additional Tutorials

The remaining tutorials would follow a similar structure, building on the foundation established in the first two tutorials:

- **Tutorial 3**: Creating more complex aggregates, commands, and events
- **Tutorial 4**: Working with repositories and event stores in more detail
- **Tutorial 5**: Building read models and projections for querying data
- **Tutorial 6**: Testing event-sourced applications with Reactive Domain
- **Tutorial 7**: Advanced patterns and best practices for production applications

Each tutorial would include:
- Clear learning objectives
- Step-by-step code examples
- Explanations of key concepts
- Practical demonstrations
- Suggestions for further exploration

[↑ Back to Top](#video-tutorial-script) | [← Back to Table of Contents](README.md)
