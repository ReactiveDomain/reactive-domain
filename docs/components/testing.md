# ReactiveDomain.Testing Component

[← Back to Components](README.md)

The ReactiveDomain.Testing component provides tools and utilities for testing event-sourced applications built with Reactive Domain. It includes in-memory implementations of repositories, event stores, and message buses to facilitate unit and integration testing.

## Key Features

- In-memory event store for testing
- Test fixtures for aggregates and event handlers
- Assertion utilities for event-sourced systems
- Test doubles for repositories and message buses
- Snapshot testing support
- Event stream verification

## Core Types

### Test Fixtures

- **AggregateTestFixture<T>**: Test fixture for testing aggregates
- **EventHandlerTestFixture<T>**: Test fixture for testing event handlers
- **CommandHandlerTestFixture<T>**: Test fixture for testing command handlers
- **ProcessManagerTestFixture<T>**: Test fixture for testing process managers

### In-Memory Implementations

- **InMemoryRepository**: In-memory implementation of `IRepository`
- **InMemoryCorrelatedRepository**: In-memory implementation of `ICorrelatedRepository`
- **InMemoryEventStore**: In-memory implementation of event store
- **InMemoryCommandBus**: In-memory implementation of `ICommandBus`
- **InMemoryEventBus**: In-memory implementation of `IEventBus`

### Assertion Utilities

- **EventAssert**: Utilities for asserting events
- **CommandAssert**: Utilities for asserting commands
- **StreamAssert**: Utilities for asserting event streams

## Usage Examples

### Testing an Aggregate

```csharp
[Fact]
public void Account_Should_Be_Created_With_Initial_Deposit()
{
    // Arrange
    var fixture = new AggregateTestFixture<Account>();
    var accountId = Guid.NewGuid();
    var accountNumber = "12345";
    var initialDeposit = 100.0m;
    
    // Act
    fixture.When(aggregate => 
    {
        aggregate.CreateAccount(accountNumber, initialDeposit, new TestCorrelatedMessage());
    });
    
    // Assert
    fixture.ThenHasEvent<AccountCreated>(evt => 
    {
        Assert.Equal(accountId, evt.AccountId);
        Assert.Equal(accountNumber, evt.AccountNumber);
        Assert.Equal(initialDeposit, evt.InitialDeposit);
    });
}
```

### Testing an Event Handler

```csharp
[Fact]
public void AccountSummaryUpdater_Should_Update_ReadModel_When_Account_Created()
{
    // Arrange
    var readModelRepository = new InMemoryReadModelRepository<AccountSummaryReadModel>();
    var logger = new TestLogger<AccountSummaryUpdater>();
    var handler = new AccountSummaryUpdater(readModelRepository, logger);
    
    var accountId = Guid.NewGuid();
    var accountNumber = "12345";
    var initialDeposit = 100.0m;
    
    var @event = new AccountCreated(
        accountId,
        accountNumber,
        initialDeposit,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid());
    
    // Act
    handler.Handle(@event);
    
    // Assert
    var readModel = readModelRepository.GetById(accountId);
    Assert.NotNull(readModel);
    Assert.Equal(accountNumber, readModel.AccountNumber);
    Assert.Equal(initialDeposit, readModel.CurrentBalance);
    Assert.Equal(AccountSummaryReadModel.AccountStatus.Active, readModel.Status);
}
```

### Testing a Command Handler

```csharp
[Fact]
public void TransferFundsHandler_Should_Transfer_Funds_Between_Accounts()
{
    // Arrange
    var fixture = new CommandHandlerTestFixture<TransferFundsHandler>();
    
    var sourceAccountId = Guid.NewGuid();
    var targetAccountId = Guid.NewGuid();
    var amount = 50.0m;
    var reference = "Test transfer";
    
    // Create source account with initial balance
    fixture.Given(new AccountCreated(
        sourceAccountId,
        "12345",
        100.0m,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid()));
    
    // Create target account
    fixture.Given(new AccountCreated(
        targetAccountId,
        "67890",
        0.0m,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid()));
    
    // Create transfer command
    var command = new TransferFunds(
        sourceAccountId,
        targetAccountId,
        amount,
        reference,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.Empty);
    
    // Act
    fixture.When(command);
    
    // Assert
    fixture.ThenHasEvent<FundsWithdrawn>(evt => 
    {
        Assert.Equal(sourceAccountId, evt.AccountId);
        Assert.Equal(amount, evt.Amount);
        Assert.Contains(reference, evt.Reference);
    });
    
    fixture.ThenHasEvent<FundsDeposited>(evt => 
    {
        Assert.Equal(targetAccountId, evt.AccountId);
        Assert.Equal(amount, evt.Amount);
        Assert.Contains(reference, evt.Reference);
    });
    
    fixture.ThenHasEvent<TransferCompleted>(evt => 
    {
        Assert.Equal(sourceAccountId, evt.SourceAccountId);
        Assert.Equal(targetAccountId, evt.TargetAccountId);
        Assert.Equal(amount, evt.Amount);
        Assert.Equal(reference, evt.Reference);
    });
}
```

