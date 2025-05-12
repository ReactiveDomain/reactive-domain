# Error Handling and Recovery Patterns

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

This document outlines the key patterns and best practices for implementing error handling and recovery mechanisms in Reactive Domain applications. Proper error handling is essential for building resilient event-driven systems that can recover from failures and maintain data consistency.

## Table of Contents

1. [Error Handling Principles](#error-handling-principles)
2. [Command Validation](#command-validation)
3. [Exception Handling Strategies](#exception-handling-strategies)
4. [Retry Patterns](#retry-patterns)
5. [Circuit Breaker Pattern](#circuit-breaker-pattern)
6. [Compensating Transactions](#compensating-transactions)
7. [Dead Letter Queues](#dead-letter-queues)
8. [Error Logging and Monitoring](#error-logging-and-monitoring)
9. [Testing Error Scenarios](#testing-error-scenarios)
10. [Best Practices](#best-practices)
11. [Common Pitfalls](#common-pitfalls)

## Error Handling Principles

In event-driven systems, error handling requires special consideration due to the asynchronous and distributed nature of these systems. The following principles should guide your error handling strategy:

1. **Fail Fast**: Detect and report errors as early as possible
2. **Fail Safely**: Ensure that failures don't leave the system in an inconsistent state
3. **Isolate Failures**: Prevent failures in one component from cascading to others
4. **Recover Automatically**: Implement mechanisms for automatic recovery where possible
5. **Track and Log**: Maintain comprehensive error logs for troubleshooting
6. **Design for Resilience**: Anticipate failures and design systems to handle them gracefully

### Error Categories in Event-Driven Systems

In Reactive Domain applications, errors typically fall into the following categories:

1. **Validation Errors**: Invalid commands or data that fail business rules
2. **Technical Errors**: Infrastructure failures, timeouts, or connectivity issues
3. **Concurrency Errors**: Conflicts due to concurrent modifications
4. **Business Process Errors**: Failures in multi-step business processes
5. **Integration Errors**: Issues when interacting with external systems

## Command Validation

Command validation is the first line of defense against errors. By validating commands before they're processed, you can catch many issues early and provide immediate feedback to users.

### 1. Validator Pattern

The Validator pattern separates validation logic from command handling:

```csharp
public interface IValidator<T>
{
    ValidationResult Validate(T command);
}

public class ValidationResult
{
    private readonly List<string> _errors = new List<string>();
    
    public bool IsValid => !_errors.Any();
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();
    
    public void AddError(string error)
    {
        _errors.Add(error);
    }
    
    public static ValidationResult Success()
    {
        return new ValidationResult();
    }
    
    public static ValidationResult Failure(string error)
    {
        var result = new ValidationResult();
        result.AddError(error);
        return result;
    }
    
    public static ValidationResult Failure(IEnumerable<string> errors)
    {
        var result = new ValidationResult();
        foreach (var error in errors)
        {
            result.AddError(error);
        }
        return result;
    }
}

public class CreateAccountValidator : IValidator<CreateAccount>
{
    private readonly IAccountRepository _repository;
    
    public CreateAccountValidator(IAccountRepository repository)
    {
        _repository = repository;
    }
    
    public ValidationResult Validate(CreateAccount command)
    {
        var result = new ValidationResult();
        
        // Validate required fields
        if (string.IsNullOrWhiteSpace(command.AccountNumber))
        {
            result.AddError("Account number is required");
        }
        
        if (string.IsNullOrWhiteSpace(command.CustomerName))
        {
            result.AddError("Customer name is required");
        }
        
        if (command.InitialDeposit < 0)
        {
            result.AddError("Initial deposit cannot be negative");
        }
        
        // Validate business rules
        if (!result.IsValid)
        {
            return result; // Don't check database if basic validation fails
        }
        
        // Check for duplicate account number
        if (_repository.ExistsByAccountNumber(command.AccountNumber))
        {
            result.AddError($"Account number '{command.AccountNumber}' is already in use");
        }
        
        return result;
    }
}
```

### 2. Validation Middleware

Validation middleware intercepts commands and validates them before they reach the command handlers:

```csharp
public class ValidationMiddleware<TCommand>
{
    private readonly IValidator<TCommand> _validator;
    private readonly ICommandHandler<TCommand> _innerHandler;
    
    public ValidationMiddleware(
        IValidator<TCommand> validator,
        ICommandHandler<TCommand> innerHandler)
    {
        _validator = validator;
        _innerHandler = innerHandler;
    }
    
    public void Handle(TCommand command)
    {
        // Validate the command
        var validationResult = _validator.Validate(command);
        
        if (!validationResult.IsValid)
        {
            // Throw a validation exception with all validation errors
            throw new ValidationException(validationResult.Errors);
        }
        
        // Command is valid, proceed with handling
        _innerHandler.Handle(command);
    }
}

// Registration in DI container
public void ConfigureServices(IServiceCollection services)
{
    // Register validators
    services.AddTransient<IValidator<CreateAccount>, CreateAccountValidator>();
    services.AddTransient<IValidator<DepositFunds>, DepositFundsValidator>();
    
    // Register command handlers with validation
    services.AddTransient<ICommandHandler<CreateAccount>>(
        sp => new ValidationMiddleware<CreateAccount>(
            sp.GetRequiredService<IValidator<CreateAccount>>(),
            new CreateAccountHandler(
                sp.GetRequiredService<IRepository<Account>>(),
                sp.GetRequiredService<IEventBus>())));
    
    // Register other handlers similarly
}
```

### 3. Fluent Validation

Fluent validation provides a more expressive and readable way to define validation rules:

```csharp
public class CreateAccountValidator : AbstractValidator<CreateAccount>
{
    public CreateAccountValidator(IAccountRepository repository)
    {
        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Account number is required")
            .MaximumLength(20).WithMessage("Account number cannot exceed 20 characters")
            .Must(accountNumber => !repository.ExistsByAccountNumber(accountNumber))
                .WithMessage(x => $"Account number '{x.AccountNumber}' is already in use");
        
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required")
            .MaximumLength(100).WithMessage("Customer name cannot exceed 100 characters");
        
        RuleFor(x => x.InitialDeposit)
            .GreaterThanOrEqualTo(0).WithMessage("Initial deposit cannot be negative");
    }
}

// Usage with command handler
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly IValidator<CreateAccount> _validator;
    private readonly IRepository<Account> _repository;
    private readonly IEventBus _eventBus;
    
    public CreateAccountHandler(
        IValidator<CreateAccount> validator,
        IRepository<Account> repository,
        IEventBus eventBus)
    {
        _validator = validator;
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(CreateAccount command)
    {
        // Validate the command
        var validationResult = _validator.Validate(command);
        validationResult.ThrowIfInvalid();
        
        // Process the command
        var account = new Account(Guid.NewGuid());
        account.Initialize(
            command.AccountNumber,
            command.CustomerName,
            command.InitialDeposit);
        
        _repository.Save(account);
    }
}

// Extension method for validation results
public static class ValidationResultExtensions
{
    public static void ThrowIfInvalid(this ValidationResult result)
    {
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
}
```

### 4. Domain Validation vs. Application Validation

Distinguish between domain validation (business rules) and application validation (input validation):

```csharp
// Application-level validation (input validation)
public class DepositFundsValidator : IValidator<DepositFunds>
{
    public ValidationResult Validate(DepositFunds command)
    {
        var result = new ValidationResult();
        
        if (command.AccountId == Guid.Empty)
        {
            result.AddError("Account ID is required");
        }
        
        if (command.Amount <= 0)
        {
            result.AddError("Deposit amount must be greater than zero");
        }
        
        return result;
    }
}

// Domain-level validation (business rules)
public class Account : AggregateRoot
{
    private bool _isActive;
    private decimal _balance;
    
    public void Deposit(decimal amount, string description)
    {
        // Domain validation
        if (!_isActive)
        {
            throw new DomainException("Cannot deposit to an inactive account");
        }
        
        if (amount <= 0)
        {
            throw new DomainException("Deposit amount must be greater than zero");
        }
        
        // Apply the deposit
        RaiseEvent(new FundsDeposited(
            Id,
            amount,
            description,
            DateTime.UtcNow));
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
}
```

### 5. Validation Response Pattern

The Validation Response pattern provides structured feedback for validation failures:

```csharp
public class CommandResponse
{
    public bool Success { get; }
    public IReadOnlyList<string> Errors { get; }
    public Guid? AggregateId { get; }
    
    private CommandResponse(bool success, IEnumerable<string> errors = null, Guid? aggregateId = null)
    {
        Success = success;
        Errors = errors?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        AggregateId = aggregateId;
    }
    
    public static CommandResponse Successful(Guid aggregateId)
    {
        return new CommandResponse(true, aggregateId: aggregateId);
    }
    
    public static CommandResponse Failed(IEnumerable<string> errors)
    {
        return new CommandResponse(false, errors);
    }
    
    public static CommandResponse Failed(string error)
    {
        return new CommandResponse(false, new[] { error });
    }
}

// Command handler with response
public class CreateAccountHandler : ICommandHandler<CreateAccount, CommandResponse>
{
    private readonly IValidator<CreateAccount> _validator;
    private readonly IRepository<Account> _repository;
    
    public CommandResponse Handle(CreateAccount command)
    {
        // Validate the command
        var validationResult = _validator.Validate(command);
        
        if (!validationResult.IsValid)
        {
            return CommandResponse.Failed(validationResult.Errors);
        }
        
        // Process the command
        var accountId = Guid.NewGuid();
        var account = new Account(accountId);
        account.Initialize(
            command.AccountNumber,
            command.CustomerName,
            command.InitialDeposit);
        
        _repository.Save(account);
        
        return CommandResponse.Successful(accountId);
    }
}

## Exception Handling Strategies

Effective exception handling is crucial for maintaining system stability and providing meaningful feedback when errors occur. Here are key exception handling strategies for Reactive Domain applications:

### 1. Exception Hierarchy

Implement a well-structured exception hierarchy to categorize different types of errors:

```csharp
// Base exception for all application exceptions
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message) { }
    protected ApplicationException(string message, Exception innerException) 
        : base(message, innerException) { }
}

// Validation exceptions
public class ValidationException : ApplicationException
{
    public IReadOnlyList<string> ValidationErrors { get; }
    
    public ValidationException(IEnumerable<string> errors) 
        : base("Validation failed: " + string.Join(", ", errors))
    {
        ValidationErrors = errors.ToList().AsReadOnly();
    }
    
    public ValidationException(string error) 
        : this(new[] { error })
    {
    }
}

// Domain exceptions for business rule violations
public class DomainException : ApplicationException
{
    public DomainException(string message) : base(message) { }
}

// Concurrency exceptions
public class ConcurrencyException : ApplicationException
{
    public ConcurrencyException(string message) : base(message) { }
}

// Infrastructure exceptions
public class InfrastructureException : ApplicationException
{
    public InfrastructureException(string message) : base(message) { }
    public InfrastructureException(string message, Exception innerException) 
        : base(message, innerException) { }
}

// Not found exceptions
public class NotFoundException : ApplicationException
{
    public NotFoundException(string message) : base(message) { }
    
    public static NotFoundException For<T>(Guid id)
    {
        return new NotFoundException($"{typeof(T).Name} with ID {id} was not found");
    }
}
```

### 2. Global Exception Handling

Implement global exception handling to ensure consistent error responses:

```csharp
// ASP.NET Core global exception handler middleware
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new ErrorResponse
        {
            TraceId = context.TraceIdentifier
        };
        
        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = "Validation failed";
                response.Errors = validationEx.ValidationErrors;
                break;
                
            case DomainException domainEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = domainEx.Message;
                break;
                
            case NotFoundException notFoundEx:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response.Message = notFoundEx.Message;
                break;
                
            case ConcurrencyException concurrencyEx:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                response.Message = concurrencyEx.Message;
                break;
                
            case InfrastructureException infraEx:
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                response.Message = "A system error occurred. Please try again later.";
                break;
                
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response.Message = "An unexpected error occurred. Please try again later.";
                break;
        }
        
        await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
    }
    
    private class ErrorResponse
    {
        public string Message { get; set; }
        public IEnumerable<string> Errors { get; set; }
        public string TraceId { get; set; }
    }
}

// Extension method to register the middleware
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}

// Usage in Startup.cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Add global exception handler
    app.UseGlobalExceptionHandler();
    
    // Other middleware...
}
```

### 3. Try-Operation Pattern

The Try-Operation pattern provides a consistent way to handle exceptions in service methods:

```csharp
public class OperationResult<T>
{
    public bool Success { get; }
    public T Data { get; }
    public string ErrorMessage { get; }
    public Exception Exception { get; }
    
    private OperationResult(bool success, T data, string errorMessage, Exception exception)
    {
        Success = success;
        Data = data;
        ErrorMessage = errorMessage;
        Exception = exception;
    }
    
    public static OperationResult<T> Successful(T data)
    {
        return new OperationResult<T>(true, data, null, null);
    }
    
    public static OperationResult<T> Failed(string errorMessage)
    {
        return new OperationResult<T>(false, default, errorMessage, null);
    }
    
    public static OperationResult<T> Failed(Exception exception)
    {
        return new OperationResult<T>(false, default, exception.Message, exception);
    }
}

public static class Try
{
    public static OperationResult<T> Run<T>(Func<T> operation)
    {
        try
        {
            var result = operation();
            return OperationResult<T>.Successful(result);
        }
        catch (ValidationException ex)
        {
            return OperationResult<T>.Failed(ex);
        }
        catch (DomainException ex)
        {
            return OperationResult<T>.Failed(ex);
        }
        catch (NotFoundException ex)
        {
            return OperationResult<T>.Failed(ex);
        }
        catch (Exception ex)
        {
            // Log unexpected exceptions
            // logger.LogError(ex, "Unexpected error occurred");
            return OperationResult<T>.Failed(ex);
        }
    }
    
    public static async Task<OperationResult<T>> RunAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            var result = await operation();
            return OperationResult<T>.Successful(result);
        }
        catch (ValidationException ex)
        {
            return OperationResult<T>.Failed(ex);
        }
        catch (DomainException ex)
        {
            return OperationResult<T>.Failed(ex);
        }
        catch (NotFoundException ex)
        {
            return OperationResult<T>.Failed(ex);
        }
        catch (Exception ex)
        {
            // Log unexpected exceptions
            // logger.LogError(ex, "Unexpected error occurred");
            return OperationResult<T>.Failed(ex);
        }
    }
}

// Usage in a service
public class AccountService
{
    private readonly IRepository<Account> _repository;
    
    public AccountService(IRepository<Account> repository)
    {
        _repository = repository;
    }
    
    public async Task<OperationResult<AccountDto>> GetAccountAsync(Guid id)
    {
        return await Try.RunAsync(async () =>
        {
            var account = await _repository.GetByIdAsync(id);
            
            if (account == null)
                throw NotFoundException.For<Account>(id);
                
            return new AccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                Balance = account.Balance,
                IsActive = account.IsActive
            };
        });
    }
}
```

### 4. Exception Filters

Exception filters in ASP.NET Core provide a way to handle exceptions at the controller or action level:

```csharp
public class DomainExceptionFilter : IExceptionFilter
{
    private readonly ILogger<DomainExceptionFilter> _logger;
    
    public DomainExceptionFilter(ILogger<DomainExceptionFilter> logger)
    {
        _logger = logger;
    }
    
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is DomainException domainException)
        {
            _logger.LogWarning(domainException, "Domain exception occurred");
            
            var result = new ObjectResult(new
            {
                error = domainException.Message
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}

// Usage in controller
[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(DomainExceptionFilter))]
public class AccountsController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    
    public AccountsController(ICommandBus commandBus)
    {
        _commandBus = commandBus;
    }
    
    [HttpPost]
    public IActionResult CreateAccount([FromBody] CreateAccountRequest request)
    {
        var command = new CreateAccount(
            request.AccountNumber,
            request.CustomerName,
            request.InitialDeposit);
            
        var result = _commandBus.Send(command);
        
        return CreatedAtAction(
            nameof(GetAccount),
            new { id = result.AggregateId },
            null);
    }
    
    // Other actions...
}
```

### 5. Domain-Specific Exception Handling

Implement domain-specific exception handling for business rule violations:

```csharp
// Specific domain exceptions
public class InsufficientFundsException : DomainException
{
    public Guid AccountId { get; }
    public decimal AttemptedAmount { get; }
    public decimal AvailableBalance { get; }
    
    public InsufficientFundsException(
        Guid accountId,
        decimal attemptedAmount,
        decimal availableBalance)
        : base($"Insufficient funds to withdraw {attemptedAmount:C}. Available balance: {availableBalance:C}")
    {
        AccountId = accountId;
        AttemptedAmount = attemptedAmount;
        AvailableBalance = availableBalance;
    }
}

public class AccountClosedException : DomainException
{
    public Guid AccountId { get; }
    
    public AccountClosedException(Guid accountId)
        : base($"Cannot perform operation on closed account {accountId}")
    {
        AccountId = accountId;
    }
}

// Usage in domain model
public class Account : AggregateRoot
{
    private bool _isActive;
    private decimal _balance;
    
    public void Withdraw(decimal amount, string description)
    {
        if (!_isActive)
            throw new AccountClosedException(Id);
            
        if (amount <= 0)
            throw new DomainException("Withdrawal amount must be greater than zero");
            
        if (_balance < amount)
            throw new InsufficientFundsException(Id, amount, _balance);
            
        RaiseEvent(new FundsWithdrawn(
            Id,
            amount,
            description,
            DateTime.UtcNow));
    }
    
    private void Apply(FundsWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

## Retry Patterns

Retry patterns help handle transient failures in distributed systems by automatically retrying operations that might succeed if attempted again after a delay.

### 1. Basic Retry Pattern

A simple retry pattern with a fixed number of attempts and delay:

```csharp
public static class RetryHelper
{
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delay = null)
    {
        var attempts = 0;
        var actualDelay = delay ?? TimeSpan.FromSeconds(1);
        
        while (true)
        {
            try
            {
                attempts++;
                return await operation();
            }
            catch (Exception ex) when (ShouldRetry(ex) && attempts < maxAttempts)
            {
                // Log retry attempt
                // logger.LogWarning(ex, $"Retry attempt {attempts} of {maxAttempts} failed. Retrying in {actualDelay.TotalMilliseconds}ms");
                
                await Task.Delay(actualDelay);
            }
        }
    }
    
    private static bool ShouldRetry(Exception ex)
    {
        // Determine which exceptions should trigger a retry
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is SocketException ||
               ex is IOException ||
               (ex is SqlException sqlEx && IsTransientSqlException(sqlEx));
    }
    
    private static bool IsTransientSqlException(SqlException ex)
    {
        // SQL Server transient error codes
        int[] transientErrorCodes = { 4060, 40197, 40501, 40613, 49918, 49919, 49920 };
        return transientErrorCodes.Contains(ex.Number);
    }
}

// Usage
public class EventStoreRepository : IEventStoreRepository
{
    private readonly IEventStoreConnection _connection;
    
    public EventStoreRepository(IEventStoreConnection connection)
    {
        _connection = connection;
    }
    
    public async Task<IEnumerable<EventData>> GetEventsAsync(Guid aggregateId)
    {
        return await RetryHelper.RetryAsync(async () =>
        {
            var streamName = $"aggregate-{aggregateId}";
            var events = new List<EventData>();
            
            var sliceStart = 0L;
            const int sliceCount = 100;
            StreamEventsSlice slice;
            
            do
            {
                slice = await _connection.ReadStreamEventsForwardAsync(
                    streamName, sliceStart, sliceCount, false);
                
                events.AddRange(slice.Events.Select(e => new EventData
                {
                    EventId = e.Event.EventId,
                    EventType = e.Event.EventType,
                    Data = Encoding.UTF8.GetString(e.Event.Data.ToArray()),
                    Metadata = Encoding.UTF8.GetString(e.Event.Metadata.ToArray()),
                    StreamPosition = e.Event.EventNumber
                }));
                
                sliceStart = slice.NextEventNumber;
            } while (!slice.IsEndOfStream);
            
            return events;
        });
    }
}
```

### 2. Exponential Backoff

Exponential backoff increases the delay between retry attempts to reduce system load during recovery:

```csharp
public static class RetryWithExponentialBackoff
{
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 5,
        TimeSpan? initialDelay = null,
        double backoffFactor = 2.0,
        TimeSpan? maxDelay = null)
    {
        var attempts = 0;
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(200);
        var maxDelayValue = maxDelay ?? TimeSpan.FromSeconds(30);
        
        while (true)
        {
            try
            {
                attempts++;
                return await operation();
            }
            catch (Exception ex) when (ShouldRetry(ex) && attempts < maxAttempts)
            {
                // Calculate next delay with exponential backoff
                var nextDelay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * backoffFactor, maxDelayValue.TotalMilliseconds));
                
                // Add jitter to avoid thundering herd problem
                var jitteredDelay = AddJitter(delay);
                
                // Log retry attempt
                // logger.LogWarning(ex, $"Retry attempt {attempts} of {maxAttempts} failed. Retrying in {jitteredDelay.TotalMilliseconds}ms");
                
                await Task.Delay(jitteredDelay);
                delay = nextDelay;
            }
        }
    }
    
    private static TimeSpan AddJitter(TimeSpan delay)
    {
        var random = new Random();
        var jitter = random.NextDouble() * 0.3 - 0.15; // -15% to +15%
        var jitteredDelayMs = delay.TotalMilliseconds * (1 + jitter);
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }
    
    private static bool ShouldRetry(Exception ex)
    {
        // Same implementation as before
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is SocketException ||
               ex is IOException ||
               (ex is SqlException sqlEx && IsTransientSqlException(sqlEx));
    }
    
    private static bool IsTransientSqlException(SqlException ex)
    {
        // Same implementation as before
        int[] transientErrorCodes = { 4060, 40197, 40501, 40613, 49918, 49919, 49920 };
        return transientErrorCodes.Contains(ex.Number);
    }
}
```

### 3. Polly for Resilience Policies

Polly is a .NET resilience and transient-fault-handling library that provides a fluent API for defining retry policies:

```csharp
public class ResilientEventStore : IEventStore
{
    private readonly IEventStore _innerEventStore;
    private readonly ILogger<ResilientEventStore> _logger;
    private readonly AsyncPolicy _retryPolicy;
    
    public ResilientEventStore(
        IEventStore innerEventStore,
        ILogger<ResilientEventStore> logger)
    {
        _innerEventStore = innerEventStore;
        _logger = logger;
        
        // Define retry policy with Polly
        _retryPolicy = Policy
            .Handle<EventStoreConnectionException>()
            .Or<SocketException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(ex, 
                        "Error accessing event store. Retry attempt {RetryCount} after {RetryDelay}ms", 
                        retryCount, timeSpan.TotalMilliseconds);
                });
    }
    
    public async Task<IEnumerable<EventData>> GetEventsAsync(Guid aggregateId)
    {
        return await _retryPolicy.ExecuteAsync(() => 
            _innerEventStore.GetEventsAsync(aggregateId));
    }
    
    public async Task SaveEventsAsync(Guid aggregateId, IEnumerable<object> events, long expectedVersion)
    {
        await _retryPolicy.ExecuteAsync(() => 
            _innerEventStore.SaveEventsAsync(aggregateId, events, expectedVersion));
    }
}

// Registration in DI container
public void ConfigureServices(IServiceCollection services)
{
    // Register the inner event store
    services.AddSingleton<IEventStoreConnection>(provider =>
    {
        var connectionSettings = ConnectionSettings.Create()
            .EnableVerboseLogging()
            .UseConsoleLogger()
            .Build();
            
        var connection = EventStoreConnection.Create(
            connectionSettings,
            new Uri("tcp://admin:changeit@localhost:1113"));
            
        connection.ConnectAsync().Wait();
        return connection;
    });
    
    services.AddSingleton<IEventStore, EventStore>();
    
    // Register the resilient decorator
    services.Decorate<IEventStore>((inner, provider) =>
        new ResilientEventStore(
            inner,
            provider.GetRequiredService<ILogger<ResilientEventStore>>()));
}
```

### 4. Retry with Circuit Breaker

Combine retry with circuit breaker to prevent repeated retries when a service is unavailable:

```csharp
public class ResilientCommandBus : ICommandBus
{
    private readonly ICommandBus _innerCommandBus;
    private readonly ILogger<ResilientCommandBus> _logger;
    private readonly AsyncPolicyWrap _resilientPolicy;
    
    public ResilientCommandBus(
        ICommandBus innerCommandBus,
        ILogger<ResilientCommandBus> logger)
    {
        _innerCommandBus = innerCommandBus;
        _logger = logger;
        
        // Define retry policy
        var retryPolicy = Policy
            .Handle<Exception>(ex => ShouldRetry(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(ex, 
                        "Error executing command. Retry attempt {RetryCount} after {RetryDelay}ms", 
                        retryCount, timeSpan.TotalMilliseconds);
                });
        
        // Define circuit breaker policy
        var circuitBreakerPolicy = Policy
            .Handle<Exception>(ex => ShouldRetry(ex))
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (ex, breakDelay) =>
                {
                    _logger.LogError(ex, 
                        "Circuit breaker opened for {BreakDelay}ms due to: {ExceptionMessage}", 
                        breakDelay.TotalMilliseconds, ex.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open, next call is a trial");
                });
        
        // Combine policies
        _resilientPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }
    
    public async Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        await _resilientPolicy.ExecuteAsync(() => 
            _innerCommandBus.SendAsync(command));
    }
    
    private bool ShouldRetry(Exception ex)
    {
        // Determine which exceptions should trigger a retry
        return !(ex is ValidationException || ex is DomainException);
    }
}
```

## Circuit Breaker Pattern

The Circuit Breaker pattern prevents an application from repeatedly trying to execute an operation that's likely to fail, allowing it to continue without waiting for the fault to be fixed or wasting CPU cycles.

### 1. Basic Circuit Breaker

A simple implementation of the Circuit Breaker pattern:

```csharp
public enum CircuitState
{
    Closed,     // Normal operation - requests go through
    Open,       // Circuit is broken - requests fail fast
    HalfOpen    // Testing if the circuit can be closed again
}

public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger _logger;
    
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    
    public CircuitBreaker(
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null,
        ILogger logger = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger;
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        await CheckStateAsync();
        
        try
        {
            var result = await operation();
            Reset(); // Success, reset the circuit
            return result;
        }
        catch (Exception ex)
        {
            TrackFailure(ex);
            throw; // Re-throw the original exception
        }
    }
    
    private async Task CheckStateAsync()
    {
        if (_state == CircuitState.Open)
        {
            // Check if the timeout has expired
            if (DateTime.UtcNow - _lastFailureTime > _resetTimeout)
            {
                // Move to half-open state
                _state = CircuitState.HalfOpen;
                _logger?.LogInformation("Circuit breaker state changed from Open to Half-Open");
            }
            else
            {
                // Circuit is still open, fail fast
                _logger?.LogWarning("Circuit breaker is Open - failing fast");
                throw new CircuitBreakerOpenException("Circuit breaker is open");
            }
        }
    }
    
    private void TrackFailure(Exception ex)
    {
        _lastFailureTime = DateTime.UtcNow;
        
        if (_state == CircuitState.HalfOpen)
        {
            // If we're testing the circuit and it failed, open the circuit again
            _state = CircuitState.Open;
            _logger?.LogWarning(ex, "Circuit breaker trial call failed, resetting to Open state");
        }
        else if (_state == CircuitState.Closed)
        {
            // Increment the failure counter
            _failureCount++;
            
            if (_failureCount >= _failureThreshold)
            {
                // Too many failures, open the circuit
                _state = CircuitState.Open;
                _logger?.LogWarning(ex, "Circuit breaker threshold reached ({FailureCount}/{FailureThreshold}), changing to Open state", 
                    _failureCount, _failureThreshold);
            }
        }
    }
    
    private void Reset()
    {
        if (_state != CircuitState.Closed)
        {
            _logger?.LogInformation("Circuit breaker reset to Closed state");
        }
        
        _failureCount = 0;
        _state = CircuitState.Closed;
    }
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

// Usage
public class ExternalServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly CircuitBreaker _circuitBreaker;
    
    public ExternalServiceClient(
        HttpClient httpClient,
        ILogger<ExternalServiceClient> logger)
    {
        _httpClient = httpClient;
        _circuitBreaker = new CircuitBreaker(
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromMinutes(1),
            logger: logger);
    }
    
    public async Task<string> GetDataAsync(string endpoint)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        });
    }
}
```

### 2. Advanced Circuit Breaker with Monitoring

An advanced circuit breaker with monitoring capabilities:

```csharp
public class MonitoredCircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger _logger;
    private readonly IMetricsReporter _metrics;
    
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private readonly string _circuitName;
    
    // Metrics
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _shortCircuitedRequests;
    
    public MonitoredCircuitBreaker(
        string circuitName,
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null,
        ILogger logger = null,
        IMetricsReporter metrics = null)
    {
        _circuitName = circuitName;
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger;
        _metrics = metrics;
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        Interlocked.Increment(ref _totalRequests);
        
        await CheckStateAsync();
        
        try
        {
            var result = await operation();
            
            Interlocked.Increment(ref _successfulRequests);
            Reset(); // Success, reset the circuit
            
            return result;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedRequests);
            TrackFailure(ex);
            
            // Report metrics
            _metrics?.IncrementCounter($"{_circuitName}.failures");
            
            throw; // Re-throw the original exception
        }
    }
    
    private async Task CheckStateAsync()
    {
        if (_state == CircuitState.Open)
        {
            // Check if the timeout has expired
            if (DateTime.UtcNow - _lastFailureTime > _resetTimeout)
            {
                // Move to half-open state
                _state = CircuitState.HalfOpen;
                _logger?.LogInformation("Circuit {CircuitName} state changed from Open to Half-Open", _circuitName);
                
                // Report state change
                _metrics?.SetGauge($"{_circuitName}.state", 1); // 0=Closed, 1=HalfOpen, 2=Open
            }
            else
            {
                // Circuit is still open, fail fast
                Interlocked.Increment(ref _shortCircuitedRequests);
                
                // Report metrics
                _metrics?.IncrementCounter($"{_circuitName}.short_circuits");
                
                _logger?.LogWarning("Circuit {CircuitName} is Open - failing fast", _circuitName);
                throw new CircuitBreakerOpenException($"Circuit {_circuitName} is open");
            }
        }
    }
    
    private void TrackFailure(Exception ex)
    {
        _lastFailureTime = DateTime.UtcNow;
        
        if (_state == CircuitState.HalfOpen)
        {
            // If we're testing the circuit and it failed, open the circuit again
            _state = CircuitState.Open;
            _logger?.LogWarning(ex, "Circuit {CircuitName} trial call failed, resetting to Open state", _circuitName);
            
            // Report state change
            _metrics?.SetGauge($"{_circuitName}.state", 2); // 0=Closed, 1=HalfOpen, 2=Open
        }
        else if (_state == CircuitState.Closed)
        {
            // Increment the failure counter
            _failureCount++;
            
            if (_failureCount >= _failureThreshold)
            {
                // Too many failures, open the circuit
                _state = CircuitState.Open;
                _logger?.LogWarning(ex, "Circuit {CircuitName} threshold reached ({FailureCount}/{FailureThreshold}), changing to Open state", 
                    _circuitName, _failureCount, _failureThreshold);
                
                // Report state change
                _metrics?.SetGauge($"{_circuitName}.state", 2); // 0=Closed, 1=HalfOpen, 2=Open
            }
        }
    }
    
    private void Reset()
    {
        if (_state != CircuitState.Closed)
        {
            _logger?.LogInformation("Circuit {CircuitName} reset to Closed state", _circuitName);
            
            // Report state change
            _metrics?.SetGauge($"{_circuitName}.state", 0); // 0=Closed, 1=HalfOpen, 2=Open
        }
        
        _failureCount = 0;
        _state = CircuitState.Closed;
    }
    
    public CircuitBreakerMetrics GetMetrics()
    {
        return new CircuitBreakerMetrics
        {
            CircuitName = _circuitName,
            State = _state,
            FailureCount = _failureCount,
            TotalRequests = _totalRequests,
            SuccessfulRequests = _successfulRequests,
            FailedRequests = _failedRequests,
            ShortCircuitedRequests = _shortCircuitedRequests,
            LastFailureTime = _lastFailureTime,
            SuccessRate = _totalRequests > 0 
                ? (double)_successfulRequests / _totalRequests 
                : 0
        };
    }
}

public class CircuitBreakerMetrics
{
    public string CircuitName { get; set; }
    public CircuitState State { get; set; }
    public int FailureCount { get; set; }
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public long ShortCircuitedRequests { get; set; }
    public DateTime LastFailureTime { get; set; }
    public double SuccessRate { get; set; }
}

public interface IMetricsReporter
{
    void IncrementCounter(string name, double value = 1);
    void SetGauge(string name, double value);
    void RecordTiming(string name, TimeSpan duration);
}
```

### 3. Circuit Breaker Factory

A factory for creating and managing circuit breakers:

```csharp
public class CircuitBreakerFactory
{
    private readonly ConcurrentDictionary<string, MonitoredCircuitBreaker> _circuitBreakers = 
        new ConcurrentDictionary<string, MonitoredCircuitBreaker>();
    private readonly ILogger<CircuitBreakerFactory> _logger;
    private readonly IMetricsReporter _metrics;
    
    public CircuitBreakerFactory(
        ILogger<CircuitBreakerFactory> logger,
        IMetricsReporter metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }
    
    public MonitoredCircuitBreaker GetOrCreate(
        string circuitName,
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null)
    {
        return _circuitBreakers.GetOrAdd(
            circuitName,
            name => new MonitoredCircuitBreaker(
                name,
                failureThreshold,
                resetTimeout,
                _logger,
                _metrics));
    }
    
    public IReadOnlyDictionary<string, CircuitBreakerMetrics> GetAllMetrics()
    {
        return _circuitBreakers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetMetrics());
    }
}

// Usage with dependency injection
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register metrics reporter
        services.AddSingleton<IMetricsReporter, PrometheusMetricsReporter>();
        
        // Register circuit breaker factory
        services.AddSingleton<CircuitBreakerFactory>();
        
        // Register services that use circuit breakers
        services.AddHttpClient<IExternalServiceClient, ExternalServiceClient>();
    }
}

public class ExternalServiceClient : IExternalServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly MonitoredCircuitBreaker _circuitBreaker;
    
    public ExternalServiceClient(
        HttpClient httpClient,
        CircuitBreakerFactory circuitBreakerFactory)
    {
        _httpClient = httpClient;
        _circuitBreaker = circuitBreakerFactory.GetOrCreate(
            "external-service",
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromMinutes(1));
    }
    
    public async Task<string> GetDataAsync(string endpoint)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        });
    }
}
```

### 4. Circuit Breaker with Fallback

Combine circuit breaker with fallback for graceful degradation:

```csharp
public class ResilientService<T>
{
    private readonly Func<Task<T>> _primaryOperation;
    private readonly Func<Task<T>> _fallbackOperation;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger _logger;
    
    public ResilientService(
        Func<Task<T>> primaryOperation,
        Func<Task<T>> fallbackOperation,
        CircuitBreaker circuitBreaker,
        ILogger logger)
    {
        _primaryOperation = primaryOperation;
        _fallbackOperation = fallbackOperation;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }
    
    public async Task<T> ExecuteAsync()
    {
        try
        {
            // Try the primary operation with circuit breaker
            return await _circuitBreaker.ExecuteAsync(_primaryOperation);
        }
        catch (CircuitBreakerOpenException)
        {
            // Circuit is open, use fallback immediately
            _logger.LogWarning("Circuit breaker open, using fallback");
            return await _fallbackOperation();
        }
        catch (Exception ex)
        {
            // Primary operation failed, try fallback
            _logger.LogWarning(ex, "Primary operation failed, using fallback");
            return await _fallbackOperation();
        }
    }
}

// Usage
public class ProductService
{
    private readonly ResilientService<List<Product>> _productListingService;
    
    public ProductService(
        IExternalServiceClient externalServiceClient,
        ILocalCacheService cacheService,
        CircuitBreakerFactory circuitBreakerFactory,
        ILogger<ProductService> logger)
    {
        // Primary operation - get from external service
        Func<Task<List<Product>>> primaryOperation = async () =>
        {
            var data = await externalServiceClient.GetDataAsync("/api/products");
            var products = JsonConvert.DeserializeObject<List<Product>>(data);
            
            // Update cache with fresh data
            await cacheService.SetAsync("products", products, TimeSpan.FromHours(1));
            
            return products;
        };
        
        // Fallback operation - get from cache or return empty list
        Func<Task<List<Product>>> fallbackOperation = async () =>
        {
            var cachedProducts = await cacheService.GetAsync<List<Product>>("products");
            return cachedProducts ?? new List<Product>();
        };
        
        // Create circuit breaker
        var circuitBreaker = circuitBreakerFactory.GetOrCreate("product-service");
        
        // Create resilient service
        _productListingService = new ResilientService<List<Product>>(
            primaryOperation,
            fallbackOperation,
            circuitBreaker,
            logger);
    }
    
    public async Task<List<Product>> GetProductsAsync()
    {
        return await _productListingService.ExecuteAsync();
    }
}
```

## Bulkhead Pattern

The Bulkhead pattern isolates elements of an application into pools so that if one fails, the others will continue to function. It's named after the sectioned partitions (bulkheads) of a ship's hull that prevent the entire ship from flooding when one section is compromised.

### 1. Thread Pool Isolation

Isolate operations by using dedicated thread pools:

```csharp
public class BulkheadThreadPool
{
    private readonly string _name;
    private readonly int _maxConcurrency;
    private readonly int _queueSize;
    private readonly ILogger _logger;
    
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<TaskCompletionSource<bool>> _queue;
    
    public BulkheadThreadPool(
        string name,
        int maxConcurrency,
        int queueSize,
        ILogger logger = null)
    {
        _name = name;
        _maxConcurrency = maxConcurrency;
        _queueSize = queueSize;
        _logger = logger;
        
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _queue = new ConcurrentQueue<TaskCompletionSource<bool>>();
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        // Try to enter the semaphore immediately
        if (await _semaphore.WaitAsync(0, cancellationToken))
        {
            try
            {
                _logger?.LogDebug("Executing operation in bulkhead {BulkheadName} (direct execution)", _name);
                return await operation();
            }
            finally
            {
                // Process the next queued item if any
                ProcessQueuedOperation();
                
                // Release the semaphore
                _semaphore.Release();
            }
        }
        
        // If we can't enter the semaphore immediately, try to queue
        if (_queue.Count >= _queueSize)
        {
            _logger?.LogWarning("Bulkhead {BulkheadName} rejected execution - queue full", _name);
            throw new BulkheadRejectedException($"Bulkhead {_name} rejected execution - queue full");
        }
        
        // Add to queue
        _queue.Enqueue(tcs);
        _logger?.LogDebug("Operation queued in bulkhead {BulkheadName}, queue size: {QueueSize}", _name, _queue.Count);
        
        // Wait for our turn or cancellation
        using var registration = cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled();
        });
        
        try
        {
            await tcs.Task; // Wait until we're allowed to proceed
            _logger?.LogDebug("Executing operation in bulkhead {BulkheadName} (from queue)", _name);
            return await operation();
        }
        finally
        {
            // Process the next queued item if any
            ProcessQueuedOperation();
            
            // Release the semaphore
            _semaphore.Release();
        }
    }
    
    private void ProcessQueuedOperation()
    {
        if (_queue.TryDequeue(out var nextTcs))
        {
            nextTcs.TrySetResult(true);
        }
    }
    
    public BulkheadStatistics GetStatistics()
    {
        return new BulkheadStatistics
        {
            Name = _name,
            MaxConcurrency = _maxConcurrency,
            AvailableConcurrency = _semaphore.CurrentCount,
            QueueSize = _queueSize,
            QueuedOperations = _queue.Count
        };
    }
}

public class BulkheadStatistics
{
    public string Name { get; set; }
    public int MaxConcurrency { get; set; }
    public int AvailableConcurrency { get; set; }
    public int QueueSize { get; set; }
    public int QueuedOperations { get; set; }
}

public class BulkheadRejectedException : Exception
{
    public BulkheadRejectedException(string message) : base(message) { }
}

// Usage
public class ExternalServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly BulkheadThreadPool _bulkhead;
    
    public ExternalServiceClient(
        HttpClient httpClient,
        ILogger<ExternalServiceClient> logger)
    {
        _httpClient = httpClient;
        _bulkhead = new BulkheadThreadPool(
            "external-service",
            maxConcurrency: 10,  // Max concurrent requests
            queueSize: 20,       // Max queued requests
            logger: logger);
    }
    
    public async Task<string> GetDataAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _bulkhead.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }, cancellationToken);
    }
}
```

### 2. Service Isolation with Dependency Injection

Isolate services using dependency injection and dedicated resources:

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure main database connection
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("MainDatabase")));
        
        // Configure read-only database connection for reporting
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("ReportingReadOnlyDatabase")));
        
        // Configure HTTP clients with different connection pools
        services.AddHttpClient("critical-service", client =>
        {
            client.BaseAddress = new Uri("https://critical-service.example.com");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 100,  // Dedicated connection pool
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        });
        
        services.AddHttpClient("non-critical-service", client =>
        {
            client.BaseAddress = new Uri("https://non-critical-service.example.com");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 20,   // Smaller connection pool
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });
        
        // Register bulkhead factories
        services.AddSingleton<BulkheadFactory>();
        
        // Register services
        services.AddScoped<ICriticalService, CriticalService>();
        services.AddScoped<INonCriticalService, NonCriticalService>();
    }
}

