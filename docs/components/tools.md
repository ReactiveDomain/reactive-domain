# ReactiveDomain.Tools Component

[← Back to Components](README.md)

The ReactiveDomain.Tools component provides utilities and tools for developing, testing, and maintaining Reactive Domain applications. These tools help with common tasks such as event store management, diagnostics, code generation, and performance analysis.

## Key Features

- Event store management tools
- Diagnostics and monitoring utilities
- Code generation for domain models
- Performance analysis tools
- Migration utilities
- Command-line interface
- Development-time helpers

## Core Tools

### Event Store Management

- **EventStoreManager**: Manages event store instances
- **StreamManager**: Manages event streams
- **EventBrowser**: Browses events in the event store
- **EventExporter**: Exports events from the event store
- **EventImporter**: Imports events into the event store

### Diagnostics and Monitoring

- **EventMonitor**: Monitors event flow in real-time
- **CommandMonitor**: Monitors command execution
- **PerformanceMonitor**: Monitors system performance
- **HealthChecker**: Checks system health
- **DiagnosticsCollector**: Collects diagnostic information

### Code Generation

- **AggregateGenerator**: Generates aggregate code
- **CommandGenerator**: Generates command code
- **EventGenerator**: Generates event code
- **ReadModelGenerator**: Generates read model code
- **ProjectionGenerator**: Generates projection code

### Performance Analysis

- **PerformanceAnalyzer**: Analyzes system performance
- **Benchmarker**: Benchmarks system components
- **LoadGenerator**: Generates load for testing
- **BottleneckDetector**: Detects performance bottlenecks
- **ResourceMonitor**: Monitors resource usage

## Usage Examples

### Managing Event Streams

```csharp
// Create a stream manager
var connectionSettings = ConnectionSettings.Create()
    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
    .Build();
    
var connection = EventStoreConnection.Create(
    connectionSettings, 
    new Uri("tcp://localhost:1113"), 
    "StreamManager");
    
await connection.ConnectAsync();

var streamManager = new StreamManager(connection);

// List streams
var streams = await streamManager.ListStreamsAsync();
foreach (var stream in streams)
{
    Console.WriteLine($"Stream: {stream.StreamId}, Events: {stream.EventCount}");
}

// Delete a stream
await streamManager.DeleteStreamAsync("account-123", true);

// Create a stream
await streamManager.CreateStreamAsync("new-account-456");

// Copy a stream
await streamManager.CopyStreamAsync("account-789", "account-789-backup");
```

### Browsing Events

```csharp
// Create an event browser
var browser = new EventBrowser(connection);

// Browse events in a stream
var events = await browser.BrowseEventsAsync("account-123");
foreach (var evt in events)
{
    Console.WriteLine($"Event: {evt.EventType}, Version: {evt.EventNumber}");
    Console.WriteLine($"Data: {Encoding.UTF8.GetString(evt.Data)}");
    Console.WriteLine($"Metadata: {Encoding.UTF8.GetString(evt.Metadata)}");
    Console.WriteLine();
}

// Search for events
var searchResults = await browser.SearchEventsAsync("AccountCreated");
foreach (var result in searchResults)
{
    Console.WriteLine($"Stream: {result.StreamId}, Event: {result.EventType}, Version: {result.EventNumber}");
}

// Get event details
var eventDetails = await browser.GetEventDetailsAsync("account-123", 0);
Console.WriteLine($"Event: {eventDetails.EventType}, Version: {eventDetails.EventNumber}");
Console.WriteLine($"Data: {Encoding.UTF8.GetString(eventDetails.Data)}");
Console.WriteLine($"Metadata: {Encoding.UTF8.GetString(eventDetails.Metadata)}");
```

### Generating Code

```csharp
// Create an aggregate generator
var generator = new AggregateGenerator();

// Define the aggregate
var aggregateDefinition = new AggregateDefinition
{
    Name = "Account",
    Namespace = "BankingDomain",
    Properties = new List<PropertyDefinition>
    {
        new PropertyDefinition { Name = "Balance", Type = "decimal" },
        new PropertyDefinition { Name = "AccountNumber", Type = "string" },
        new PropertyDefinition { Name = "IsActive", Type = "bool" }
    },
    Commands = new List<CommandDefinition>
    {
        new CommandDefinition
        {
            Name = "CreateAccount",
            Parameters = new List<ParameterDefinition>
            {
                new ParameterDefinition { Name = "accountNumber", Type = "string" },
                new ParameterDefinition { Name = "initialDeposit", Type = "decimal" }
            },
            Events = new List<string> { "AccountCreated" }
        },
        new CommandDefinition
        {
            Name = "Deposit",
            Parameters = new List<ParameterDefinition>
            {
                new ParameterDefinition { Name = "amount", Type = "decimal" }
            },
            Events = new List<string> { "FundsDeposited" }
        },
        new CommandDefinition
        {
            Name = "Withdraw",
            Parameters = new List<ParameterDefinition>
            {
                new ParameterDefinition { Name = "amount", Type = "decimal" }
            },
            Events = new List<string> { "FundsWithdrawn" }
        }
    },
    Events = new List<EventDefinition>
    {
        new EventDefinition
        {
            Name = "AccountCreated",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "AccountId", Type = "Guid" },
                new PropertyDefinition { Name = "AccountNumber", Type = "string" },
                new PropertyDefinition { Name = "InitialDeposit", Type = "decimal" }
            }
        },
        new EventDefinition
        {
            Name = "FundsDeposited",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "AccountId", Type = "Guid" },
                new PropertyDefinition { Name = "Amount", Type = "decimal" }
            }
        },
        new EventDefinition
        {
            Name = "FundsWithdrawn",
            Properties = new List<PropertyDefinition>
            {
                new PropertyDefinition { Name = "AccountId", Type = "Guid" },
                new PropertyDefinition { Name = "Amount", Type = "decimal" }
            }
        }
    }
};

// Generate the code
var code = generator.GenerateCode(aggregateDefinition);
File.WriteAllText("Account.cs", code);
```

