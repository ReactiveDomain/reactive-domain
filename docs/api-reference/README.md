# Reactive Domain API Reference

[← Back to Table of Contents](../README.md)

This section provides a comprehensive reference for all public types, methods, properties, and events in the Reactive Domain library, organized by namespace and key types.

## Table of Contents

- [Organization](#organization)
- [Key Types](#key-types)
- [Namespaces](#namespaces)
- [Assemblies](#assemblies)

## Organization

To make the API reference more manageable, we've organized it into the following sections:

1. **Key Types**: Documentation for the most important and commonly used types
2. **Namespaces**: Documentation organized by namespace
3. **Assemblies**: Documentation organized by assembly

Each type is documented with:
- Type signature and inheritance hierarchy
- Method signatures, parameters, and return types
- Property types and accessibility
- Usage examples and common patterns
- Related types and interfaces

## Navigation

For easier navigation through the API reference, use these resources:

- [Component Relationships](../component-relationships.md) - Visual guide showing how different components work together
- [Navigation Index](../navigation-index.md) - Comprehensive index of all documentation with cross-references
- [Related Components](#key-types) - Each component documentation includes links to related components

## Key Types

### Core Interfaces

- [IEventSource](types/ievent-source.md) - The core interface for event-sourced entities
- [IRepository](types/irepository.md) - Interface for repositories
- [ICorrelatedRepository](types/icorrelated-repository.md) - Repository with correlation support
- [IMetadataSource](types/imetadata-source.md) - Interface for metadata handling
- [ISnapshotSource](types/isnapshot-source.md) - Interface for snapshot support

### Base Classes

- [AggregateRoot](types/aggregate-root.md) - Base class for domain aggregates
- [EventRecorder](types/event-recorder.md) - Utility for recording events
- [ReadModelBase](types/read-model-base.md) - Base class for read models in CQRS architecture

### Message Types

- [IMessage](types/imessage.md) - Base interface for messages
- [ICommand](types/icommand.md) - Interface for commands
- [IEvent](types/ievent.md) - Interface for events
- [ICorrelatedMessage](types/icorrelated-message.md) - Interface for correlated messages
- [Command](types/command.md) - Base class for command messages
- [Event](types/event.md) - Base class for event messages

### Utilities

- [MessageBuilder](types/message-builder.md) - Factory for creating correlated messages

### Repositories

- [StreamStoreRepository](types/stream-store-repository.md) - Implementation of IRepository
- [CorrelatedStreamStoreRepository](types/correlated-stream-store-repository.md) - Implementation of ICorrelatedRepository
- [IReadModelRepository](types/iread-model-repository.md) - Interface for read model repositories

### Event Store

- [IStreamStoreConnection](types/istream-store-connection.md) - Interface for event store connections
- [StreamStoreConnection](types/stream-store-connection.md) - Implementation of IStreamStoreConnection

### CQRS Components

- [Query Handling](types/query-handling.md) - Patterns and best practices for handling queries
- [Event Subscription](types/event-subscription.md) - Patterns for subscribing to and processing events

## Namespaces

The Reactive Domain library is organized into the following namespaces:

- [ReactiveDomain](namespaces/reactivedomain.md) - Core interfaces and types
- [ReactiveDomain.Foundation](namespaces/reactivedomain-foundation.md) - Domain implementation
- [ReactiveDomain.Foundation.Domain](namespaces/reactivedomain-foundation-domain.md) - Domain-specific types
- [ReactiveDomain.Foundation.StreamStore](namespaces/reactivedomain-foundation-streamstore.md) - Stream store implementation
- [ReactiveDomain.Messaging](namespaces/reactivedomain-messaging.md) - Messaging framework
- [ReactiveDomain.Messaging.Messages](namespaces/reactivedomain-messaging-messages.md) - Message types
- [ReactiveDomain.Persistence](namespaces/reactivedomain-persistence.md) - Event storage
- [ReactiveDomain.Transport](namespaces/reactivedomain-transport.md) - Transport layer
- [ReactiveDomain.Testing](namespaces/reactivedomain-testing.md) - Testing utilities
- [ReactiveDomain.Policy](namespaces/reactivedomain-policy.md) - Policy implementation
- [ReactiveDomain.IdentityStorage](namespaces/reactivedomain-identitystorage.md) - Identity storage
- [ReactiveDomain.Tools](namespaces/reactivedomain-tools.md) - Developer tools

## Assemblies

The Reactive Domain library consists of the following assemblies:

- [ReactiveDomain.Core](assemblies/reactivedomain-core.md) - Core interfaces and abstractions
- [ReactiveDomain.Foundation](assemblies/reactivedomain-foundation.md) - Domain implementation
- [ReactiveDomain.Messaging](assemblies/reactivedomain-messaging.md) - Messaging framework
- [ReactiveDomain.Persistence](assemblies/reactivedomain-persistence.md) - Event storage
- [ReactiveDomain.Transport](assemblies/reactivedomain-transport.md) - Transport layer
- [ReactiveDomain.Testing](assemblies/reactivedomain-testing.md) - Testing utilities
- [ReactiveDomain.Policy](assemblies/reactivedomain-policy.md) - Policy implementation
- [ReactiveDomain.IdentityStorage](assemblies/reactivedomain-identitystorage.md) - Identity storage
- [ReactiveDomain.Tools](assemblies/reactivedomain-tools.md) - Developer tools

---

**Navigation**:
- [← Previous: Code Examples](../code-examples/README.md)
- [↑ Back to Top](#reactive-domain-api-reference)
- [→ Next: Architecture Guide](../architecture.md)

[↑ Back to Top](#reactive-domain-api-reference) | [← Back to Table of Contents](../README.md)
