# Reactive Domain Documentation Navigation Index

This index provides quick access to all documentation sections and shows relationships between different components and concepts in Reactive Domain.

## Documentation Sections

- [Home](README.md) - Main documentation page
- [Core Concepts](core-concepts.md) - Fundamental principles of event sourcing and CQRS
- [Component Documentation](components/README.md) - Documentation for major components
- [Interface Documentation](interfaces/README.md) - Documentation for key interfaces
- [Usage Patterns](usage-patterns.md) - Common usage patterns and best practices
- [Code Examples](code-examples/README.md) - Practical code examples
- [Troubleshooting Guide](troubleshooting.md) - Solutions to common issues
- [API Reference](api-reference/README.md) - Detailed API documentation
- [Architecture Guide](architecture.md) - System architecture overview
- [Component Relationships](component-relationships.md) - Visual guide to component relationships
- [Migration Guide](migration.md) - Upgrading between versions
- [Glossary](glossary.md) - Terminology reference
- [FAQ](faq.md) - Frequently asked questions
- [Deployment Guide](deployment.md) - Deployment instructions
- [Performance Optimization Guide](performance.md) - Performance tuning
- [Security Guide](security.md) - Security best practices
- [Integration Guide](integration.md) - Integration with other systems
- [Workshop Materials](workshop-materials.md) - Training materials

## Core Concepts Index

