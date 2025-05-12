# Testing Aggregates and Event Handlers

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to test aggregates, command handlers, and event handlers in Reactive Domain using xUnit.

## Setting Up Test Projects

```csharp
// MyApp.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.5.0" />
    <PackageReference Include="Moq" Version="4.18.4" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="3.2.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp\MyApp.csproj" />
  </ItemGroup>

</Project>
```

## Testing Aggregates

```csharp
using System;
using Xunit;
using MyApp.Domain;

namespace MyApp.Tests.Domain
{
    public class AccountTests
    {
        [Fact]
        public void Create_ValidParameters_SetsProperties()
        {
            // Arrange
            var id = Guid.NewGuid();
            var account = new Account(id);
            
            // Act
            account.Create("ACC-123", "John Doe");
            
            // Assert
            var events = account.TakeEvents();
            Assert.Single(events);
            Assert.IsType<AccountCreated>(events[0]);
            
            var createdEvent = (AccountCreated)events[0];
            Assert.Equal(id, createdEvent.AccountId);
            Assert.Equal("ACC-123", createdEvent.AccountNumber);
            Assert.Equal("John Doe", createdEvent.CustomerName);
        }
        
        [Fact]
        public void Create_EmptyAccountNumber_ThrowsArgumentException()
        {
            // Arrange
            var account = new Account(Guid.NewGuid());
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => account.Create("", "John Doe"));
            Assert.Equal("Account number cannot be empty (Parameter 'accountNumber')", exception.Message);
        }
        
        [Fact]
        public void Deposit_PositiveAmount_IncreasesBalance()
        {
            // Arrange
            var account = new Account(Guid.NewGuid());
            account.Create("ACC-123", "John Doe");
            account.TakeEvents(); // Clear events
            
            // Act
            account.Deposit(100);
            
            // Assert
            var events = account.TakeEvents();
            Assert.Single(events);
            Assert.IsType<FundsDeposited>(events[0]);
            
            var depositEvent = (FundsDeposited)events[0];
            Assert.Equal(100, depositEvent.Amount);
            
            Assert.Equal(100, account.GetBalance());
        }
        
        [Fact]
        public void Deposit_NegativeAmount_ThrowsArgumentException()
        {
            // Arrange
            var account = new Account(Guid.NewGuid());
            account.Create("ACC-123", "John Doe");
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => account.Deposit(-100));
            Assert.Equal("Amount must be positive (Parameter 'amount')", exception.Message);
        }
        
        [Fact]
        public void Withdraw_ValidAmount_DecreasesBalance()
        {
            // Arrange
            var account = new Account(Guid.NewGuid());
            account.Create("ACC-123", "John Doe");
            account.Deposit(100);
            account.TakeEvents(); // Clear events
            
            // Act
            account.Withdraw(50);
            
            // Assert
            var events = account.TakeEvents();
            Assert.Single(events);
            Assert.IsType<FundsWithdrawn>(events[0]);
            
            var withdrawEvent = (FundsWithdrawn)events[0];
            Assert.Equal(50, withdrawEvent.Amount);
            
            Assert.Equal(50, account.GetBalance());
        }
        
        [Fact]
        public void Withdraw_InsufficientFunds_ThrowsInvalidOperationException()
        {
            // Arrange
            var account = new Account(Guid.NewGuid());
            account.Create("ACC-123", "John Doe");
            account.Deposit(100);
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => account.Withdraw(150));
            Assert.Equal("Insufficient funds", exception.Message);
        }
        
        [Fact]
        public void Close_OpenAccount_MarksAsClosed()
        {
            // Arrange
            var account = new Account(Guid.NewGuid());
            account.Create("ACC-123", "John Doe");
            account.TakeEvents(); // Clear events
            
            // Act
            account.Close();
            
            // Assert
            var events = account.TakeEvents();
            Assert.Single(events);
            Assert.IsType<AccountClosed>(events[0]);
            
            // Try to deposit after closing (should throw)
            Assert.Throws<InvalidOperationException>(() => account.Deposit(100));
        }
    }
}
```