public class BulkheadFactory
{
    private readonly ConcurrentDictionary<string, BulkheadThreadPool> _bulkheads = 
        new ConcurrentDictionary<string, BulkheadThreadPool>();
    private readonly ILogger<BulkheadFactory> _logger;
    
    public BulkheadFactory(ILogger<BulkheadFactory> logger)
    {
        _logger = logger;
    }
    
    public BulkheadThreadPool GetOrCreate(
        string name,
        int maxConcurrency,
        int queueSize)
    {
        return _bulkheads.GetOrAdd(
            name,
            key => new BulkheadThreadPool(
                key,
                maxConcurrency,
                queueSize,
                _logger));
    }
    
    public IReadOnlyDictionary<string, BulkheadStatistics> GetAllStatistics()
    {
        return _bulkheads.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetStatistics());
    }
}

public class CriticalService : ICriticalService
{
    private readonly HttpClient _httpClient;
    private readonly BulkheadThreadPool _bulkhead;
    
    public CriticalService(
        IHttpClientFactory httpClientFactory,
        BulkheadFactory bulkheadFactory)
    {
        _httpClient = httpClientFactory.CreateClient("critical-service");
        _bulkhead = bulkheadFactory.GetOrCreate(
            "critical-service",
            maxConcurrency: 50,
            queueSize: 100);
    }
    