| Concept | Description | Documentation | Related Concepts |
|---------|-------------|---------------|------------------|
| Event Sourcing | Pattern of storing state changes as events | [Core Concepts](core-concepts.md#event-sourcing-fundamentals) | [Event Store Architecture](core-concepts.md#event-store-architecture), [Snapshots](core-concepts.md#snapshots) |
| CQRS | Command Query Responsibility Segregation | [Core Concepts](core-concepts.md#cqrs-implementation) | [Commands](core-concepts.md#commands), [Queries](core-concepts.md#queries), [Read Models](core-concepts.md#read-models) |
| Domain-Driven Design | Approach to software development | [Core Concepts](core-concepts.md#domain-driven-design-concepts) | [Aggregates](core-concepts.md#aggregates), [Value Objects](core-concepts.md#value-objects), [Entities](core-concepts.md#entities) |
| Reactive Programming | Programming with asynchronous data streams | [Core Concepts](core-concepts.md#reactive-programming-principles) | [Event Streams](core-concepts.md#event-streams), [Observers](core-concepts.md#observers) |
| Correlation and Causation | Tracking relationships between messages | [Core Concepts](core-concepts.md#correlation-and-causation-tracking) | [Message Flow](core-concepts.md#message-flow), [Distributed Tracing](core-concepts.md#distributed-tracing) |

## Component Index

| Component | Description | Documentation | Related Components |
|-----------|-------------|---------------|-------------------|
| AggregateRoot | Base class for domain aggregates | [API Reference](api-reference/types/aggregate-root.md) | [IEventSource](api-reference/types/ievent-source.md), [EventRecorder](api-reference/types/event-recorder.md) |
| Command | Base class for command messages | [API Reference](api-reference/types/command.md) | [ICommand](api-reference/types/icommand.md), [ICommandHandler](api-reference/types/icommand-handler.md), [ICommandBus](api-reference/types/icommand-bus.md) |
| Event | Base class for event messages | [API Reference](api-reference/types/event.md) | [IEvent](api-reference/types/ievent.md), [IEventHandler](api-reference/types/ievent-handler.md), [IEventBus](api-reference/types/ievent-bus.md) |
| MessageBuilder | Factory for creating correlated messages | [API Reference](api-reference/types/message-builder.md) | [ICorrelatedMessage](api-reference/types/icorrelated-message.md) |
| Repository | Storage for aggregates | [API Reference](api-reference/types/irepository.md) | [ICorrelatedRepository](api-reference/types/icorrelated-repository.md), [StreamStoreRepository](api-reference/types/stream-store-repository.md) |
| EventProcessor | Processes events from the event store | [API Reference](api-reference/types/ievent-processor.md) | [ICheckpointStore](api-reference/types/icheckpoint-store.md), [IEventBus](api-reference/types/ievent-bus.md) |
| ReadModelBase | Base class for read models | [API Reference](api-reference/types/read-model-base.md) | [IReadModelRepository](api-reference/types/iread-model-repository.md) |
| ProcessManager | Coordinates complex business processes | [API Reference](api-reference/types/process-manager.md) | [ICommandBus](api-reference/types/icommand-bus.md), [IEventBus](api-reference/types/ievent-bus.md) |

## Interface Index

| Interface | Description | Documentation | Implementations |
|-----------|-------------|---------------|----------------|
| IEventSource | Core interface for event-sourced entities | [API Reference](api-reference/types/ievent-source.md) | [AggregateRoot](api-reference/types/aggregate-root.md), [EventDrivenStateMachine](api-reference/types/event-driven-state-machine.md) |
| IRepository | Interface for repositories | [API Reference](api-reference/types/irepository.md) | [StreamStoreRepository](api-reference/types/stream-store-repository.md) |
| ICorrelatedRepository | Repository with correlation support | [API Reference](api-reference/types/icorrelated-repository.md) | [CorrelatedStreamStoreRepository](api-reference/types/correlated-stream-store-repository.md) |
| ICommand | Interface for commands | [API Reference](api-reference/types/icommand.md) | [Command](api-reference/types/command.md) |
| IEvent | Interface for events | [API Reference](api-reference/types/ievent.md) | [Event](api-reference/types/event.md) |
| ICorrelatedMessage | Interface for correlated messages | [API Reference](api-reference/types/icorrelated-message.md) | [Command](api-reference/types/command.md), [Event](api-reference/types/event.md) |
| ICommandBus | Interface for command bus | [API Reference](api-reference/types/icommand-bus.md) | [CommandBus](api-reference/types/command-bus.md) |
| IEventBus | Interface for event bus | [API Reference](api-reference/types/ievent-bus.md) | [EventBus](api-reference/types/event-bus.md) |
| IEventProcessor | Interface for event processors | [API Reference](api-reference/types/ievent-processor.md) | [EventProcessor](api-reference/types/event-processor.md) |
| ICheckpointStore | Interface for checkpoint stores | [API Reference](api-reference/types/icheckpoint-store.md) | [CheckpointStore](api-reference/types/checkpoint-store.md) |

## Code Examples Index

| Example | Description | Documentation | Related Examples |
|---------|-------------|---------------|------------------|
| Creating a New Aggregate Root | How to create a new aggregate root | [Code Examples](code-examples/creating-aggregate-root.md) | [Handling Commands and Events](code-examples/handling-commands-events.md) |
| Handling Commands and Events | How to handle commands and generate events | [Code Examples](code-examples/handling-commands-events.md) | [Creating a New Aggregate Root](code-examples/creating-aggregate-root.md), [Saving and Retrieving Aggregates](code-examples/saving-retrieving-aggregates.md) |
| Saving and Retrieving Aggregates | How to save and retrieve aggregates | [Code Examples](code-examples/saving-retrieving-aggregates.md) | [Handling Commands and Events](code-examples/handling-commands-events.md) |
| Setting Up Event Listeners | How to set up event listeners | [Code Examples](code-examples/event-listeners.md) | [Implementing Projections](code-examples/implementing-projections.md) |
| Implementing Projections | How to implement projections | [Code Examples](code-examples/implementing-projections.md) | [Setting Up Event Listeners](code-examples/event-listeners.md) |
| Handling Correlation and Causation | How to handle correlation and causation | [Code Examples](code-examples/correlation-causation.md) | [Handling Commands and Events](code-examples/handling-commands-events.md) |
| Implementing Snapshots | How to implement snapshots | [Code Examples](code-examples/implementing-snapshots.md) | [Saving and Retrieving Aggregates](code-examples/saving-retrieving-aggregates.md) |
| Testing Aggregates and Event Handlers | How to test aggregates and event handlers | [Code Examples](code-examples/testing.md) | [Creating a New Aggregate Root](code-examples/creating-aggregate-root.md), [Handling Commands and Events](code-examples/handling-commands-events.md) |
| Integration with ASP.NET Core | How to integrate with ASP.NET Core | [Code Examples](code-examples/aspnet-integration.md) | [Complete Sample Applications](code-examples/sample-applications.md) |
| Banking Domain Example | Real-world banking domain example | [Code Examples](code-examples/banking-domain-example.md) | [E-Commerce Domain Example](code-examples/ecommerce-domain-example.md), [Inventory Management Example](code-examples/inventory-management-example.md) |
| E-Commerce Domain Example | Real-world e-commerce domain example | [Code Examples](code-examples/ecommerce-domain-example.md) | [Banking Domain Example](code-examples/banking-domain-example.md), [Inventory Management Example](code-examples/inventory-management-example.md) |
| Inventory Management Example | Real-world inventory management example | [Code Examples](code-examples/inventory-management-example.md) | [Banking Domain Example](code-examples/banking-domain-example.md), [E-Commerce Domain Example](code-examples/ecommerce-domain-example.md) |

## Common Tasks Index

| Task | Documentation | Related Tasks |
|------|---------------|---------------|
| Understanding event sourcing | [Core Concepts](core-concepts.md) | [Understanding CQRS](core-concepts.md#cqrs-implementation) |
| Starting a new project | [Usage Patterns](usage-patterns.md#setting-up-a-new-reactive-domain-project) | [Creating an aggregate](code-examples/creating-aggregate-root.md) |
| Creating an aggregate | [Code Examples](code-examples/creating-aggregate-root.md) | [Handling commands and events](code-examples/handling-commands-events.md) |
| Implementing commands and events | [Code Examples](code-examples/handling-commands-events.md) | [Creating an aggregate](code-examples/creating-aggregate-root.md) |
| Setting up repositories | [Code Examples](code-examples/saving-retrieving-aggregates.md) | [Creating an aggregate](code-examples/creating-aggregate-root.md) |
| Creating read models | [Code Examples](code-examples/implementing-projections.md) | [Setting up event listeners](code-examples/event-listeners.md) |
| Implementing correlation | [Code Examples](code-examples/correlation-causation.md) | [Handling commands and events](code-examples/handling-commands-events.md) |
| Testing your application | [Code Examples](code-examples/testing.md) | [Creating an aggregate](code-examples/creating-aggregate-root.md) |
| Fixing a common issue | [Troubleshooting Guide](troubleshooting.md) | [FAQ](faq.md) |
| Optimizing performance | [Performance Optimization Guide](performance.md) | [Implementing snapshots](code-examples/implementing-snapshots.md) |
| Deploying to production | [Deployment Guide](deployment.md) | [Performance Optimization Guide](performance.md), [Security Guide](security.md) |

## Troubleshooting Index

| Issue | Solution | Documentation | Related Issues |
|-------|----------|---------------|----------------|
| Event versioning and schema evolution | How to handle event versioning | [Troubleshooting Guide](troubleshooting.md#event-versioning-and-schema-evolution) | [Handling concurrency conflicts](troubleshooting.md#handling-concurrency-conflicts) |
| Handling concurrency conflicts | How to handle concurrency conflicts | [Troubleshooting Guide](troubleshooting.md#handling-concurrency-conflicts) | [Event versioning and schema evolution](troubleshooting.md#event-versioning-and-schema-evolution) |
| Debugging event-sourced systems | How to debug event-sourced systems | [Troubleshooting Guide](troubleshooting.md#debugging-event-sourced-systems) | [Testing strategies and common issues](troubleshooting.md#testing-strategies-and-common-issues) |
| Performance issues and optimization | How to optimize performance | [Troubleshooting Guide](troubleshooting.md#performance-issues-and-optimization) | [Deployment considerations](troubleshooting.md#deployment-considerations) |
| Integration challenges | How to handle integration challenges | [Troubleshooting Guide](troubleshooting.md#integration-challenges) | [Deployment considerations](troubleshooting.md#deployment-considerations) |
| Testing strategies and common issues | How to test event-sourced systems | [Troubleshooting Guide](troubleshooting.md#testing-strategies-and-common-issues) | [Debugging event-sourced systems](troubleshooting.md#debugging-event-sourced-systems) |
| Deployment considerations | How to deploy event-sourced systems | [Troubleshooting Guide](troubleshooting.md#deployment-considerations) | [Performance issues and optimization](troubleshooting.md#performance-issues-and-optimization) |