## Testing Command Handlers

```csharp
using System;
using Moq;
using Xunit;
using ReactiveDomain.Foundation;
using MyApp.Domain;
using MyApp.Domain.Commands;
using MyApp.Domain.Handlers;

namespace MyApp.Tests.Domain.Handlers
{
    public class AccountCommandHandlerTests
    {
        [Fact]
        public void Handle_CreateAccount_CreatesAndSavesAccount()
        {
            // Arrange
            var mockRepository = new Mock<IRepository>();
            var handler = new AccountCommandHandler(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var command = new CreateAccount(accountId, "ACC-123", "John Doe");
            
            // Setup mock to capture the saved aggregate
            Account savedAccount = null;
            mockRepository.Setup(r => r.Save(It.IsAny<Account>()))
                .Callback<Account>(a => savedAccount = a);
            
            // Act
            handler.Handle(command);
            
            // Assert
            mockRepository.Verify(r => r.Save(It.IsAny<Account>()), Times.Once);
            Assert.NotNull(savedAccount);
            Assert.Equal(accountId, savedAccount.Id);
            Assert.Equal(0, savedAccount.GetBalance());
        }
        
        [Fact]
        public void Handle_DepositFunds_LoadsUpdatesAndSavesAccount()
        {
            // Arrange
            var mockRepository = new Mock<IRepository>();
            var handler = new AccountCommandHandler(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var existingAccount = new Account(accountId);
            existingAccount.Create("ACC-123", "John Doe");
            
            mockRepository.Setup(r => r.GetById<Account>(accountId))
                .Returns(existingAccount);
            
            var command = new DepositFunds(accountId, 100);
            
            // Setup mock to capture the saved aggregate
            Account savedAccount = null;
            mockRepository.Setup(r => r.Save(It.IsAny<Account>()))
                .Callback<Account>(a => savedAccount = a);
            
            // Act
            handler.Handle(command);
            
            // Assert
            mockRepository.Verify(r => r.GetById<Account>(accountId), Times.Once);
            mockRepository.Verify(r => r.Save(It.IsAny<Account>()), Times.Once);
            Assert.NotNull(savedAccount);
            Assert.Equal(100, savedAccount.GetBalance());
        }
        
        [Fact]
        public void Handle_DepositFunds_AccountNotFound_ThrowsException()
        {
            // Arrange
            var mockRepository = new Mock<IRepository>();
            var handler = new AccountCommandHandler(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            
            mockRepository.Setup(r => r.GetById<Account>(accountId))
                .Throws(new AggregateNotFoundException(accountId));
            
            var command = new DepositFunds(accountId, 100);
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => handler.Handle(command));
            Assert.Equal($"Account {accountId} not found", exception.Message);
            mockRepository.Verify(r => r.Save(It.IsAny<Account>()), Times.Never);
        }
        
        [Fact]
        public void Handle_WithdrawFunds_InsufficientFunds_ThrowsException()
        {
            // Arrange
            var mockRepository = new Mock<IRepository>();
            var handler = new AccountCommandHandler(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var existingAccount = new Account(accountId);
            existingAccount.Create("ACC-123", "John Doe");
            existingAccount.Deposit(50);
            
            mockRepository.Setup(r => r.GetById<Account>(accountId))
                .Returns(existingAccount);
            
            var command = new WithdrawFunds(accountId, 100);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => handler.Handle(command));
            mockRepository.Verify(r => r.Save(It.IsAny<Account>()), Times.Never);
        }
    }
}
```

## Testing Event Handlers

