# Complete Sample Applications

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example provides an overview of complete sample applications built with Reactive Domain, demonstrating how all the components work together in real-world scenarios.

## Banking Sample Application

The Banking Sample Application demonstrates a simple banking system with accounts, transactions, and reporting. It showcases the core concepts of Reactive Domain, including event sourcing, CQRS, and domain-driven design.

### Repository Structure

```
BankingSample/
├── src/
│   ├── BankingSample.Domain/           # Domain model and business logic
│   │   ├── Aggregates/                 # Aggregate roots
│   │   ├── Commands/                   # Command definitions
│   │   ├── Events/                     # Event definitions
│   │   └── Handlers/                   # Command handlers
│   ├── BankingSample.ReadModels/       # Read models and projections
│   │   ├── Accounts/                   # Account read models
│   │   ├── Reporting/                  # Reporting read models
│   │   └── Projections/                # Event projections
│   ├── BankingSample.Infrastructure/   # Infrastructure components
│   │   ├── EventStore/                 # Event store configuration
│   │   ├── Repositories/               # Repository implementations
│   │   └── Serialization/              # Event serialization
│   ├── BankingSample.Api/              # Web API
│   │   ├── Controllers/                # API controllers
│   │   ├── Models/                     # Request/response models
│   │   └── Startup.cs                  # Application configuration
│   └── BankingSample.Console/          # Console application
│       └── Program.cs                  # Entry point
└── test/
    ├── BankingSample.Domain.Tests/     # Domain tests
    ├── BankingSample.ReadModels.Tests/ # Read model tests
    └── BankingSample.Api.Tests/        # API tests
```

### Key Features

- Account creation, deposits, withdrawals, and transfers
- Account statements and transaction history
- Reporting and analytics
- Command validation and error handling
- Event sourcing with EventStoreDB
- CQRS with separate read and write models
- Correlation and causation tracking
- Snapshotting for performance optimization
- Integration with ASP.NET Core Web API

### Running the Sample

1. Clone the repository: `git clone https://github.com/reactive-domain/banking-sample.git`
2. Start EventStoreDB: `docker-compose up -d`
3. Build the solution: `dotnet build`
4. Run the API: `dotnet run --project src/BankingSample.Api/BankingSample.Api.csproj`
5. Open your browser to `https://localhost:5001/swagger` to explore the API

## E-Commerce Sample Application

The E-Commerce Sample Application demonstrates a more complex domain with products, orders, customers, and inventory management. It showcases advanced concepts like sagas, process managers, and integration with external systems.

### Repository Structure

```
ECommerceSample/
├── src/
│   ├── ECommerceSample.Domain/         # Domain model and business logic
│   │   ├── Aggregates/                 # Aggregate roots
│   │   ├── Commands/                   # Command definitions
│   │   ├── Events/                     # Event definitions
│   │   └── Handlers/                   # Command handlers
│   ├── ECommerceSample.ReadModels/     # Read models and projections
│   │   ├── Catalog/                    # Product catalog read models
│   │   ├── Orders/                     # Order read models
│   │   ├── Customers/                  # Customer read models
│   │   └── Projections/                # Event projections
│   ├── ECommerceSample.ProcessManagers/ # Process managers and sagas
│   │   ├── OrderProcessing/            # Order processing workflow
│   │   ├── Shipping/                   # Shipping workflow
│   │   └── Payment/                    # Payment processing workflow
│   ├── ECommerceSample.Infrastructure/ # Infrastructure components
│   │   ├── EventStore/                 # Event store configuration
│   │   ├── Repositories/               # Repository implementations
│   │   ├── Serialization/              # Event serialization
│   │   └── ExternalServices/           # External service integrations
│   ├── ECommerceSample.Api/            # Web API
│   │   ├── Controllers/                # API controllers
│   │   ├── Models/                     # Request/response models
│   │   └── Startup.cs                  # Application configuration
│   └── ECommerceSample.Web/            # Web frontend
│       ├── Pages/                      # Razor pages
│       ├── Components/                 # Blazor components
│       └── Program.cs                  # Entry point
└── test/
    ├── ECommerceSample.Domain.Tests/   # Domain tests
    ├── ECommerceSample.ReadModels.Tests/ # Read model tests
    ├── ECommerceSample.ProcessManagers.Tests/ # Process manager tests
    └── ECommerceSample.Api.Tests/      # API tests
```

