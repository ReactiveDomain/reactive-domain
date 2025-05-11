# ReactiveDomain.Policy Component

[← Back to Components](README.md)

The ReactiveDomain.Policy component provides infrastructure for implementing business policies, rules, and decision-making logic in a Reactive Domain application. It helps separate policy concerns from core domain logic, making the system more maintainable and flexible.

## Key Features

- Policy definition and execution
- Rule-based decision making
- Policy versioning and evolution
- Policy evaluation context
- Conditional policy application
- Policy composition

## Core Types

### Policy Infrastructure

- **Policy**: Base class for policy definitions
- **PolicyEvaluator**: Evaluates policies against a context
- **PolicyRegistry**: Registry for policy definitions
- **PolicyVersion**: Represents a version of a policy
- **PolicyResult**: Result of policy evaluation

### Rules Engine

- **Rule**: Base class for rule definitions
- **RuleSet**: Collection of rules
- **RuleEvaluator**: Evaluates rules against a context
- **RuleResult**: Result of rule evaluation
- **CompositeRule**: Composition of multiple rules

### Policy Context

- **PolicyContext**: Context for policy evaluation
- **PolicyContextBuilder**: Builder for policy contexts
- **ContextVariable**: Variable in a policy context
- **ContextAccessor**: Accessor for context variables

## Usage Examples

### Defining a Policy

```csharp
public class AccountWithdrawalPolicy : Policy
{
    public PolicyResult Evaluate(Account account, decimal amount)
    {
        var context = new PolicyContext();
        context.Set("account", account);
        context.Set("amount", amount);
        
        var rules = new RuleSet();
        rules.Add(new AccountMustBeActiveRule());
        rules.Add(new SufficientFundsRule());
        rules.Add(new WithdrawalLimitRule());
        
        return Evaluate(context, rules);
    }
}

public class AccountMustBeActiveRule : Rule
{
    public override RuleResult Evaluate(PolicyContext context)
    {
        var account = context.Get<Account>("account");
        
        if (!account.IsActive)
        {
            return RuleResult.Failure("Account is not active");
        }
        
        return RuleResult.Success();
    }
}

public class SufficientFundsRule : Rule
{
    public override RuleResult Evaluate(PolicyContext context)
    {
        var account = context.Get<Account>("account");
        var amount = context.Get<decimal>("amount");
        
        if (account.Balance < amount)
        {
            return RuleResult.Failure($"Insufficient funds. Current balance: {account.Balance}, Requested: {amount}");
        }
        
        return RuleResult.Success();
    }
}

public class WithdrawalLimitRule : Rule
{
    public override RuleResult Evaluate(PolicyContext context)
    {
        var account = context.Get<Account>("account");
        var amount = context.Get<decimal>("amount");
        
        if (amount > account.DailyWithdrawalLimit)
        {
            return RuleResult.Failure($"Withdrawal limit exceeded. Limit: {account.DailyWithdrawalLimit}, Requested: {amount}");
        }
        
        return RuleResult.Success();
    }
}
```

### Using a Policy in a Command Handler

```csharp
public class WithdrawFundsHandler : ICommandHandler<WithdrawFunds>
{
    private readonly ICorrelatedRepository _repository;
    private readonly AccountWithdrawalPolicy _withdrawalPolicy;
    
    public WithdrawFundsHandler(
        ICorrelatedRepository repository,
        AccountWithdrawalPolicy withdrawalPolicy)
    {
        _repository = repository;
        _withdrawalPolicy = withdrawalPolicy;
    }
    
    public void Handle(WithdrawFunds command)
    {
        // Load the account
        var account = _repository.GetById<Account>(command.AccountId, command);
        
        // Evaluate the policy
        var policyResult = _withdrawalPolicy.Evaluate(account, command.Amount);
        
        // Check if the policy allows the withdrawal
        if (!policyResult.IsSuccess)
        {
            throw new PolicyViolationException(policyResult.ErrorMessage);
        }
        
        // Perform the withdrawal
        account.Withdraw(command.Amount, command.Reference, command);
        
        // Save the account
        _repository.Save(account);
    }
}
```

### Composing Policies