```csharp
using System;
using Moq;
using Xunit;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.ReadModels;
using MyApp.EventHandlers;

namespace MyApp.Tests.EventHandlers
{
    public class AccountReadModelUpdaterTests
    {
        [Fact]
        public void Handle_AccountCreated_CreatesReadModel()
        {
            // Arrange
            var mockRepository = new Mock<IReadModelRepository<AccountSummary>>();
            var handler = new AccountReadModelUpdater(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var @event = new AccountCreated(accountId, "ACC-123", "John Doe");
            
            // Setup mock to capture the saved read model
            AccountSummary savedModel = null;
            mockRepository.Setup(r => r.Save(It.IsAny<AccountSummary>()))
                .Callback<AccountSummary>(m => savedModel = m);
            
            // Act
            handler.Handle(@event);
            
            // Assert
            mockRepository.Verify(r => r.Save(It.IsAny<AccountSummary>()), Times.Once);
            Assert.NotNull(savedModel);
            Assert.Equal(accountId, savedModel.Id);
            Assert.Equal("ACC-123", savedModel.AccountNumber);
            Assert.Equal("John Doe", savedModel.CustomerName);
            Assert.Equal(0, savedModel.Balance);
            Assert.False(savedModel.IsClosed);
        }
        
        [Fact]
        public void Handle_FundsDeposited_UpdatesReadModel()
        {
            // Arrange
            var mockRepository = new Mock<IReadModelRepository<AccountSummary>>();
            var handler = new AccountReadModelUpdater(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var existingModel = new AccountSummary(accountId);
            existingModel.Update("ACC-123", "John Doe", 100, false);
            
            mockRepository.Setup(r => r.GetById(accountId))
                .Returns(existingModel);
            
            var @event = new FundsDeposited(accountId, 50);
            
            // Setup mock to capture the saved read model
            AccountSummary savedModel = null;
            mockRepository.Setup(r => r.Save(It.IsAny<AccountSummary>()))
                .Callback<AccountSummary>(m => savedModel = m);
            
            // Act
            handler.Handle(@event);
            
            // Assert
            mockRepository.Verify(r => r.GetById(accountId), Times.Once);
            mockRepository.Verify(r => r.Save(It.IsAny<AccountSummary>()), Times.Once);
            Assert.NotNull(savedModel);
            Assert.Equal(150, savedModel.Balance);
        }
        
        [Fact]
        public void Handle_AccountClosed_MarksReadModelAsClosed()
        {
            // Arrange
            var mockRepository = new Mock<IReadModelRepository<AccountSummary>>();
            var handler = new AccountReadModelUpdater(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var existingModel = new AccountSummary(accountId);
            existingModel.Update("ACC-123", "John Doe", 100, false);
            
            mockRepository.Setup(r => r.GetById(accountId))
                .Returns(existingModel);
            
            var @event = new AccountClosed(accountId);
            
            // Setup mock to capture the saved read model
            AccountSummary savedModel = null;
            mockRepository.Setup(r => r.Save(It.IsAny<AccountSummary>()))
                .Callback<AccountSummary>(m => savedModel = m);
            
            // Act
            handler.Handle(@event);
            
            // Assert
            mockRepository.Verify(r => r.GetById(accountId), Times.Once);
            mockRepository.Verify(r => r.Save(It.IsAny<AccountSummary>()), Times.Once);
            Assert.NotNull(savedModel);
            Assert.True(savedModel.IsClosed);
        }
    }
}
```

## Testing Projections