    public async Task<string> GetCriticalDataAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _bulkhead.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }, cancellationToken);
    }
}

public class NonCriticalService : INonCriticalService
{
    private readonly HttpClient _httpClient;
    private readonly BulkheadThreadPool _bulkhead;
    
    public NonCriticalService(
        IHttpClientFactory httpClientFactory,
        BulkheadFactory bulkheadFactory)
    {
        _httpClient = httpClientFactory.CreateClient("non-critical-service");
        _bulkhead = bulkheadFactory.GetOrCreate(
            "non-critical-service",
            maxConcurrency: 10,
            queueSize: 20);
    }
    
    public async Task<string> GetNonCriticalDataAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _bulkhead.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }, cancellationToken);
    }
}
```

### 3. Task Scheduler Isolation

Use custom task schedulers to isolate different workloads:

```csharp
public class LimitedConcurrencyTaskScheduler : TaskScheduler
{
    private readonly int _maxDegreeOfParallelism;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<Task> _tasks = new ConcurrentQueue<Task>();
    private readonly string _name;
    private readonly ILogger _logger;
    
    public LimitedConcurrencyTaskScheduler(
        string name,
        int maxDegreeOfParallelism,
        ILogger logger = null)
    {
        _name = name;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        _logger = logger;
    }
    
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return _tasks.ToArray();
    }
    
    protected override void QueueTask(Task task)
    {
        _tasks.Enqueue(task);
        _logger?.LogDebug("Task queued in scheduler {SchedulerName}", _name);
        
        ThreadPool.QueueUserWorkItem(_ => TryExecuteTask());
    }
    
    private void TryExecuteTask()
    {
        if (_semaphore.Wait(0))
        {
            try
            {
                if (_tasks.TryDequeue(out var task))
                {
                    _logger?.LogDebug("Executing task in scheduler {SchedulerName}", _name);
                    TryExecuteTask(task);
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            // Check if there are more tasks to process
            if (!_tasks.IsEmpty)
            {
                ThreadPool.QueueUserWorkItem(_ => TryExecuteTask());
            }
        }
    }
    
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // Don't execute tasks inline to maintain concurrency control
        return false;
    }
    
    public TaskFactory CreateTaskFactory()
    {
        return new TaskFactory(this);
    }
    
    public int CurrentlyExecutingTaskCount => _maxDegreeOfParallelism - _semaphore.CurrentCount;
    
    public int QueuedTaskCount => _tasks.Count;
}