### Key Features

- Product catalog management
- Shopping cart functionality
- Order processing with multi-step workflow
- Customer management and authentication
- Inventory tracking and management
- Payment processing integration
- Shipping and fulfillment
- Reporting and analytics
- Event sourcing with EventStoreDB
- CQRS with separate read and write models
- Process managers for coordinating workflows
- Integration events for external systems
- Snapshotting for performance optimization
- Integration with ASP.NET Core and Blazor

### Running the Sample

1. Clone the repository: `git clone https://github.com/reactive-domain/ecommerce-sample.git`
2. Start the infrastructure: `docker-compose up -d`
3. Build the solution: `dotnet build`
4. Run the API: `dotnet run --project src/ECommerceSample.Api/ECommerceSample.Api.csproj`
5. Run the Web frontend: `dotnet run --project src/ECommerceSample.Web/ECommerceSample.Web.csproj`
6. Open your browser to `https://localhost:5001` to explore the application

## Task Management Sample Application

The Task Management Sample Application demonstrates a simple task tracking system with projects, tasks, and users. It showcases the basics of Reactive Domain in a straightforward domain.

### Repository Structure

```
TaskManagementSample/
├── src/
│   ├── TaskManagementSample.Domain/    # Domain model and business logic
│   │   ├── Aggregates/                 # Aggregate roots
│   │   ├── Commands/                   # Command definitions
│   │   ├── Events/                     # Event definitions
│   │   └── Handlers/                   # Command handlers
│   ├── TaskManagementSample.ReadModels/ # Read models and projections
│   │   ├── Projects/                   # Project read models
│   │   ├── Tasks/                      # Task read models
│   │   ├── Users/                      # User read models
│   │   └── Projections/                # Event projections
│   ├── TaskManagementSample.Infrastructure/ # Infrastructure components
│   │   ├── EventStore/                 # Event store configuration
│   │   ├── Repositories/               # Repository implementations
│   │   └── Serialization/              # Event serialization
│   └── TaskManagementSample.Api/       # Web API
│       ├── Controllers/                # API controllers
│       ├── Models/                     # Request/response models
│       └── Startup.cs                  # Application configuration
└── test/
    ├── TaskManagementSample.Domain.Tests/ # Domain tests
    ├── TaskManagementSample.ReadModels.Tests/ # Read model tests
    └── TaskManagementSample.Api.Tests/ # API tests
```

### Key Features

- Project creation and management
- Task creation, assignment, and status tracking
- User management
- Event sourcing with EventStoreDB
- CQRS with separate read and write models
- Integration with ASP.NET Core Web API

### Running the Sample

1. Clone the repository: `git clone https://github.com/reactive-domain/task-management-sample.git`
2. Start EventStoreDB: `docker-compose up -d`
3. Build the solution: `dotnet build`
4. Run the API: `dotnet run --project src/TaskManagementSample.Api/TaskManagementSample.Api.csproj`
5. Open your browser to `https://localhost:5001/swagger` to explore the API

## Sample Code Snippets

### Domain Model

```csharp
// BankingSample.Domain/Aggregates/Account.cs
using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using BankingSample.Domain.Events;

namespace BankingSample.Domain.Aggregates
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
        
        public Account(Guid id, ICorrelatedMessage source) : base(id, source)
        {
        }
        
        public void Create(string accountNumber, string customerName)
        {
            if (string.IsNullOrEmpty(accountNumber))
                throw new ArgumentException("Account number cannot be empty", nameof(accountNumber));
                
            if (string.IsNullOrEmpty(customerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(customerName));
                
            ApplyChange(new AccountCreated(Id, accountNumber, customerName, CorrelationId, CausationId));
        }
        
        public void Deposit(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            if (_isClosed)
                throw new InvalidOperationException("Cannot deposit to a closed account");
                
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
                
            ApplyChange(new FundsWithdrawn(Id, amount, CorrelationId, CausationId));
        }
        
        public void Transfer(Guid targetAccountId, decimal amount)
        {
            if (targetAccountId == Id)
                throw new ArgumentException("Cannot transfer to the same account", nameof(targetAccountId));
                
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
                
            if (_isClosed)
                throw new InvalidOperationException("Cannot transfer from a closed account");
                
            if (_balance < amount)
                throw new InvalidOperationException("Insufficient funds");
                
            ApplyChange(new FundsTransferred(Id, targetAccountId, amount, CorrelationId, CausationId));
        }
        
        public void Close()
        {
            if (_isClosed)
                throw new InvalidOperationException("Account is already closed");
                
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
        
        private void Apply(FundsTransferred @event)
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

### Process Manager

```csharp
// ECommerceSample.ProcessManagers/OrderProcessing/OrderProcessManager.cs
using System;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ECommerceSample.Domain.Commands;
using ECommerceSample.Domain.Events;