```csharp
using System;
using Moq;
using Xunit;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.ReadModels;
using MyApp.Projections;

namespace MyApp.Tests.Projections
{
    public class AccountSummaryProjectionTests
    {
        [Fact]
        public void When_AccountCreated_CreatesReadModel()
        {
            // Arrange
            var mockRepository = new Mock<IReadModelRepository<AccountSummary>>();
            var projection = new AccountSummaryProjection(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var @event = new AccountCreated(accountId, "ACC-123", "John Doe");
            
            // Setup mock to capture the saved read model
            AccountSummary savedModel = null;
            mockRepository.Setup(r => r.Save(It.IsAny<AccountSummary>()))
                .Callback<AccountSummary>(m => savedModel = m);
            
            // Act
            projection.When(@event);
            
            // Assert
            mockRepository.Verify(r => r.Save(It.IsAny<AccountSummary>()), Times.Once);
            Assert.NotNull(savedModel);
            Assert.Equal(accountId, savedModel.Id);
            Assert.Equal("ACC-123", savedModel.AccountNumber);
            Assert.Equal("John Doe", savedModel.CustomerName);
            Assert.Equal(0, savedModel.Balance);
            Assert.False(savedModel.IsClosed);
        }
        
        [Fact]
        public void When_FundsDeposited_UpdatesReadModel()
        {
            // Arrange
            var mockRepository = new Mock<IReadModelRepository<AccountSummary>>();
            var projection = new AccountSummaryProjection(mockRepository.Object);
            
            var accountId = Guid.NewGuid();
            var existingModel = new AccountSummary(accountId);
            existingModel.Update("ACC-123", "John Doe", 100, false);
            
            mockRepository.Setup(r => r.GetById(accountId))
                .Returns(existingModel);
            
            var @event = new FundsDeposited(accountId, 50);
            
            // Setup mock to capture the saved read model
            AccountSummary savedModel = null;
            mockRepository.Setup(r => r.Save(It.IsAny<AccountSummary>()))
                .Callback<AccountSummary>(m => savedModel = m);
            
            // Act
            projection.When(@event);
            
            // Assert
            mockRepository.Verify(r => r.GetById(accountId), Times.Once);
            mockRepository.Verify(r => r.Save(It.IsAny<AccountSummary>()), Times.Once);
            Assert.NotNull(savedModel);
            Assert.Equal(150, savedModel.Balance);
        }
    }
}
```

## Testing with In-Memory Event Store

```csharp
using System;
using System.Linq;
using Xunit;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.Domain.Commands;
using MyApp.Domain.Handlers;

namespace MyApp.Tests.Integration
{
    public class InMemoryEventStore : IRepository
    {
        private readonly Dictionary<Guid, List<object>> _eventStore = new Dictionary<Guid, List<object>>();
        
        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource
        {
            if (!_eventStore.ContainsKey(id))
            {
                throw new AggregateNotFoundException(id);
            }
            
            var aggregate = (TAggregate)Activator.CreateInstance(typeof(TAggregate), id);
            var events = _eventStore[id];
            aggregate.RestoreFromEvents(events);
            
            return aggregate;
        }
        
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource
        {
            try
            {
                aggregate = GetById<TAggregate>(id);
                return true;
            }
            catch (AggregateNotFoundException)
            {
                aggregate = null;
                return false;
            }
        }
        
        public void Save(IEventSource aggregate)
        {
            var events = aggregate.TakeEvents();
            
            if (!_eventStore.ContainsKey(aggregate.Id))
            {
                _eventStore[aggregate.Id] = new List<object>();
            }
            
            _eventStore[aggregate.Id].AddRange(events);
        }
        
        public void Update<TAggregate>(ref TAggregate aggregate) where TAggregate : class, IEventSource
        {
            // Not needed for this simple implementation
        }
        
        public void Delete(IEventSource aggregate)
        {
            // Not needed for this simple implementation
        }
        
        public void HardDelete(IEventSource aggregate)
        {
            if (_eventStore.ContainsKey(aggregate.Id))
            {
                _eventStore.Remove(aggregate.Id);
            }
        }
        
        public List<object> GetAllEvents(Guid aggregateId)
        {
            if (_eventStore.ContainsKey(aggregateId))
            {
                return _eventStore[aggregateId];
            }
            
            return new List<object>();
        }
    }
    
    public class AccountIntegrationTests
    {
        [Fact]
        public void FullAccountLifecycle_GeneratesCorrectEvents()
        {
            // Arrange
            var repository = new InMemoryEventStore();
            var handler = new AccountCommandHandler(repository);
            
            var accountId = Guid.NewGuid();
            
            // Act - Create account
            var createCommand = new CreateAccount(accountId, "ACC-123", "John Doe");
            handler.Handle(createCommand);
            
            // Act - Deposit funds
            var depositCommand = new DepositFunds(accountId, 1000);
            handler.Handle(depositCommand);
            
            // Act - Withdraw funds
            var withdrawCommand = new WithdrawFunds(accountId, 250);
            handler.Handle(withdrawCommand);
            
            // Act - Close account
            var closeCommand = new CloseAccount(accountId);
            handler.Handle(closeCommand);
            
            // Assert - Check events
            var events = repository.GetAllEvents(accountId);
            
            Assert.Equal(4, events.Count);
            Assert.IsType<AccountCreated>(events[0]);
            Assert.IsType<FundsDeposited>(events[1]);
            Assert.IsType<FundsWithdrawn>(events[2]);
            Assert.IsType<AccountClosed>(events[3]);
            
            // Assert - Check event details
            var createdEvent = (AccountCreated)events[0];
            Assert.Equal(accountId, createdEvent.AccountId);
            Assert.Equal("ACC-123", createdEvent.AccountNumber);
            Assert.Equal("John Doe", createdEvent.CustomerName);
            
            var depositEvent = (FundsDeposited)events[1];
            Assert.Equal(accountId, depositEvent.AccountId);
            Assert.Equal(1000, depositEvent.Amount);
            
            var withdrawEvent = (FundsWithdrawn)events[2];
            Assert.Equal(accountId, withdrawEvent.AccountId);
            Assert.Equal(250, withdrawEvent.Amount);
            
            var closedEvent = (AccountClosed)events[3];
            Assert.Equal(accountId, closedEvent.AccountId);
            
            // Assert - Check aggregate state
            var account = repository.GetById<Account>(accountId);
            Assert.Equal(750, account.GetBalance());
            
            // Act & Assert - Verify closed account rejects deposits
            Assert.Throws<InvalidOperationException>(() => 
            {
                var depositToClosedCommand = new DepositFunds(accountId, 100);
                handler.Handle(depositToClosedCommand);
            });
        }
    }
}
```

