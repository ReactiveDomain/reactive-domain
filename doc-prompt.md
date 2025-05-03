# Prompts for creating comprehensive documentation for the Reactive Domain library

{{ ... }}

Use Mermaid to generate diagrams and flowcharts.

Use the following prompts to create comprehensive documentation for the Reactive Domain library:

## Table of Contents Prompt

Create a table of contents for the documentation, including:

1. [x] Overview
2. [x] Core Concepts
3. [x] Component Documentation (partially complete)
4. [x] Interface Documentation (partially complete)
5. [x] Usage Patterns
6. [x] Code Examples (structure only)
7. [x] Troubleshooting Guide
8. [x] API Reference (partially complete)
9. [x] Architecture Guide
10. [x] Migration Guide
11. [x] Glossary
12. [x] FAQ
13. [x] Deployment Guide
14. [x] Performance Optimization Guide
15. [x] Security Guide
16. [x] Integration Guide
17. [x] Video Tutorial Script
18. [x] Workshop Materials
19. [x] Documentation Structure

## Overview Prompt

Generate comprehensive documentation for the Reactive Domain library, which is an open-source framework for implementing event sourcing in .NET projects using reactive programming principles. The documentation should cover all aspects of the library, including its core concepts, components, interfaces, and usage patterns. The documentation should be accessible to developers of all skill levels, from beginners to experts.

## Core Concepts Prompt

Document the core concepts of event sourcing as implemented in Reactive Domain, including:

1. Event sourcing fundamentals and how they're implemented in Reactive Domain
2. The event store architecture and integration with EventStoreDB
3. The CQRS (Command Query Responsibility Segregation) pattern implementation
4. The reactive programming principles used throughout the library
5. The domain-driven design concepts that underpin the framework
6. How events, commands, and messages flow through the system
7. The correlation and causation tracking mechanisms

## Component Documentation Prompt

Create detailed documentation for each of the following components, including their purpose, interfaces, implementation details, and usage examples:

1. **ReactiveDomain.Core**: Document the fundamental interfaces like `IEventSource`, `IMetadataSource`, and other core abstractions.
2. **ReactiveDomain.Foundation**: Document the domain implementation including `AggregateRoot`, `EventRecorder`, and repository patterns.
3. **ReactiveDomain.Messaging**: Document the messaging framework, including message types, handlers, and routing.
4. **ReactiveDomain.Persistence**: Document the event storage mechanisms, including `EventData`, `EventReadResult`, and stream operations.
5. **ReactiveDomain.Transport**: Document the transport layer for messages.
6. **ReactiveDomain.Testing**: Document the testing utilities and frameworks for event-sourced systems.
7. **ReactiveDomain.Policy**: Document the policy implementation and enforcement mechanisms.
8. **ReactiveDomain.IdentityStorage**: Document the identity storage mechanisms.
9. **ReactiveDomain.Tools**: Document the developer tools and utilities.

## Interface Documentation Prompt

Generate detailed documentation for all public interfaces in the library, including:

1. `IEventSource` - The core interface for event-sourced entities
2. `IRepository` - The repository pattern implementation for event-sourced aggregates
3. `ICorrelatedRepository` - The repository with correlation support
4. `IListener` - The event stream listener interface
5. `IMetadataSource` - The metadata handling interface
6. `ISnapshotSource` - The snapshot mechanism interface
7. `IStreamStoreConnection` - The event store connection interface
8. `IEventSerializer` - The event serialization interface
9. `IMessage`, `ICommand`, `IEvent` - The message type interfaces
10. `ICorrelatedMessage`, `ICorrelatedEventSource` - The correlation tracking interfaces

For each interface, include:
- Purpose and responsibility
- Method and property descriptions
- Usage patterns and best practices
- Implementation considerations
- Common pitfalls and how to avoid them

## Usage Patterns Prompt

Document the common usage patterns and best practices for Reactive Domain, including:

1. Setting up a new Reactive Domain project
2. Creating and working with aggregates
3. Implementing commands and events
4. Setting up repositories and event stores
5. Implementing projections and read models
6. Handling concurrency and versioning
7. Error handling and recovery strategies
8. Testing event-sourced systems
9. Performance optimization techniques
10. Integration with other systems and frameworks

## Code Examples Prompt

