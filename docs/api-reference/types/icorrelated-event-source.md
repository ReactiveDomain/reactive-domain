# ICorrelatedEventSource

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ICorrelatedEventSource` is an interface in Reactive Domain that extends the base `IEventSource` interface to add correlation tracking capabilities to event-sourced entities.

## Overview

In complex event-driven systems, tracking the flow of messages is crucial for debugging, auditing, and understanding causal relationships. The `ICorrelatedEventSource` interface enables event-sourced entities to maintain correlation information when raising events, ensuring that the complete chain of causality can be traced through the system.

This interface is particularly important for maintaining correlation context across aggregate boundaries and ensuring that events raised by aggregates preserve the correlation information from the commands that triggered them.

## Interface Definition

```csharp
public interface ICorrelatedEventSource : IEventSource
{
    void UpdateWithEvents(IEnumerable<object> events, long expectedVersion, ICorrelatedMessage source);
    void RaiseEvent(object @event, ICorrelatedMessage source);
}
```

## Key Features

- **Correlation Tracking**: Maintains correlation and causation IDs across event-sourced entities
- **Event Sourcing**: Inherits the event sourcing capabilities from `IEventSource`
- **Command-Event Correlation**: Ensures events raised in response to commands maintain correlation
- **Debugging Support**: Makes it easier to debug complex message flows by maintaining clear relationships
- **Audit Trail**: Provides a complete audit trail of related messages for compliance and analysis

## Usage

### Implementing the Interface

Here's an example of implementing the `ICorrelatedEventSource` interface in a correlated aggregate:

```csharp
public class CorrelatedAccount : ICorrelatedEventSource
{
    private readonly EventRecorder _recorder = new EventRecorder();
    private decimal _balance;
    private bool _isActive;
    
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    public CorrelatedAccount(Guid id)
    {
        Id = id;
        ExpectedVersion = -1;
        _isActive = false;
        _balance = 0;
    }
    
    public void RestoreFromEvents(IEnumerable<object> events)
    {
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)
    {
        if (ExpectedVersion != expectedVersion)
            throw new InvalidOperationException($"Expected version {ExpectedVersion} but got {expectedVersion}");
            
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion, ICorrelatedMessage source)
    {
        // Same as above, but preserves correlation information
        if (ExpectedVersion != expectedVersion)
            throw new InvalidOperationException($"Expected version {ExpectedVersion} but got {expectedVersion}");
            
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    public object[] TakeEvents()
    {
        return _recorder.TakeEvents();
    }
    
    public void RaiseEvent(object @event)
    {
        Apply(@event);
        _recorder.Record(@event);
    }
    
    public void RaiseEvent(object @event, ICorrelatedMessage source)
    {
        // Create a correlated event using the source message
        var correlatedEvent = MessageBuilder.From(source, () => @event);
        
        Apply(correlatedEvent);
        _recorder.Record(correlatedEvent);
    }
    
    // Command handlers
    public void CreateAccount(CreateAccount command)
    {
        if (_isActive)
            throw new InvalidOperationException("Account already exists");
            
        RaiseEvent(new AccountCreated(Id, command.InitialBalance), command);
    }
    
    public void Deposit(DepositFunds command)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (command.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(command.Amount));
            
        RaiseEvent(new FundsDeposited(Id, command.Amount), command);
    }
    
    public void Withdraw(WithdrawFunds command)
    {
        if (!_isActive)
            throw new InvalidOperationException("Account is not active");
            
        if (command.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(command.Amount));
            
        if (_balance < command.Amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new FundsWithdrawn(Id, command.Amount), command);
    }
    
    // Event handlers
    private void Apply(object @event)
    {
        switch (@event)
        {
            case AccountCreated e:
                ApplyAccountCreated(e);
                break;
                
            case FundsDeposited e:
                ApplyFundsDeposited(e);
                break;
                
            case FundsWithdrawn e:
                ApplyFundsWithdrawn(e);
                break;
        }
    }
    
    private void ApplyAccountCreated(AccountCreated @event)
    {
        _isActive = true;
        _balance = @event.InitialBalance;
    }
    
    private void ApplyFundsDeposited(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void ApplyFundsWithdrawn(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

### Using with a Correlated Repository

The `ICorrelatedEventSource` interface is typically used with a correlated repository that preserves correlation information:

```csharp
public class AccountService
{
    private readonly ICorrelatedRepository _repository;
    
    public AccountService(ICorrelatedRepository repository)
    {
        _repository = repository;
    }
    
    public void HandleCreateAccount(CreateAccount command)
    {
        var account = new CorrelatedAccount(command.AccountId);
        account.CreateAccount(command);
        _repository.Save(account, command);
    }
    
    public void HandleDepositFunds(DepositFunds command)
    {
        var account = _repository.GetById<CorrelatedAccount>(command.AccountId, command);
        account.Deposit(command);
        _repository.Save(account, command);
    }
    
    public void HandleWithdrawFunds(WithdrawFunds command)
    {
        var account = _repository.GetById<CorrelatedAccount>(command.AccountId, command);
        account.Withdraw(command);
        _repository.Save(account, command);
    }
}
```

## Best Practices

1. **Always Use MessageBuilder**: Use the `MessageBuilder` factory to ensure proper correlation
2. **Pass Source Messages**: Always pass the source message when raising events
3. **Preserve Correlation Chains**: Ensure correlation information flows through the entire system
4. **Consistent Implementation**: Implement correlation tracking consistently across all entities
5. **Logging**: Include correlation IDs in logs for easier debugging
6. **Testing**: Test correlation chains to ensure they are maintained correctly
7. **Documentation**: Document the correlation flow in your system for better understanding
8. **Error Handling**: Include correlation IDs in error messages and exception details

## Common Pitfalls

1. **Missing Correlation**: Failing to pass the source message when raising events breaks the correlation chain
2. **Manual ID Setting**: Avoid manually setting correlation and causation IDs as this is error-prone
3. **Inconsistent Implementation**: Ensure all parts of your system handle correlation consistently
4. **Ignoring Correlation in Repositories**: Ensure repositories preserve correlation information
5. **Breaking Correlation Chains**: Ensure correlation information is passed through all message flows

## Related Components

- [IEventSource](./ievent-source.md): The base interface for event-sourced entities
- [ICorrelatedMessage](./icorrelated-message.md): Interface for messages with correlation information
- [MessageBuilder](./message-builder.md): Factory for creating correlated messages
- [ICorrelatedRepository](./icorrelated-repository.md): Repository that preserves correlation information
- [AggregateRoot](./aggregate-root.md): Base class for domain entities that often implements `ICorrelatedEventSource`
- [Command](./command.md): Messages that trigger state changes in correlated event sources
- [Event](./event.md): Messages that represent state changes in correlated event sources

---

**Navigation**:
- [← Previous: ProcessManager](./process-manager.md)
- [↑ Back to Top](#icorrelatedeventsource)
- [→ Next: ISnapshotSource](./isnapshot-source.md)