namespace ECommerceSample.ProcessManagers.OrderProcessing
{
    public class OrderProcessManager : 
        IHandleEvent<OrderCreated>,
        IHandleEvent<PaymentReceived>,
        IHandleEvent<OrderShipped>,
        IHandleEvent<OrderDelivered>
    {
        private readonly ICommandBus _commandBus;
        private readonly IOrderProcessStateRepository _stateRepository;
        
        public OrderProcessManager(
            ICommandBus commandBus,
            IOrderProcessStateRepository stateRepository)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        }
        
        public async Task Handle(OrderCreated @event)
        {
            // Create a new process state
            var state = new OrderProcessState(@event.OrderId);
            state.OrderCreated(@event.CustomerId, @event.OrderItems, @event.TotalAmount);
            
            // Save the state
            await _stateRepository.SaveAsync(state);
            
            // Request payment
            var requestPaymentCommand = MessageBuilder.From(@event, () => 
                new RequestPayment(@event.OrderId, @event.CustomerId, @event.TotalAmount));
                
            await _commandBus.SendAsync(requestPaymentCommand);
        }
        
        public async Task Handle(PaymentReceived @event)
        {
            // Get the process state
            var state = await _stateRepository.GetByOrderIdAsync(@event.OrderId);
            
            if (state == null)
            {
                // Handle missing state
                return;
            }
            
            // Update the state
            state.PaymentReceived(@event.Amount, @event.PaymentId);
            
            // Save the state
            await _stateRepository.SaveAsync(state);
            
            // Prepare order for shipping
            var prepareShippingCommand = MessageBuilder.From(@event, () => 
                new PrepareOrderForShipping(@event.OrderId, state.OrderItems));
                
            await _commandBus.SendAsync(prepareShippingCommand);
        }
        
        public async Task Handle(OrderShipped @event)
        {
            // Get the process state
            var state = await _stateRepository.GetByOrderIdAsync(@event.OrderId);
            
            if (state == null)
            {
                // Handle missing state
                return;
            }
            
            // Update the state
            state.OrderShipped(@event.TrackingNumber, @event.ShippingProvider);
            
            // Save the state
            await _stateRepository.SaveAsync(state);
            
            // Notify customer
            var notifyCustomerCommand = MessageBuilder.From(@event, () => 
                new NotifyCustomer(
                    state.CustomerId,
                    "Your order has been shipped",
                    $"Your order {state.OrderId} has been shipped via {state.ShippingProvider}. " +
                    $"Tracking number: {state.TrackingNumber}"));
                    
            await _commandBus.SendAsync(notifyCustomerCommand);
        }
        
        public async Task Handle(OrderDelivered @event)
        {
            // Get the process state
            var state = await _stateRepository.GetByOrderIdAsync(@event.OrderId);
            
            if (state == null)
            {
                // Handle missing state
                return;
            }
            
            // Update the state
            state.OrderDelivered(@event.DeliveryDate);
            
            // Save the state
            await _stateRepository.SaveAsync(state);
            
            // Complete the order
            var completeOrderCommand = MessageBuilder.From(@event, () => 
                new CompleteOrder(@event.OrderId));
                
            await _commandBus.SendAsync(completeOrderCommand);
            
            // Request customer feedback
            var requestFeedbackCommand = MessageBuilder.From(@event, () => 
                new RequestCustomerFeedback(state.CustomerId, state.OrderId));
                
            await _commandBus.SendAsync(requestFeedbackCommand);
        }
    }
}
```

### API Controller

```csharp
// BankingSample.Api/Controllers/AccountsController.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BankingSample.Api.Models;
using BankingSample.Domain.Commands;
using ReactiveDomain.Messaging;