// Usage with task factory
public class IsolatedTaskService
{
    private readonly TaskFactory _criticalTaskFactory;
    private readonly TaskFactory _nonCriticalTaskFactory;
    private readonly ILogger<IsolatedTaskService> _logger;
    
    public IsolatedTaskService(ILogger<IsolatedTaskService> logger)
    {
        _logger = logger;
        
        var criticalScheduler = new LimitedConcurrencyTaskScheduler(
            "critical-tasks",
            maxDegreeOfParallelism: 4,
            logger: logger);
            
        var nonCriticalScheduler = new LimitedConcurrencyTaskScheduler(
            "non-critical-tasks",
            maxDegreeOfParallelism: 2,
            logger: logger);
            
        _criticalTaskFactory = criticalScheduler.CreateTaskFactory();
        _nonCriticalTaskFactory = nonCriticalScheduler.CreateTaskFactory();
    }
    
    public async Task ProcessCriticalWorkAsync(Func<Task> workItem)
    {
        await _criticalTaskFactory.StartNew(async () =>
        {
            try
            {
                _logger.LogInformation("Processing critical work item");
                await workItem();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing critical work item");
                throw;
            }
        }).Unwrap();
    }
    
    public async Task ProcessNonCriticalWorkAsync(Func<Task> workItem)
    {
        await _nonCriticalTaskFactory.StartNew(async () =>
        {
            try
            {
                _logger.LogInformation("Processing non-critical work item");
                await workItem();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing non-critical work item");
                // Swallow exception for non-critical work
            }
        }).Unwrap();
    }
}
```

### 4. Resource Isolation with Timeouts

Combine bulkheads with timeouts to prevent resource exhaustion:

```csharp
public class ResourceIsolationService
{
    private readonly BulkheadThreadPool _bulkhead;
    private readonly ILogger<ResourceIsolationService> _logger;
    