## Key Concepts

### Unit Testing Aggregates

- Test that commands generate the correct events
- Test that events update the aggregate state correctly
- Test that business rules are enforced
- Test that exceptions are thrown for invalid operations

### Testing Command Handlers

- Mock the repository to verify interactions
- Test that commands are processed correctly
- Test error handling and edge cases
- Verify that aggregates are saved after processing

### Testing Event Handlers

- Mock dependencies to isolate the handler
- Test that events update read models correctly
- Test error handling and edge cases
- Verify that read models are saved after processing

### Integration Testing

- Use an in-memory event store for testing
- Test complete workflows from commands to events
- Verify that the aggregate state is updated correctly
- Test interactions between components

## Best Practices

1. **Isolated Tests**: Keep tests isolated and independent
2. **Arrange-Act-Assert**: Structure tests with clear setup, action, and verification
3. **Mock Dependencies**: Use mocks to isolate the component under test
4. **Test Edge Cases**: Test boundary conditions and error scenarios
5. **Descriptive Names**: Use descriptive test names that explain the scenario and expected outcome
6. **Avoid Test Duplication**: Extract common setup code to helper methods

## Common Pitfalls

1. **Testing Implementation Details**: Focus on testing behavior, not implementation details
2. **Incomplete Test Coverage**: Ensure all business rules and edge cases are tested
3. **Brittle Tests**: Avoid tests that break with minor implementation changes
4. **Slow Tests**: Keep tests fast to encourage frequent running
5. **Complex Test Setup**: Simplify test setup with helper methods and builders

---

**Navigation**:
- [← Previous: Implementing Snapshots](implementing-snapshots.md)
- [↑ Back to Top](#testing-aggregates-and-event-handlers)
- [→ Next: Integration with ASP.NET Core](aspnet-integration.md)
