# Integration with ASP.NET Core

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to integrate Reactive Domain with ASP.NET Core to build event-sourced web applications.

## Project Setup

```csharp
// MyApp.Web.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp\MyApp.csproj" />
  </ItemGroup>

</Project>
```

## Dependency Injection Configuration

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.Domain.Commands;
using MyApp.Domain.Handlers;
using MyApp.EventHandlers;
using MyApp.Infrastructure;
using MyApp.ReadModels;

namespace MyApp.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add controllers and API explorer for Swagger
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            
            // Configure EventStore connection
            var eventStoreConnectionString = Configuration.GetConnectionString("EventStore");
            
            // Register repository
            var repositoryConfig = new RepositoryConfiguration();
            var repository = repositoryConfig.ConfigureRepository(eventStoreConnectionString);
            services.AddSingleton<IRepository>(repository);
            
            // Register correlated repository
            var correlatedRepository = repositoryConfig.ConfigureCorrelatedRepository(repository);
            services.AddSingleton<ICorrelatedRepository>(correlatedRepository);
            
            // Register command bus and handlers
            var commandBus = new CommandBus();
            var accountCommandHandler = new AccountCommandHandler(repository);
            
            commandBus.Subscribe<CreateAccount>(accountCommandHandler);
            commandBus.Subscribe<DepositFunds>(accountCommandHandler);
            commandBus.Subscribe<WithdrawFunds>(accountCommandHandler);
            commandBus.Subscribe<CloseAccount>(accountCommandHandler);
            
            services.AddSingleton<ICommandBus>(commandBus);
            
            // Register event bus
            var eventBus = new EventBus();
            services.AddSingleton<IEventBus>(eventBus);
            
            // Register read model repositories
            services.AddSingleton<IReadModelRepository<AccountSummary>, InMemoryReadModelRepository<AccountSummary>>();
            services.AddSingleton<IReadModelRepository<TransactionHistory>, InMemoryReadModelRepository<TransactionHistory>>();
            
            // Register event handlers
            services.AddSingleton<AccountReadModelUpdater>();
            services.AddSingleton<TransactionHistoryUpdater>();
            
            // Register application services
            services.AddSingleton<AccountService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            
            // Set up event handlers
            ConfigureEventHandlers(serviceProvider);
        }
        
        private void ConfigureEventHandlers(IServiceProvider serviceProvider)
        {
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            var accountReadModelUpdater = serviceProvider.GetRequiredService<AccountReadModelUpdater>();
            var transactionHistoryUpdater = serviceProvider.GetRequiredService<TransactionHistoryUpdater>();
            
            // Subscribe event handlers to the event bus
            eventBus.Subscribe<AccountCreated>(accountReadModelUpdater);
            eventBus.Subscribe<FundsDeposited>(accountReadModelUpdater);
            eventBus.Subscribe<FundsWithdrawn>(accountReadModelUpdater);
            eventBus.Subscribe<AccountClosed>(accountReadModelUpdater);
            
            eventBus.Subscribe<AccountCreated>(transactionHistoryUpdater);
            eventBus.Subscribe<FundsDeposited>(transactionHistoryUpdater);
            eventBus.Subscribe<FundsWithdrawn>(transactionHistoryUpdater);
            eventBus.Subscribe<AccountClosed>(transactionHistoryUpdater);
            
            // Set up event store subscription
            var repository = serviceProvider.GetRequiredService<IRepository>();
            var eventStoreSubscription = new EventStoreSubscription(
                GetStreamStoreConnection(repository),
                eventBus,
                GetEventSerializer(repository));
                
            // Subscribe to all events
            eventStoreSubscription.SubscribeToAll();
        }
        
        private IStreamStoreConnection GetStreamStoreConnection(IRepository repository)
        {
            // This is a simplified version just for the example
            // In a real application, you would use a more robust approach
            return repository.GetType()
                .GetProperty("Connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(repository) as IStreamStoreConnection;
        }
        
        private IEventSerializer GetEventSerializer(IRepository repository)
        {
            // This is a simplified version just for the example
            // In a real application, you would use a more robust approach
            return repository.GetType()
                .GetProperty("Serializer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(repository) as IEventSerializer;
        }
    }
}
```

## Application Service

```csharp
using System;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using MyApp.Domain.Commands;
using MyApp.ReadModels;

namespace MyApp
{
    public class AccountService
    {
        private readonly ICommandBus _commandBus;
        private readonly IReadModelRepository<AccountSummary> _accountRepository;
        private readonly IReadModelRepository<TransactionHistory> _transactionRepository;
        
        public AccountService(
            ICommandBus commandBus,
            IReadModelRepository<AccountSummary> accountRepository,
            IReadModelRepository<TransactionHistory> transactionRepository)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        }
        
        public async Task<Guid> CreateAccountAsync(string accountNumber, string customerName)
        {
            var accountId = Guid.NewGuid();
            
            var command = new CreateAccount(accountId, accountNumber, customerName);
            await _commandBus.SendAsync(command);
            
            return accountId;
        }
        
        public async Task DepositFundsAsync(Guid accountId, decimal amount)
        {
            var command = new DepositFunds(accountId, amount);
            await _commandBus.SendAsync(command);
        }
        
        public async Task WithdrawFundsAsync(Guid accountId, decimal amount)
        {
            var command = new WithdrawFunds(accountId, amount);
            await _commandBus.SendAsync(command);
        }
        
        public async Task CloseAccountAsync(Guid accountId)
        {
            var command = new CloseAccount(accountId);
            await _commandBus.SendAsync(command);
        }
        