    public ResourceIsolationService(
        BulkheadFactory bulkheadFactory,
        ILogger<ResourceIsolationService> logger)
    {
        _logger = logger;
        _bulkhead = bulkheadFactory.GetOrCreate(
            "resource-service",
            maxConcurrency: 5,
            queueSize: 10);
    }
    
    public async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Create a new token source that will timeout after the specified duration
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);
        
        try
        {
            return await _bulkhead.ExecuteAsync(async () =>
            {
                try
                {
                    return await operation(linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    _logger.LogWarning("Operation timed out after {Timeout}ms", timeout.TotalMilliseconds);
                    throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms");
                }
            }, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("Operation timed out after {Timeout}ms", timeout.TotalMilliseconds);
            throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms");
        }
    }
}

// Usage with dependency injection
public class DataService
{
    private readonly ResourceIsolationService _isolationService;
    private readonly HttpClient _httpClient;
    
    public DataService(
        ResourceIsolationService isolationService,
        IHttpClientFactory httpClientFactory)
    {
        _isolationService = isolationService;
        _httpClient = httpClientFactory.CreateClient();
    }
    
    public async Task<string> GetDataWithTimeoutAsync(string url, CancellationToken cancellationToken = default)
    {
        return await _isolationService.ExecuteWithTimeoutAsync(async (token) =>
        {
            var response = await _httpClient.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }, TimeSpan.FromSeconds(5), cancellationToken);
    }
}
```

## Timeout Pattern

The Timeout pattern prevents an operation from blocking indefinitely by setting a maximum time limit for its completion. This is essential in distributed systems where external services may be slow or unresponsive.

### 1. Basic Timeout with CancellationToken

Implement a simple timeout using `CancellationToken`:

public static class TimeoutExtensions
{
    public static async Task<T> WithTimeout<T>(
        this Task<T> task,
        TimeSpan timeout,
        string operationName = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);
        
        try
        {
            return await task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Operation {operationName ?? "unknown"} timed out after {timeout.TotalMilliseconds}ms");
        }
    }
    
    public static async Task WithTimeout(
        this Task task,
        TimeSpan timeout,
        string operationName = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);
        
        try
        {
            await task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Operation {operationName ?? "unknown"} timed out after {timeout.TotalMilliseconds}ms");
        }
    }
}

// Usage
public class ExternalServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalServiceClient> _logger;
    
    public ExternalServiceClient(
        HttpClient httpClient,
        ILogger<ExternalServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<string> GetDataWithTimeoutAsync(
        string endpoint,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        
        try
        {
            return await _httpClient.GetStringAsync(endpoint, cancellationToken)
                .WithTimeout(effectiveTimeout, $"HTTP GET {endpoint}", cancellationToken);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Request to {Endpoint} timed out after {Timeout}ms",
                endpoint, effectiveTimeout.TotalMilliseconds);
            throw;
        }
    }
}
```