Create practical code examples that demonstrate:

1. Creating a new aggregate root
2. Handling commands and generating events
3. Saving and retrieving aggregates from repositories
4. Setting up event listeners and subscribers
5. Implementing projections for read models
6. Handling correlation and causation
7. Implementing snapshots for performance
8. Testing aggregates and event handlers
9. Integration with ASP.NET Core or other .NET applications
10. Complete sample applications demonstrating end-to-end workflows

## Troubleshooting Guide Prompt

Create a troubleshooting guide that addresses common issues and challenges when working with Reactive Domain, including:

1. Event versioning and schema evolution
2. Handling concurrency conflicts
3. Debugging event-sourced systems
4. Performance issues and optimization
5. Integration challenges with existing systems
6. Testing strategies and common testing issues
7. Deployment considerations and best practices
8. Monitoring and observability

## API Reference Prompt

Generate a complete API reference for all public types, methods, properties, and events in the Reactive Domain library, organized by namespace and assembly. The reference should include:

1. Type signatures and inheritance hierarchies
2. Method signatures, parameters, and return types
3. Property types and accessibility
4. Event patterns and subscription models
5. Extension points and customization options
6. Deprecation notices and migration paths

## Architecture Guide Prompt

Create an architecture guide that explains:

1. The high-level architecture of Reactive Domain
2. The design principles and patterns used
3. The component interactions and dependencies
4. The extension points and customization options
5. The integration patterns with other systems
6. Scaling and performance considerations
7. Security considerations and best practices

## Migration Guide Prompt

Create a migration guide for users upgrading from previous versions of Reactive Domain, including:

1. Breaking changes and deprecations
2. New features and enhancements
3. Migration strategies and patterns
4. Backward compatibility considerations
5. Testing strategies for migrations

## Glossary Prompt

Create a comprehensive glossary of terms used in Reactive Domain and event sourcing, including:

1. Event sourcing terminology
2. CQRS terminology
3. Domain-driven design terminology
4. Reactive programming terminology
5. Reactive Domain-specific terminology

## FAQ Prompt

Generate a frequently asked questions section that addresses common questions about Reactive Domain, including:

1. When to use event sourcing and CQRS
2. Performance considerations and optimizations
3. Scaling event-sourced systems
4. Integration with existing systems
5. Testing strategies and best practices
6. Common pitfalls and how to avoid them
7. Comparison with other event sourcing frameworks

## Deployment Guide Prompt

Create a deployment guide that covers:

1. Development environment setup
2. Testing environment configuration
3. Production deployment considerations
4. Scaling strategies
5. Monitoring and observability
6. Backup and recovery strategies
7. Security considerations

## Performance Optimization Guide Prompt

Generate a performance optimization guide that covers:

1. Event store performance considerations
2. Snapshot strategies for performance
3. Read model optimization techniques
4. Message handling performance
5. Scaling strategies for high-throughput systems
6. Monitoring and profiling techniques
7. Benchmarking and performance testing

## Security Guide Prompt

Create a security guide that addresses:

1. Authentication and authorization in event-sourced systems
2. Data protection and privacy considerations
3. Audit logging and compliance
4. Secure deployment practices
5. Threat modeling for event-sourced systems
6. Security testing strategies

## Integration Guide Prompt

Generate an integration guide that covers:

1. Integration with ASP.NET Core
2. Integration with other .NET frameworks and libraries
3. Integration with non-.NET systems
4. API design for event-sourced systems
5. Message contracts and versioning
6. Integration testing strategies

## Video Tutorial Script Prompt

Create scripts for video tutorials that demonstrate:

1. Getting started with Reactive Domain
2. Building a complete application with Reactive Domain
3. Advanced usage patterns and techniques
4. Performance optimization and scaling
5. Testing strategies and best practices

## Workshop Materials Prompt

Generate workshop materials for training developers on Reactive Domain, including:

1. Presentation slides
2. Hands-on exercises
3. Code samples and starter projects
4. Discussion questions and activities
5. Assessment materials

## Documentation Structure Prompt

Organize all the documentation into a coherent structure with:

1. A logical hierarchy of topics
2. Clear navigation paths for different user journeys
3. Cross-references between related topics
4. Progressive disclosure of complexity
5. Search-friendly organization and metadata