### Performance Analysis

```csharp
// Create a performance analyzer
var analyzer = new PerformanceAnalyzer();

// Start monitoring
analyzer.Start();

// Run your code
RunYourCode();

// Stop monitoring
var results = analyzer.Stop();

// Print results
Console.WriteLine($"Total Execution Time: {results.TotalExecutionTime}ms");
Console.WriteLine($"Command Execution Time: {results.CommandExecutionTime}ms");
Console.WriteLine($"Event Processing Time: {results.EventProcessingTime}ms");
Console.WriteLine($"Repository Access Time: {results.RepositoryAccessTime}ms");
Console.WriteLine($"Read Model Update Time: {results.ReadModelUpdateTime}ms");
Console.WriteLine($"Commands Processed: {results.CommandsProcessed}");
Console.WriteLine($"Events Processed: {results.EventsProcessed}");
Console.WriteLine($"Commands Per Second: {results.CommandsPerSecond}");
Console.WriteLine($"Events Per Second: {results.EventsPerSecond}");
```

## Command-Line Interface

The ReactiveDomain.Tools component includes a command-line interface (CLI) for common tasks:

```bash
# List streams
rd-tools streams list --connection "tcp://localhost:1113" --credentials "admin:changeit"

# Export events
rd-tools events export --stream "account-123" --output "account-123-events.json" --connection "tcp://localhost:1113" --credentials "admin:changeit"

# Import events
rd-tools events import --input "account-123-events.json" --stream "account-123-restored" --connection "tcp://localhost:1113" --credentials "admin:changeit"

# Generate code
rd-tools generate aggregate --definition "aggregate-definition.json" --output "Account.cs"

# Run performance analysis
rd-tools analyze performance --connection "tcp://localhost:1113" --credentials "admin:changeit" --duration 60
```

## Integration with Other Components

The Tools component integrates with:

- **ReactiveDomain.Core**: Uses core interfaces and types
- **ReactiveDomain.Foundation**: Works with domain components
- **ReactiveDomain.Messaging**: Analyzes message flow
- **ReactiveDomain.Persistence**: Manages event store

## Best Practices

1. **Use Tools in Development**: Leverage these tools during development to improve productivity
2. **Automate Code Generation**: Automate code generation for repetitive tasks
3. **Monitor Performance**: Regularly monitor performance to identify issues early
4. **Backup Event Store**: Regularly backup your event store using the export/import tools
5. **Version Control Generated Code**: Keep generated code under version control
6. **Validate Generated Code**: Always validate generated code before using it in production
7. **Use CLI in Scripts**: Incorporate CLI tools in automation scripts

## Common Use Cases

### Event Store Maintenance

```csharp
// Compact the event store
await streamManager.CompactStreamAsync("account-123");

// Rebuild read models
await readModelManager.RebuildReadModelsAsync();

// Verify event store integrity
var integrityReport = await streamManager.VerifyIntegrityAsync();
if (!integrityReport.IsValid)
{
    Console.WriteLine("Event store integrity check failed:");
    foreach (var issue in integrityReport.Issues)
    {
        Console.WriteLine($"- {issue}");
    }
}
```

### Development Workflows

```csharp
// Generate a complete domain model
var domainGenerator = new DomainGenerator();
await domainGenerator.GenerateDomainAsync("domain-definition.json", "output-directory");

// Create a test environment
var testEnvironment = new TestEnvironmentManager();
await testEnvironment.CreateTestEnvironmentAsync("test-environment-definition.json");

// Generate sample data
var sampleDataGenerator = new SampleDataGenerator();
await sampleDataGenerator.GenerateSampleDataAsync("sample-data-definition.json");
```

### Diagnostics and Troubleshooting

```csharp
// Collect diagnostic information
var diagnosticsCollector = new DiagnosticsCollector();
var diagnosticInfo = await diagnosticsCollector.CollectDiagnosticsAsync();
File.WriteAllText("diagnostics.json", JsonConvert.SerializeObject(diagnosticInfo, Formatting.Indented));

// Analyze event flow
var eventFlowAnalyzer = new EventFlowAnalyzer();
var eventFlowReport = await eventFlowAnalyzer.AnalyzeEventFlowAsync("account-123");
Console.WriteLine(eventFlowReport.ToString());

// Check system health
var healthChecker = new HealthChecker();
var healthReport = await healthChecker.CheckHealthAsync();
Console.WriteLine($"System Health: {healthReport.Status}");
foreach (var entry in healthReport.Entries)
{
    Console.WriteLine($"- {entry.Key}: {entry.Value.Status} ({entry.Value.Description})");
}
```

## Related Documentation

- [Command API Reference](../api-reference/types/command.md)
- [Event API Reference](../api-reference/types/event.md)
- [AggregateRoot API Reference](../api-reference/types/aggregate-root.md)
- [IRepository API Reference](../api-reference/types/irepository.md)
- [IEventProcessor API Reference](../api-reference/types/ievent-processor.md)

## Navigation

**Section Navigation**:
- [← Previous: ReactiveDomain.IdentityStorage](identity-storage.md)
- [↑ Parent: Component Documentation](README.md)
- [→ Next: API Reference](../api-reference/README.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