### 2. Timeout with Graceful Cancellation

Implement a timeout that attempts to gracefully cancel the operation:

```csharp
public class GracefulTimeoutHandler
{
    private readonly ILogger _logger;
    
    public GracefulTimeoutHandler(ILogger logger = null)
    {
        _logger = logger;
    }
    
    public async Task<(bool Success, T Result, Exception Error)> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        Func<Task> cleanupAction = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);
        
        var operationTask = operation(linkedCts.Token);
        
        try
        {
            var result = await operationTask;
            return (true, result, null);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
        {
            _logger?.LogWarning("Operation timed out after {Timeout}ms", timeout.TotalMilliseconds);
            
            // Try to execute cleanup action if provided
            if (cleanupAction != null)
            {
                try
                {
                    _logger?.LogDebug("Executing cleanup action after timeout");
                    await cleanupAction();
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogError(cleanupEx, "Error executing cleanup action after timeout");
                }
            }
            
            return (false, default, new TimeoutException(
                $"Operation timed out after {timeout.TotalMilliseconds}ms", ex));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Operation failed with exception");
            return (false, default, ex);
        }
    }
}

// Usage with database transaction
public class DatabaseService
{
    private readonly IDbConnection _connection;
    private readonly GracefulTimeoutHandler _timeoutHandler;
    private readonly ILogger<DatabaseService> _logger;
    
    public DatabaseService(
        IDbConnection connection,
        ILogger<DatabaseService> logger)
    {
        _connection = connection;
        _logger = logger;
        _timeoutHandler = new GracefulTimeoutHandler(logger);
    }
    
    public async Task<(bool Success, IEnumerable<Customer> Customers)> GetCustomersWithTimeoutAsync(
        CancellationToken cancellationToken = default)
    {
        IDbTransaction transaction = null;
        
        var result = await _timeoutHandler.ExecuteWithTimeoutAsync(
            async (token) =>
            {
                await _connection.OpenAsync(token);
                transaction = _connection.BeginTransaction();
                
                var customers = await _connection.QueryAsync<Customer>(
                    "SELECT * FROM Customers",
                    transaction: transaction,
                    commandTimeout: 30);
                    
                transaction.Commit();
                return customers.ToList();
            },
            timeout: TimeSpan.FromSeconds(5),
            cleanupAction: async () =>
            {
                // Cleanup: rollback transaction if it exists
                transaction?.Rollback();
                
                if (_connection.State != ConnectionState.Closed)
                {
                    _connection.Close();
                }
            },
            cancellationToken);
            
        if (result.Success)
        {
            return (true, result.Result);
        }
        else
        {
            _logger.LogError(result.Error, "Failed to get customers");
            return (false, Enumerable.Empty<Customer>());
        }
    }
}
```

