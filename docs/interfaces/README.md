# Reactive Domain Interfaces

[← Back to Table of Contents](../README.md)

This section provides detailed documentation for the key interfaces in the Reactive Domain library. These interfaces define the contracts that components must adhere to and form the foundation of the library's architecture.

## Table of Contents

### Core Interfaces

1. [IEventSource](event-source.md) - The core interface for event-sourced entities
2. [IRepository](repository.md) - The repository pattern implementation for event-sourced aggregates
3. [ICorrelatedRepository](correlated-repository.md) - The repository with correlation support
4. [IListener](listener.md) - The event stream listener interface
5. [IMetadataSource](metadata-source.md) - The metadata handling interface
6. [ISnapshotSource](snapshot-source.md) - The snapshot mechanism interface
7. [IStreamStoreConnection](stream-store-connection.md) - The event store connection interface
8. [IEventSerializer](event-serializer.md) - The event serialization interface

### Message Interfaces

9. [IMessage](message.md) - The base message interface
10. [ICommand](command.md) - The command message interface
11. [IEvent](event.md) - The event message interface
12. [ICorrelatedMessage](correlated-message.md) - The correlation tracking interface
13. [ICorrelatedEventSource](correlated-event-source.md) - The correlation tracking for event sources

Each interface documentation includes:

- Purpose and responsibility
- Method and property descriptions
- Usage patterns and best practices
- Implementation considerations
- Common pitfalls and how to avoid them

[↑ Back to Top](#reactive-domain-interfaces) | [← Back to Table of Contents](../README.md)