```csharp
public class CompositeAccountPolicy : Policy
{
    private readonly AccountWithdrawalPolicy _withdrawalPolicy;
    private readonly AccountTransferPolicy _transferPolicy;
    
    public CompositeAccountPolicy(
        AccountWithdrawalPolicy withdrawalPolicy,
        AccountTransferPolicy transferPolicy)
    {
        _withdrawalPolicy = withdrawalPolicy;
        _transferPolicy = transferPolicy;
    }
    
    public PolicyResult EvaluateForTransfer(Account sourceAccount, Account targetAccount, decimal amount)
    {
        // First check if withdrawal is allowed
        var withdrawalResult = _withdrawalPolicy.Evaluate(sourceAccount, amount);
        if (!withdrawalResult.IsSuccess)
        {
            return withdrawalResult;
        }
        
        // Then check if transfer is allowed
        var transferResult = _transferPolicy.Evaluate(sourceAccount, targetAccount, amount);
        return transferResult;
    }
}
```

## Integration with Other Components

The Policy component integrates with:

- **ReactiveDomain.Core**: Uses core interfaces and types
- **ReactiveDomain.Foundation**: Provides policy infrastructure for domain components
- **ReactiveDomain.Messaging**: Integrates with command handling

## Best Practices

1. **Separate Policy from Domain Logic**: Keep policy concerns separate from core domain logic
2. **Make Policies Explicit**: Define policies explicitly rather than embedding them in domain logic
3. **Version Policies**: Version policies to track changes over time
4. **Compose Policies**: Compose complex policies from simpler ones
5. **Test Policies Thoroughly**: Write comprehensive tests for policies
6. **Document Policy Decisions**: Document the reasoning behind policy decisions
7. **Make Policies Configurable**: Allow policies to be configured without code changes where appropriate

## Common Policy Patterns

### Validation Policies

Validation policies ensure that inputs meet certain criteria before processing:

```csharp
public class AccountCreationValidationPolicy : Policy
{
    public PolicyResult Evaluate(string accountNumber, decimal initialDeposit)
    {
        var context = new PolicyContext();
        context.Set("accountNumber", accountNumber);
        context.Set("initialDeposit", initialDeposit);
        
        var rules = new RuleSet();
        rules.Add(new AccountNumberFormatRule());
        rules.Add(new MinimumInitialDepositRule());
        
        return Evaluate(context, rules);
    }
}
```

### Authorization Policies

Authorization policies determine if an action is permitted:

```csharp
public class AccountAccessPolicy : Policy
{
    public PolicyResult Evaluate(User user, Account account, AccountAction action)
    {
        var context = new PolicyContext();
        context.Set("user", user);
        context.Set("account", account);
        context.Set("action", action);
        
        var rules = new RuleSet();
        rules.Add(new UserMustBeAuthenticatedRule());
        rules.Add(new UserMustBeAuthorizedForAccountRule());
        rules.Add(new ActionMustBePermittedRule());
        
        return Evaluate(context, rules);
    }
}
```

### Business Rules Policies

Business rules policies enforce domain-specific rules:

```csharp
public class LoanApprovalPolicy : Policy
{
    public PolicyResult Evaluate(Customer customer, LoanApplication application)
    {
        var context = new PolicyContext();
        context.Set("customer", customer);
        context.Set("application", application);
        
        var rules = new RuleSet();
        rules.Add(new CreditScoreRule());
        rules.Add(new DebtToIncomeRatioRule());
        rules.Add(new LoanAmountRule());
        
        return Evaluate(context, rules);
    }
}
```

## Related Documentation

- [Command API Reference](../api-reference/types/command.md)
- [ICommandHandler API Reference](../api-reference/types/icommand-handler.md)
- [AggregateRoot API Reference](../api-reference/types/aggregate-root.md)
- [IRepository API Reference](../api-reference/types/irepository.md)
- [Process Manager API Reference](../api-reference/types/process-manager.md)

## Navigation

**Section Navigation**:
- [← Previous: ReactiveDomain.Testing](testing.md)
- [↑ Parent: Component Documentation](README.md)
- [→ Next: ReactiveDomain.IdentityStorage](identity-storage.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