### 3. Progressive Timeout Strategy

Implement a timeout strategy that adjusts based on system conditions:

```csharp
public class AdaptiveTimeoutStrategy
{
    private readonly ILogger _logger;
    private readonly object _lock = new object();
    
    // Default timeout values
    private readonly TimeSpan _minTimeout;
    private readonly TimeSpan _maxTimeout;
    private readonly TimeSpan _defaultTimeout;
    private readonly double _timeoutIncreaseFactor;
    private readonly double _timeoutDecreaseFactor;
    
    // Current timeout and statistics
    private TimeSpan _currentTimeout;
    private int _successCount;
    private int _timeoutCount;
    private int _adjustmentThreshold;
    
    public AdaptiveTimeoutStrategy(
        TimeSpan? minTimeout = null,
        TimeSpan? maxTimeout = null,
        TimeSpan? defaultTimeout = null,
        double timeoutIncreaseFactor = 1.5,
        double timeoutDecreaseFactor = 0.9,
        int adjustmentThreshold = 10,
        ILogger logger = null)
    {
        _minTimeout = minTimeout ?? TimeSpan.FromSeconds(1);
        _maxTimeout = maxTimeout ?? TimeSpan.FromSeconds(30);
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
        _timeoutIncreaseFactor = timeoutIncreaseFactor;
        _timeoutDecreaseFactor = timeoutDecreaseFactor;
        _adjustmentThreshold = adjustmentThreshold;
        _logger = logger;
        
        _currentTimeout = _defaultTimeout;
    }
    
    public TimeSpan GetCurrentTimeout()
    {
        lock (_lock)
        {
            return _currentTimeout;
        }
    }
    
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _successCount++;
            
            // If we've had several successes, we might decrease the timeout
            if (_successCount >= _adjustmentThreshold)
            {
                DecreaseTimeout();
                _successCount = 0;
            }
        }
    }
    
    public void RecordTimeout()
    {
        lock (_lock)
        {
            _timeoutCount++;
            _successCount = 0; // Reset success count
            
            // Immediately increase timeout after a failure
            IncreaseTimeout();
        }
    }
    
    private void IncreaseTimeout()
    {
        var newTimeout = TimeSpan.FromMilliseconds(
            _currentTimeout.TotalMilliseconds * _timeoutIncreaseFactor);
            
        if (newTimeout > _maxTimeout)
        {
            newTimeout = _maxTimeout;
        }
        
        if (newTimeout != _currentTimeout)
        {
            _logger?.LogInformation(
                "Increasing timeout from {CurrentTimeout}ms to {NewTimeout}ms",
                _currentTimeout.TotalMilliseconds, newTimeout.TotalMilliseconds);
                
            _currentTimeout = newTimeout;
        }
    }
    
    private void DecreaseTimeout()
    {
        var newTimeout = TimeSpan.FromMilliseconds(
            _currentTimeout.TotalMilliseconds * _timeoutDecreaseFactor);
            
        if (newTimeout < _minTimeout)
        {
            newTimeout = _minTimeout;
        }
        
        if (newTimeout != _currentTimeout)
        {
            _logger?.LogInformation(
                "Decreasing timeout from {CurrentTimeout}ms to {NewTimeout}ms",
                _currentTimeout.TotalMilliseconds, newTimeout.TotalMilliseconds);
                
            _currentTimeout = newTimeout;
        }
    }
}

// Usage
public class AdaptiveTimeoutClient
{
    private readonly HttpClient _httpClient;
    private readonly AdaptiveTimeoutStrategy _timeoutStrategy;
    private readonly ILogger<AdaptiveTimeoutClient> _logger;
    
    public AdaptiveTimeoutClient(
        HttpClient httpClient,
        ILogger<AdaptiveTimeoutClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _timeoutStrategy = new AdaptiveTimeoutStrategy(
            minTimeout: TimeSpan.FromSeconds(1),
            maxTimeout: TimeSpan.FromSeconds(20),
            defaultTimeout: TimeSpan.FromSeconds(5),
            logger: logger);
    }
    
    public async Task<string> GetDataAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        var timeout = _timeoutStrategy.GetCurrentTimeout();
        
        try
        {
            var result = await _httpClient.GetStringAsync(endpoint, cancellationToken)
                .WithTimeout(timeout, $"HTTP GET {endpoint}", cancellationToken);
                
            // Record success
            _timeoutStrategy.RecordSuccess();
            
            return result;
        }
        catch (TimeoutException)
        {
            // Record timeout
            _timeoutStrategy.RecordTimeout();
            throw;
        }
    }
}
```

