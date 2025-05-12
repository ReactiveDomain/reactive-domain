# Deployment Guide

[← Back to Table of Contents](README.md)

This guide provides best practices and considerations for deploying Reactive Domain applications to various environments.

## Table of Contents

- [Development Environment Setup](#development-environment-setup)
- [Testing Environment Configuration](#testing-environment-configuration)
- [Production Deployment Considerations](#production-deployment-considerations)
- [Scaling Strategies](#scaling-strategies)
- [Monitoring and Observability](#monitoring-and-observability)
- [Backup and Recovery Strategies](#backup-and-recovery-strategies)
- [Security Considerations](#security-considerations)

## Development Environment Setup

### Prerequisites

To set up a development environment for Reactive Domain applications, you'll need:

- **.NET SDK**: .NET 7.0 or later
- **EventStoreDB**: Version 20.10 or later
- **IDE**: Visual Studio 2022, JetBrains Rider, or Visual Studio Code
- **Git**: For source control
- **Docker**: For containerized development (optional)

### Local EventStoreDB Setup

#### Using Docker

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

#### Using Installer

1. Download the installer from [EventStoreDB Downloads](https://eventstore.com/downloads/)
2. Follow the installation instructions for your platform
3. Configure EventStoreDB to run as a service

### Project Setup

1. **Create a new solution**:
   ```bash
   dotnet new sln -n MyReactiveDomainApp
   ```

2. **Add projects**:
   ```bash
   dotnet new classlib -n MyReactiveDomainApp.Domain
   dotnet new classlib -n MyReactiveDomainApp.Infrastructure
   dotnet new classlib -n MyReactiveDomainApp.Application
   dotnet new webapi -n MyReactiveDomainApp.Api
   dotnet new xunit -n MyReactiveDomainApp.Tests
   
   dotnet sln add MyReactiveDomainApp.Domain
   dotnet sln add MyReactiveDomainApp.Infrastructure
   dotnet sln add MyReactiveDomainApp.Application
   dotnet sln add MyReactiveDomainApp.Api
   dotnet sln add MyReactiveDomainApp.Tests
   ```

3. **Add Reactive Domain packages**:
   ```bash
   dotnet add MyReactiveDomainApp.Domain package ReactiveDomain.Core
   dotnet add MyReactiveDomainApp.Domain package ReactiveDomain.Foundation
   dotnet add MyReactiveDomainApp.Infrastructure package ReactiveDomain.Persistence
   dotnet add MyReactiveDomainApp.Infrastructure package ReactiveDomain.Messaging
   dotnet add MyReactiveDomainApp.Application package ReactiveDomain.Messaging
   dotnet add MyReactiveDomainApp.Tests package ReactiveDomain.Testing
   ```

4. **Configure connection strings**:
   ```json
   {
     "EventStore": {
       "ConnectionString": "tcp://admin:changeit@localhost:1113"
     }
   }
   ```

### Development Workflow

1. **Define domain model**:
   - Create aggregates, events, and commands in the Domain project
   - Implement business logic in aggregates

2. **Implement infrastructure**:
   - Configure event store connection
   - Implement repositories
   - Set up message bus

3. **Implement application services**:
   - Create command handlers
   - Create event handlers
   - Implement read models

4. **Expose API**:
   - Create API controllers
   - Configure dependency injection
   - Set up authentication and authorization

5. **Write tests**:
   - Unit tests for aggregates
   - Integration tests for repositories
   - End-to-end tests for API

## Testing Environment Configuration

### Environment Setup

1. **Isolated EventStoreDB**:
   - Set up a dedicated EventStoreDB instance for testing
   - Use Docker for easy setup and teardown

2. **Continuous Integration**:
   - Configure CI pipeline to run tests
   - Set up automated deployment to testing environment

3. **Test Data Management**:
   - Create scripts to initialize test data
   - Implement test data cleanup

### Testing Strategies

1. **Unit Testing**:
   ```csharp
   [Fact]
   public void CanCreateAccount()
   {
       // Arrange
       var accountId = Guid.NewGuid();
       var account = new Account(accountId);
       
       // Act
       account.Initialize("John Doe", 100);
       
       // Assert
       var events = ((IEventSource)account).TakeEvents();
       Assert.Single(events);
       var @event = Assert.IsType<AccountCreated>(events[0]);
       Assert.Equal(accountId, @event.AccountId);
       Assert.Equal("John Doe", @event.Owner);
       Assert.Equal(100, @event.InitialBalance);
   }
   ```

2. **Integration Testing**:
   ```csharp
   [Fact]
   public async Task CanSaveAndLoadAggregate()
   {
       // Arrange
       var accountId = Guid.NewGuid();
       var connectionString = "tcp://admin:changeit@localhost:1113";
       var connection = new EventStoreConnection(connectionString);
       connection.Connect();
       var repository = new StreamStoreRepository(connection);
       
       // Act - Save
       var account = new Account(accountId);
       account.Initialize("John Doe", 100);
       repository.Save(account);
       
       // Act - Load
       repository.TryGetById<Account>(accountId, out var loadedAccount);
       
       // Assert
       Assert.Equal(100, loadedAccount.GetBalance());
   }
   ```

3. **End-to-End Testing**:
   ```csharp
   [Fact]
   public async Task CanCreateAndRetrieveAccount()
   {
       // Arrange
       var client = _factory.CreateClient();
       var accountId = Guid.NewGuid();
       
       // Act - Create
       var createResponse = await client.PostAsJsonAsync("/api/accounts", new
       {
           Id = accountId,
           Owner = "John Doe",
           InitialBalance = 100
       });
       
       // Assert - Create
       createResponse.EnsureSuccessStatusCode();
       
       // Act - Retrieve
       var getResponse = await client.GetAsync($"/api/accounts/{accountId}");
       
       // Assert - Retrieve
       getResponse.EnsureSuccessStatusCode();
       var account = await getResponse.Content.ReadFromJsonAsync<AccountDto>();
       Assert.Equal(accountId, account.Id);
       Assert.Equal("John Doe", account.Owner);
       Assert.Equal(100, account.Balance);
   }
   ```

### Test Environment Configuration

```json
{
  "EventStore": {
    "ConnectionString": "tcp://admin:changeit@test-eventstore:1113"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## Production Deployment Considerations

### Infrastructure Requirements

1. **EventStoreDB Cluster**:
   - Minimum 3-node cluster for high availability
   - Sufficient storage for event data
   - Backup infrastructure

2. **Application Servers**:
   - Sufficient capacity for command processing
   - Sufficient capacity for query processing
   - Load balancing for high availability

3. **Read Model Databases**:
   - Appropriate database for read models
   - Sufficient capacity for query load
   - Backup infrastructure

### Deployment Process

1. **Database Migrations**:
   - Deploy read model database schema changes
   - No migrations needed for event store (append-only)

2. **Application Deployment**:
   - Deploy command-side services
   - Deploy query-side services
   - Deploy API services

3. **Verification**:
   - Verify connectivity to event store
   - Verify connectivity to read model databases
   - Run smoke tests

### Containerization

#### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["MyReactiveDomainApp.Api/MyReactiveDomainApp.Api.csproj", "MyReactiveDomainApp.Api/"]
COPY ["MyReactiveDomainApp.Application/MyReactiveDomainApp.Application.csproj", "MyReactiveDomainApp.Application/"]
COPY ["MyReactiveDomainApp.Domain/MyReactiveDomainApp.Domain.csproj", "MyReactiveDomainApp.Domain/"]
COPY ["MyReactiveDomainApp.Infrastructure/MyReactiveDomainApp.Infrastructure.csproj", "MyReactiveDomainApp.Infrastructure/"]
RUN dotnet restore "MyReactiveDomainApp.Api/MyReactiveDomainApp.Api.csproj"
COPY . .
WORKDIR "/src/MyReactiveDomainApp.Api"
RUN dotnet build "MyReactiveDomainApp.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyReactiveDomainApp.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyReactiveDomainApp.Api.dll"]
```

#### Docker Compose

```yaml
version: '3.8'

services:
  eventstore:
    image: eventstore/eventstore:latest
    environment:
      - EVENTSTORE_CLUSTER_SIZE=1
      - EVENTSTORE_RUN_PROJECTIONS=All
      - EVENTSTORE_START_STANDARD_PROJECTIONS=true
      - EVENTSTORE_EXT_TCP_PORT=1113
      - EVENTSTORE_HTTP_PORT=2113
      - EVENTSTORE_INSECURE=true
      - EVENTSTORE_ENABLE_EXTERNAL_TCP=true
      - EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true
    ports:
      - "1113:1113"
      - "2113:2113"
    volumes:
      - eventstore-data:/var/lib/eventstore
    networks:
      - reactive-domain-network

  api:
    build:
      context: .
      dockerfile: MyReactiveDomainApp.Api/Dockerfile
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - EventStore__ConnectionString=tcp://admin:changeit@eventstore:1113
    depends_on:
      - eventstore
    networks:
      - reactive-domain-network

networks:
  reactive-domain-network:
    driver: bridge

volumes:
  eventstore-data:
```

### Kubernetes Deployment

#### EventStoreDB StatefulSet

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: eventstore
spec:
  serviceName: eventstore
  replicas: 3
  selector:
    matchLabels:
      app: eventstore
  template:
    metadata:
      labels:
        app: eventstore
    spec:
      containers:
      - name: eventstore
        image: eventstore/eventstore:latest
        ports:
        - containerPort: 1113
          name: tcp
        - containerPort: 2113
          name: http
        env:
        - name: EVENTSTORE_CLUSTER_SIZE
          value: "3"
        - name: EVENTSTORE_INT_IP
          valueFrom:
            fieldRef:
              fieldPath: status.podIP
        - name: EVENTSTORE_CLUSTER_DNS
          value: "eventstore-0.eventstore.default.svc.cluster.local,eventstore-1.eventstore.default.svc.cluster.local,eventstore-2.eventstore.default.svc.cluster.local"
        - name: EVENTSTORE_CLUSTER_GOSSIP_PORT
          value: "2113"
        - name: EVENTSTORE_RUN_PROJECTIONS
          value: "All"
        - name: EVENTSTORE_START_STANDARD_PROJECTIONS
          value: "true"
        - name: EVENTSTORE_INSECURE
          value: "true"
        - name: EVENTSTORE_ENABLE_EXTERNAL_TCP
          value: "true"
        - name: EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP
          value: "true"
        volumeMounts:
        - name: eventstore-data
          mountPath: /var/lib/eventstore
  volumeClaimTemplates:
  - metadata:
      name: eventstore-data
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 10Gi
```

#### API Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: reactive-domain-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: reactive-domain-api
  template:
    metadata:
      labels:
        app: reactive-domain-api
    spec:
      containers:
      - name: api
        image: myreactivedomainapp/api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
        - name: EventStore__ConnectionString
          value: tcp://admin:changeit@eventstore:1113
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 10
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 15
          periodSeconds: 20
```

## Scaling Strategies

### Command-Side Scaling

1. **Horizontal Scaling**:
   - Deploy multiple instances of command-side services
   - Use load balancing to distribute commands
   - Ensure idempotent command handling

2. **Partitioning**:
   - Partition aggregates by type or ID
   - Route commands to specific partitions
   - Reduce contention on aggregates

3. **Command Queuing**:
   - Queue commands for asynchronous processing
   - Process commands in batches
   - Implement backpressure mechanisms

### Query-Side Scaling

1. **Read Model Optimization**:
   - Optimize read models for specific query patterns
   - Denormalize data for efficient queries
   - Use appropriate database technology

2. **Caching**:
   - Cache frequently accessed read models
   - Implement cache invalidation based on events
   - Use distributed caching for multiple instances

3. **Read Replicas**:
   - Deploy read replicas for high-volume queries
   - Distribute queries across replicas
   - Accept eventual consistency for better performance

### EventStoreDB Scaling

1. **Cluster Configuration**:
   - Deploy a multi-node cluster for high availability
   - Configure appropriate hardware for each node
   - Monitor cluster health and performance

2. **Stream Partitioning**:
   - Partition streams by aggregate type
   - Use category projections for efficient querying
   - Implement custom stream naming strategies

3. **Subscription Optimization**:
   - Use persistent subscriptions for reliable event processing
   - Configure appropriate subscription settings
   - Monitor subscription performance

## Monitoring and Observability

### Key Metrics

1. **EventStoreDB Metrics**:
   - Queue length
   - Write throughput
   - Read throughput
   - Disk usage
   - Memory usage

2. **Application Metrics**:
   - Command throughput
   - Command latency
   - Event processing throughput
   - Event processing latency
   - Read model update latency

3. **Infrastructure Metrics**:
   - CPU usage
   - Memory usage
   - Network throughput
   - Disk I/O
   - Error rates

### Logging

1. **Structured Logging**:
   ```csharp
   public class LoggingCommandBus : ICommandBus
   {
       private readonly ICommandBus _innerBus;
       private readonly ILogger<LoggingCommandBus> _logger;
       
       public LoggingCommandBus(ICommandBus innerBus, ILogger<LoggingCommandBus> logger)
       {
           _innerBus = innerBus;
           _logger = logger;
       }
       
       public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
       {
           _logger.LogInformation(
               "Sending command {CommandType} with ID {CommandId}",
               typeof(TCommand).Name,
               command is ICorrelatedMessage msg ? msg.CorrelationId : Guid.Empty);
               
           _innerBus.Send(command);
       }
   }
   ```

2. **Event Logging**:
   ```csharp
   public class LoggingEventHandler<TEvent> : IEventHandler<TEvent>
   {
       private readonly IEventHandler<TEvent> _innerHandler;
       private readonly ILogger<LoggingEventHandler<TEvent>> _logger;
       
       public LoggingEventHandler(IEventHandler<TEvent> innerHandler, ILogger<LoggingEventHandler<TEvent>> logger)
       {
           _innerHandler = innerHandler;
           _logger = logger;
       }
       
       public void Handle(TEvent @event)
       {
           _logger.LogInformation(
               "Handling event {EventType}",
               typeof(TEvent).Name);
               
           _innerHandler.Handle(@event);
       }
   }
   ```

### Distributed Tracing

1. **Correlation and Causation Tracking**:
   ```csharp
   public class TracingCommandBus : ICommandBus
   {
       private readonly ICommandBus _innerBus;
       private readonly ITracer _tracer;
       
       public TracingCommandBus(ICommandBus innerBus, ITracer tracer)
       {
           _innerBus = innerBus;
           _tracer = tracer;
       }
       
       public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
       {
           using (var scope = _tracer.StartActiveSpan($"command.{typeof(TCommand).Name}"))
           {
               if (command is ICorrelatedMessage correlatedMessage)
               {
                   scope.SetTag("correlation_id", correlatedMessage.CorrelationId.ToString());
                   scope.SetTag("causation_id", correlatedMessage.CausationId.ToString());
               }
               
               _innerBus.Send(command);
           }
       }
   }
   ```

### Health Checks

```csharp
public class EventStoreHealthCheck : IHealthCheck
{
    private readonly IStreamStoreConnection _connection;
    
    public EventStoreHealthCheck(IStreamStoreConnection connection)
    {
        _connection = connection;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to read a stream
            var slice = _connection.ReadStreamForward("$stats-0.0.0.0:2113", 0, 1);
            
            return HealthCheckResult.Healthy("EventStore connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("EventStore connection is unhealthy", ex);
        }
    }
}
```

## Backup and Recovery Strategies

### Event Store Backup

1. **File System Backup**:
   - Back up the EventStoreDB data directory
   - Use file system snapshots for consistent backups
   - Store backups off-site

2. **Stream Backup**:
   - Use the EventStoreDB API to read all streams
   - Store events in a backup format
   - Implement incremental backup strategies

3. **Backup Automation**:
   - Schedule regular backups
   - Verify backup integrity
   - Test recovery procedures

### Recovery Procedures

1. **EventStoreDB Recovery**:
   - Restore from file system backup
   - Rebuild indexes if necessary
   - Verify stream integrity

2. **Read Model Recovery**:
   - Rebuild read models from events
   - Implement catch-up subscriptions
   - Verify read model integrity

3. **Point-in-Time Recovery**:
   - Restore events up to a specific point in time
   - Rebuild read models to the same point
   - Verify system consistency

### Disaster Recovery

1. **Multi-Region Deployment**:
   - Deploy EventStoreDB clusters in multiple regions
   - Implement cross-region replication
   - Configure failover procedures

2. **Recovery Testing**:
   - Regularly test recovery procedures
   - Simulate disaster scenarios
   - Measure recovery time and data loss

3. **Documentation**:
   - Document recovery procedures
   - Train operations staff
   - Update procedures based on testing results

## Security Considerations

### Authentication and Authorization

1. **API Security**:
   - Implement OAuth 2.0 or OpenID Connect
   - Use JWT for authentication
   - Implement role-based access control

2. **EventStoreDB Security**:
   - Configure EventStoreDB authentication
   - Use TLS for secure communication
   - Implement network security controls

3. **Command Authorization**:
   ```csharp
   public class AuthorizedCommandBus : ICommandBus
   {
       private readonly ICommandBus _innerBus;
       private readonly IAuthorizationService _authorizationService;
       private readonly IUserContext _userContext;
       
       public AuthorizedCommandBus(
           ICommandBus innerBus,
           IAuthorizationService authorizationService,
           IUserContext userContext)
       {
           _innerBus = innerBus;
           _authorizationService = authorizationService;
           _userContext = userContext;
       }
       
       public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
       {
           // Authorize the command
           if (!_authorizationService.IsAuthorized(_userContext.CurrentUser, command))
           {
               throw new UnauthorizedAccessException($"User is not authorized to execute {typeof(TCommand).Name}");
           }
           
           // Forward to inner bus
           _innerBus.Send(command);
       }
   }
   ```

### Data Protection

1. **Sensitive Data Handling**:
   - Encrypt sensitive data in events
   - Implement data masking for logs
   - Use secure storage for secrets

2. **Event Data Encryption**:
   ```csharp
   public class EncryptingEventSerializer : IEventSerializer
   {
       private readonly IEventSerializer _innerSerializer;
       private readonly IEncryptionService _encryptionService;
       
       public EncryptingEventSerializer(
           IEventSerializer innerSerializer,
           IEncryptionService encryptionService)
       {
           _innerSerializer = innerSerializer;
           _encryptionService = encryptionService;
       }
       
       public object Deserialize(RecordedEvent recordedEvent)
       {
           // Decrypt event data if necessary
           if (ShouldEncrypt(recordedEvent.EventType))
           {
               var decryptedData = _encryptionService.Decrypt(recordedEvent.Data);
               var decryptedEvent = new RecordedEvent(
                   recordedEvent.EventStreamId,
                   recordedEvent.EventNumber,
                   recordedEvent.EventId,
                   recordedEvent.EventType,
                   decryptedData,
                   recordedEvent.Metadata,
                   recordedEvent.IsJson,
                   recordedEvent.Created);
                   
               return _innerSerializer.Deserialize(decryptedEvent);
           }
           
           return _innerSerializer.Deserialize(recordedEvent);
       }
       
       public IEventData Serialize(object @event, Guid eventId)
       {
           var eventData = _innerSerializer.Serialize(@event, eventId);
           
           // Encrypt event data if necessary
           if (ShouldEncrypt(@event.GetType().Name))
           {
               var encryptedData = _encryptionService.Encrypt(eventData.Data);
               return new EventData(
                   eventData.EventId,
                   eventData.Type,
                   eventData.IsJson,
                   encryptedData,
                   eventData.Metadata);
           }
           
           return eventData;
       }
       
       private bool ShouldEncrypt(string eventType)
       {
           // Determine if the event type should be encrypted
           return eventType.Contains("Sensitive") || eventType.Contains("Personal");
       }
   }
   ```

3. **Transport Security**:
   - Use HTTPS for all API communication
   - Use TLS for EventStoreDB communication
   - Implement proper certificate management

### Compliance

1. **Audit Logging**:
   - Log all security-relevant events
   - Implement tamper-evident logging
   - Store logs securely

2. **Data Retention**:
   - Implement data retention policies
   - Provide mechanisms for data deletion
   - Document compliance measures

3. **Privacy Controls**:
   - Implement privacy by design
   - Provide mechanisms for data subject requests
   - Document privacy impact assessments

[↑ Back to Top](#deployment-guide) | [← Back to Table of Contents](README.md)
