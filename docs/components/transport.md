# ReactiveDomain.Transport Component

[← Back to Components](README.md)

The ReactiveDomain.Transport component provides messaging infrastructure for communication between different parts of a Reactive Domain application or between different applications. It implements various transport mechanisms for commands and events.

## Key Features

- Message routing and delivery
- Pub/sub messaging patterns
- Remote command handling
- Distributed event processing
- Message serialization and deserialization
- Transport-level security

## Core Types

### Message Transport

- **MessageTransport**: Base class for message transport implementations
- **InProcessMessageTransport**: In-process message transport for local communication
- **RabbitMqMessageTransport**: RabbitMQ-based message transport for distributed communication
- **AzureServiceBusTransport**: Azure Service Bus-based message transport

### Message Routing

- **MessageRouter**: Routes messages to appropriate handlers
- **SubscriptionManager**: Manages message subscriptions
- **TopicNameFormatter**: Formats topic names for pub/sub messaging

### Serialization

- **MessageSerializer**: Serializes and deserializes messages for transport
- **JsonMessageSerializer**: JSON-based message serializer
- **ProtobufMessageSerializer**: Protocol Buffers-based message serializer

## Usage Examples

### Configuring In-Process Transport

```csharp
// Create an in-process message transport
var transport = new InProcessMessageTransport();

// Register command handlers
transport.RegisterCommandHandler<CreateAccount>(cmd => 
{
    // Handle command
});

// Subscribe to events
transport.Subscribe<AccountCreated>(evt => 
{
    // Handle event
});
```

### Configuring RabbitMQ Transport

```csharp
// Create RabbitMQ connection settings
var connectionSettings = new RabbitMqConnectionSettings
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest",
    VirtualHost = "/"
};

// Create a RabbitMQ message transport
var transport = new RabbitMqMessageTransport(connectionSettings);

// Configure message routing
transport.ConfigureRouting(routing =>
{
    routing.RouteCommandsToEndpoint<CreateAccount>("accounts-service");
    routing.RouteEventsToExchange<AccountCreated>("account-events");
});

// Start the transport
transport.Start();
```

### Publishing Messages

```csharp
// Create a command
var createAccountCommand = new CreateAccount(
    Guid.NewGuid(),
    "12345",
    100.0m,
    Guid.NewGuid(),
    Guid.NewGuid(),
    Guid.Empty);

// Send the command
transport.Send(createAccountCommand);

// Create an event
var accountCreatedEvent = new AccountCreated(
    Guid.NewGuid(),
    "12345",
    100.0m,
    Guid.NewGuid(),
    Guid.NewGuid(),
    Guid.NewGuid());

// Publish the event
transport.Publish(accountCreatedEvent);
```

## Integration with Other Components

The Transport component integrates with:

- **ReactiveDomain.Core**: Uses core message interfaces
- **ReactiveDomain.Messaging**: Transports messages defined in the messaging component
- **ReactiveDomain.Foundation**: Provides communication infrastructure for domain components

## Configuration Options

### Common Transport Settings

- **Serializer**: Message serializer to use
- **RetryPolicy**: Policy for retrying failed message delivery
- **DeadLetterQueue**: Queue for messages that cannot be delivered
- **MessageTtl**: Time-to-live for messages
- **PrefetchCount**: Number of messages to prefetch

### RabbitMQ-Specific Settings

- **HostName**: RabbitMQ host name
- **Port**: RabbitMQ port
- **UserName**: RabbitMQ user name
- **Password**: RabbitMQ password
- **VirtualHost**: RabbitMQ virtual host
- **ExchangeType**: Type of exchange to use (direct, fanout, topic, headers)
- **Durable**: Whether exchanges and queues should be durable
- **AutoDelete**: Whether exchanges and queues should be auto-deleted

### Azure Service Bus-Specific Settings

- **ConnectionString**: Azure Service Bus connection string
- **QueueName**: Name of the queue
- **TopicName**: Name of the topic
- **SubscriptionName**: Name of the subscription
- **EnablePartitioning**: Whether to enable partitioning
- **EnableBatchedOperations**: Whether to enable batched operations

## Best Practices

1. **Use Correlation IDs**: Always include correlation IDs in messages for tracking
2. **Implement Idempotent Handlers**: Ensure handlers can process the same message multiple times without side effects
3. **Use Dead Letter Queues**: Configure dead letter queues for messages that cannot be processed
4. **Monitor Message Flow**: Implement monitoring for message flow and processing
5. **Secure Transport**: Use secure connections and authentication for distributed messaging
6. **Handle Failures Gracefully**: Implement retry policies and circuit breakers for resilience

## Common Issues and Solutions

### Connection Issues

If you're having trouble connecting to the message broker:

1. Check that the broker is running
2. Verify connection settings
3. Check network connectivity
4. Verify credentials

### Message Delivery Issues

If messages are not being delivered:

1. Check that the routing is configured correctly
2. Verify that subscribers are registered
3. Check for errors in message serialization
4. Verify queue and exchange configuration

### Performance Issues

If you're experiencing performance issues:

1. Adjust prefetch count
2. Consider batching messages
3. Optimize message serialization
4. Scale out message brokers

## Related Documentation

- [ICommandBus API Reference](../api-reference/types/icommand-bus.md)
- [IEventBus API Reference](../api-reference/types/ievent-bus.md)
- [Command API Reference](../api-reference/types/command.md)
- [Event API Reference](../api-reference/types/event.md)
- [ICorrelatedMessage API Reference](../api-reference/types/icorrelated-message.md)

## Navigation

**Section Navigation**:
- [← Previous: ReactiveDomain.Persistence](persistence.md)
- [↑ Parent: Component Documentation](README.md)
- [→ Next: ReactiveDomain.Testing](testing.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