namespace BankingSample.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly ICommandBus _commandBus;
        private readonly IAccountQueries _accountQueries;
        
        public AccountsController(
            ICommandBus commandBus,
            IAccountQueries accountQueries)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _accountQueries = accountQueries ?? throw new ArgumentNullException(nameof(accountQueries));
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            var accountId = Guid.NewGuid();
            
            var command = new CreateAccount(
                accountId,
                request.AccountNumber,
                request.CustomerName);
                
            await _commandBus.SendAsync(command);
            
            return CreatedAtAction(
                nameof(GetAccount),
                new { id = accountId },
                new { Id = accountId });
        }
        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAccount(Guid id)
        {
            var account = await _accountQueries.GetAccountByIdAsync(id);
            
            if (account == null)
            {
                return NotFound();
            }
            
            return Ok(account);
        }
        
        [HttpPost("{id}/deposit")]
        public async Task<IActionResult> Deposit(Guid id, [FromBody] DepositRequest request)
        {
            var command = new DepositFunds(id, request.Amount);
            await _commandBus.SendAsync(command);
            
            return Ok();
        }
        
        [HttpPost("{id}/withdraw")]
        public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawRequest request)
        {
            var command = new WithdrawFunds(id, request.Amount);
            
            try
            {
                await _commandBus.SendAsync(command);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
        
        [HttpPost("{id}/transfer")]
        public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferRequest request)
        {
            var command = new TransferFunds(
                id,
                request.TargetAccountId,
                request.Amount);
                
            try
            {
                await _commandBus.SendAsync(command);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
        
        [HttpPost("{id}/close")]
        public async Task<IActionResult> Close(Guid id)
        {
            var command = new CloseAccount(id);
            
            try
            {
                await _commandBus.SendAsync(command);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
        
        [HttpGet("{id}/transactions")]
        public async Task<IActionResult> GetTransactions(Guid id)
        {
            var transactions = await _accountQueries.GetTransactionsByAccountIdAsync(id);
            return Ok(transactions);
        }
    }
}
```

## Key Concepts

### Complete Application Architecture

- **Domain Layer**: Contains the domain model, aggregates, commands, and events
- **Read Model Layer**: Contains read models and projections
- **Infrastructure Layer**: Contains repositories, event store configuration, and external service integrations
- **API Layer**: Contains controllers, request/response models, and application configuration
- **Process Manager Layer**: Contains process managers and sagas for coordinating workflows
- **Web Layer**: Contains the user interface components

### Sample Applications

- **Banking Sample**: Demonstrates basic event sourcing and CQRS concepts
- **E-Commerce Sample**: Demonstrates advanced concepts like process managers and integration events
- **Task Management Sample**: Demonstrates a simple domain with basic event sourcing

### Running the Samples

- All samples include Docker Compose files for setting up the required infrastructure
- Samples can be run locally for development and testing
- Samples include comprehensive test suites for all layers

## Best Practices

1. **Clean Architecture**: Organize code into distinct layers with clear responsibilities
2. **Domain-Driven Design**: Focus on the domain model and business rules
3. **CQRS**: Separate command and query responsibilities
4. **Event Sourcing**: Store state as a sequence of events
5. **Process Managers**: Use process managers to coordinate complex workflows
6. **Testing**: Write comprehensive tests for all layers
7. **Documentation**: Document the architecture, domain model, and API

## Common Pitfalls

1. **Overcomplicating**: Start simple and add complexity as needed
2. **Tight Coupling**: Keep layers loosely coupled and use interfaces
3. **Missing Tests**: Ensure all business rules and edge cases are tested
4. **Performance Issues**: Monitor and optimize performance as needed
5. **Lack of Documentation**: Document the architecture and domain model

---

**Navigation**:
- [← Previous: Integration with ASP.NET Core](aspnet-integration.md)
- [↑ Back to Top](#complete-sample-applications)
- [← Back to Code Examples](README.md)