        public AccountSummary GetAccountSummary(Guid accountId)
        {
            return _accountRepository.GetById(accountId);
        }
        
        public TransactionHistory GetTransactionHistory(Guid accountId)
        {
            return _transactionRepository.GetById(accountId);
        }
    }
}
```

## API Controllers

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyApp.ReadModels;
using MyApp.Web.Models;

namespace MyApp.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly AccountService _accountService;
        
        public AccountsController(AccountService accountService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            try
            {
                var accountId = await _accountService.CreateAccountAsync(
                    request.AccountNumber,
                    request.CustomerName);
                    
                return CreatedAtAction(nameof(GetAccount), new { id = accountId }, new { AccountId = accountId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
        
        [HttpGet("{id}")]
        public IActionResult GetAccount(Guid id)
        {
            var account = _accountService.GetAccountSummary(id);
            
            if (account == null)
            {
                return NotFound();
            }
            
            return Ok(new AccountResponse
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                CustomerName = account.CustomerName,
                Balance = account.Balance,
                IsClosed = account.IsClosed,
                CreatedAt = account.CreatedAt,
                LastUpdatedAt = account.LastUpdatedAt
            });
        }
        
        [HttpPost("{id}/deposit")]
        public async Task<IActionResult> Deposit(Guid id, [FromBody] DepositRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var account = _accountService.GetAccountSummary(id);
            
            if (account == null)
            {
                return NotFound();
            }
            
            if (account.IsClosed)
            {
                return BadRequest(new { Error = "Cannot deposit to a closed account" });
            }
            
            try
            {
                await _accountService.DepositFundsAsync(id, request.Amount);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
        
        [HttpPost("{id}/withdraw")]
        public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var account = _accountService.GetAccountSummary(id);
            
            if (account == null)
            {
                return NotFound();
            }
            
            if (account.IsClosed)
            {
                return BadRequest(new { Error = "Cannot withdraw from a closed account" });
            }
            
            if (account.Balance < request.Amount)
            {
                return BadRequest(new { Error = "Insufficient funds" });
            }
            
            try
            {
                await _accountService.WithdrawFundsAsync(id, request.Amount);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
        
        [HttpPost("{id}/close")]
        public async Task<IActionResult> Close(Guid id)
        {
            var account = _accountService.GetAccountSummary(id);
            
            if (account == null)
            {
                return NotFound();
            }
            
            if (account.IsClosed)
            {
                return BadRequest(new { Error = "Account is already closed" });
            }
            
            try
            {
                await _accountService.CloseAccountAsync(id);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
        
        [HttpGet("{id}/transactions")]
        public IActionResult GetTransactions(Guid id)
        {
            var account = _accountService.GetAccountSummary(id);
            
            if (account == null)
            {
                return NotFound();
            }
            
            var history = _accountService.GetTransactionHistory(id);
            
            if (history == null)
            {
                return Ok(Array.Empty<TransactionResponse>());
            }
            
            var transactions = history.Transactions.Select(t => new TransactionResponse
            {
                Id = t.Id,
                Type = t.Type,
                Amount = t.Amount,
                Description = t.Description,
                Timestamp = t.Timestamp
            }).ToArray();
            
            return Ok(transactions);
        }
    }
}
```

## Request and Response Models

```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace MyApp.Web.Models
{
    public class CreateAccountRequest
    {
        [Required]
        [StringLength(50)]
        public string AccountNumber { get; set; }
        
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; }
    }
    
    public class DepositRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }
    }
    
    public class WithdrawRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }
    }
    
    public class AccountResponse
    {
        public Guid Id { get; set; }
        public string AccountNumber { get; set; }
        public string CustomerName { get; set; }
        public decimal Balance { get; set; }
        public bool IsClosed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }
    
    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
```

## Program.cs

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace MyApp.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
```

## appsettings.json

```json
{
  "ConnectionStrings": {
    "EventStore": "tcp://admin:changeit@localhost:1113"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

## Key Concepts

### Dependency Injection

- Register Reactive Domain components in the ASP.NET Core DI container
- Configure repositories, command bus, and event bus as singletons
- Register command handlers and event handlers
- Set up event store subscription during application startup

### Application Services

- Create application services that encapsulate domain operations
- Use command bus to send commands to the domain
- Use read model repositories to query data
- Provide a clean API for controllers

### API Controllers

- Create RESTful API endpoints for domain operations
- Use application services to handle domain logic
- Return appropriate HTTP status codes and responses
- Validate input using model validation

### Request/Response Models

- Define clear request and response models for API endpoints
- Use data annotations for validation
- Map domain entities to response models
- Keep domain models separate from API models

## Best Practices

1. **Separation of Concerns**: Keep domain logic separate from API controllers
2. **Dependency Injection**: Use the ASP.NET Core DI container to manage dependencies
3. **Validation**: Validate input at the API boundary
4. **Error Handling**: Implement proper error handling and return appropriate status codes
5. **Asynchronous Operations**: Use async/await for command operations
6. **CQRS**: Separate command and query responsibilities
7. **API Design**: Follow RESTful API design principles

## Common Pitfalls

1. **Mixing Domain and API Concerns**: Keep domain logic out of controllers
2. **Synchronous Command Handling**: Use async/await for command operations
3. **Missing Validation**: Always validate input at the API boundary
4. **Exposing Domain Models**: Use dedicated response models for API responses
5. **Ignoring Error Handling**: Implement proper error handling for domain operations

---

**Navigation**:
- [← Previous: Testing Aggregates and Event Handlers](testing.md)
- [↑ Back to Top](#integration-with-aspnet-core)
- [→ Next: Complete Sample Applications](sample-applications.md)