## Integration with Other Components

The Testing component integrates with:

- **ReactiveDomain.Core**: Uses core interfaces for testing
- **ReactiveDomain.Foundation**: Provides testing utilities for domain components
- **ReactiveDomain.Messaging**: Includes test doubles for messaging components
- **ReactiveDomain.Persistence**: Provides in-memory implementations of persistence components

## Best Practices

1. **Test Behavior, Not Implementation**: Focus on testing the behavior of aggregates and handlers, not their implementation details
2. **Use Test Fixtures**: Leverage the provided test fixtures for a consistent testing approach
3. **Test Event Sequences**: Verify that events are raised in the correct sequence
4. **Test Command Validation**: Ensure commands are properly validated
5. **Test Read Model Updates**: Verify that read models are updated correctly in response to events
6. **Test Process Manager Coordination**: Ensure process managers coordinate workflows correctly
7. **Use Given-When-Then Pattern**: Structure tests using the Given-When-Then pattern for clarity

## Common Testing Scenarios

### Testing Aggregate Creation

```csharp
[Fact]
public void Account_Should_Be_Created_With_Initial_Deposit()
{
    // Arrange
    var fixture = new AggregateTestFixture<Account>();
    
    // Act
    fixture.When(aggregate => 
    {
        aggregate.CreateAccount("12345", 100.0m, new TestCorrelatedMessage());
    });
    
    // Assert
    fixture.ThenHasEvent<AccountCreated>();
}
```

### Testing Business Rules

```csharp
[Fact]
public void Account_Should_Not_Allow_Withdrawal_When_Insufficient_Funds()
{
    // Arrange
    var fixture = new AggregateTestFixture<Account>();
    
    fixture.Given(new AccountCreated(
        Guid.NewGuid(),
        "12345",
        100.0m,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid()));
    
    // Act & Assert
    var exception = Assert.Throws<InsufficientFundsException>(() => 
    {
        fixture.When(aggregate => 
        {
            aggregate.Withdraw(200.0m, "Test withdrawal", new TestCorrelatedMessage());
        });
    });
    
    Assert.Contains("Insufficient funds", exception.Message);
}
```

### Testing Event Handlers

```csharp
[Fact]
public void AccountSummaryUpdater_Should_Update_ReadModel_When_Funds_Deposited()
{
    // Arrange
    var readModelRepository = new InMemoryReadModelRepository<AccountSummaryReadModel>();
    var logger = new TestLogger<AccountSummaryUpdater>();
    var handler = new AccountSummaryUpdater(readModelRepository, logger);
    
    var accountId = Guid.NewGuid();
    var initialBalance = 100.0m;
    var depositAmount = 50.0m;
    
    // Create initial read model
    var readModel = new AccountSummaryReadModel
    {
        Id = accountId,
        AccountNumber = "12345",
        CurrentBalance = initialBalance,
        Status = AccountSummaryReadModel.AccountStatus.Active
    };
    
    readModelRepository.Save(readModel);
    
    var @event = new FundsDeposited(
        accountId,
        depositAmount,
        "Test deposit",
        DateTime.UtcNow,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid());
    
    // Act
    handler.Handle(@event);
    
    // Assert
    var updatedReadModel = readModelRepository.GetById(accountId);
    Assert.Equal(initialBalance + depositAmount, updatedReadModel.CurrentBalance);
}
```

## Related Documentation

- [Testing Code Examples](../code-examples/testing.md)
- [AggregateRoot API Reference](../api-reference/types/aggregate-root.md)
- [IRepository API Reference](../api-reference/types/irepository.md)
- [ICommandHandler API Reference](../api-reference/types/icommand-handler.md)
- [IEventHandler API Reference](../api-reference/types/ievent-handler.md)

## Navigation

**Section Navigation**:
- [← Previous: ReactiveDomain.Transport](transport.md)
- [↑ Parent: Component Documentation](README.md)
- [→ Next: ReactiveDomain.Policy](policy.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