### 4. Timeout with Fallback

Implement a timeout with a fallback mechanism for graceful degradation:

```csharp
public class TimeoutWithFallback
{
    private readonly ILogger _logger;
    
    public TimeoutWithFallback(ILogger logger = null)
    {
        _logger = logger;
    }
    
    public async Task<T> ExecuteWithFallbackAsync<T>(
        Func<CancellationToken, Task<T>> primaryOperation,
        Func<Exception, Task<T>> fallbackOperation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);
        
        try
        {
            // Try primary operation with timeout
            return await primaryOperation(linkedCts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException && timeoutCts.IsCancellationRequested)
        {
            _logger?.LogWarning("Primary operation timed out after {Timeout}ms, using fallback",
                timeout.TotalMilliseconds);
                
            // Use fallback for timeout
            return await fallbackOperation(new TimeoutException(
                $"Operation timed out after {timeout.TotalMilliseconds}ms", ex));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Primary operation failed with exception, using fallback");
            
            // Use fallback for other exceptions
            return await fallbackOperation(ex);
        }
    }
}

// Usage with caching fallback
public class ProductService
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly TimeoutWithFallback _timeoutHandler;
    private readonly ILogger<ProductService> _logger;
    
    public ProductService(
        HttpClient httpClient,
        IDistributedCache cache,
        ILogger<ProductService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _timeoutHandler = new TimeoutWithFallback(logger);
    }
    
    public async Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _timeoutHandler.ExecuteWithFallbackAsync(
            // Primary operation - get from API
            async (token) =>
            {
                var response = await _httpClient.GetAsync("/api/products", token);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var products = JsonConvert.DeserializeObject<List<Product>>(content);
                
                // Update cache with fresh data
                await UpdateCacheAsync(products);
                
                return products;
            },
            // Fallback operation - get from cache
            async (ex) =>
            {
                _logger.LogWarning(ex, "Using cached products due to API failure");
                
                var cachedData = await _cache.GetStringAsync("products", cancellationToken);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    return JsonConvert.DeserializeObject<List<Product>>(cachedData);
                }
                
                // If cache is empty, return empty list
                return new List<Product>();
            },
            timeout: TimeSpan.FromSeconds(3),
            cancellationToken);
    }
    
    private async Task UpdateCacheAsync(List<Product> products)
    {
        var serializedData = JsonConvert.SerializeObject(products);
        await _cache.SetStringAsync(
            "products",
            serializedData,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });
    }
}
```

## Conclusion

Effective error handling is a critical aspect of building robust, resilient event-sourced systems. By implementing the patterns described in this document, you can create applications that gracefully handle failures, provide meaningful error information, and maintain system integrity even under adverse conditions.

### Key Takeaways

1. **Defense in Depth**: Implement multiple layers of error handling, from input validation to global exception handling, to create a comprehensive error management strategy.

2. **Fail Fast, Fail Safely**: Detect errors as early as possible in the processing pipeline, but ensure that when failures occur, they don't compromise system integrity or data consistency.

3. **Resilience Patterns**: Use patterns like Circuit Breaker, Retry, Bulkhead, and Timeout to build systems that can withstand transient failures and gracefully degrade when necessary.

4. **Domain-Specific Error Handling**: Create domain-specific exceptions and error responses that provide meaningful context about what went wrong from a business perspective.

5. **Observability**: Ensure errors are properly logged, monitored, and tracked to facilitate troubleshooting and system improvement.

### Best Practices

- **Be Specific**: Use specific exception types rather than generic ones to provide clear information about what went wrong.

- **Don't Swallow Exceptions**: Always handle exceptions appropriately; never catch exceptions without proper handling or logging.

- **Centralize Error Handling Logic**: Use middleware, decorators, or aspects to centralize error handling logic and ensure consistent error responses.

- **Provide Context**: Include relevant context information in error messages and logs to aid in troubleshooting.

- **Design for Recovery**: Implement mechanisms for automatic recovery from transient failures and graceful degradation during more severe issues.

- **Test Error Scenarios**: Explicitly test error scenarios, including edge cases and failure modes, to ensure your error handling works as expected.

By applying these patterns and practices, you can build event-sourced systems that are not only functionally correct but also resilient in the face of the inevitable errors and failures that occur in distributed systems.
