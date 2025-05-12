# Reactive Domain Learning Path

[← Back to Table of Contents](README.md)

This document provides a structured learning path for developers new to Reactive Domain, Event Sourcing, and CQRS. Follow this path to gain a comprehensive understanding of the framework and its underlying concepts.

## Learning Stages

### Stage 1: Core Concepts

Start by understanding the fundamental concepts behind Reactive Domain:

1. **Event Sourcing Basics**
   - [Core Concepts: Event Sourcing Fundamentals](core-concepts.md#event-sourcing-fundamentals)
   - [Event Store Architecture](core-concepts.md#event-store-architecture)
   - [Event, Command, and Message Flow](core-concepts.md#event-command-and-message-flow)

2. **CQRS Fundamentals**
   - [Core Concepts: CQRS Implementation](core-concepts.md#cqrs-implementation)
   - [Command and Query Separation](core-concepts.md#command-and-query-separation)
   - [Read Models and Projections](core-concepts.md#read-models-and-projections)

3. **Domain-Driven Design Concepts**
   - [Core Concepts: Domain-Driven Design](core-concepts.md#domain-driven-design-concepts)
   - [Aggregates and Aggregate Roots](core-concepts.md#aggregates)
   - [Value Objects and Entities](core-concepts.md#value-objects)
   - [Bounded Contexts](core-concepts.md#bounded-contexts)

### Stage 2: Practical Implementations

Once you understand the core concepts, move on to practical implementations:

1. **Setting Up Your Environment**
   - [Usage Patterns: Setting Up a New Reactive Domain Project](usage-patterns.md#setting-up-a-new-reactive-domain-project)
   - [Development Environment Configuration](deployment.md#development-environment-setup)
   - [Required Dependencies](usage-patterns.md#required-dependencies)

2. **Creating Your First Aggregate**
   - [Code Examples: Creating a New Aggregate Root](code-examples/creating-aggregate-root.md)
   - [Handling Commands and Generating Events](code-examples/handling-commands-events.md)
   - [Saving and Retrieving Aggregates](code-examples/saving-retrieving-aggregates.md)

3. **Building Read Models**
   - [Code Examples: Implementing Projections](code-examples/implementing-projections.md)
   - [Setting Up Event Listeners](code-examples/event-listeners.md)
   - [Querying Read Models](usage-patterns.md#querying-read-models)

### Stage 3: Advanced Topics

After mastering the basics, explore advanced topics:

1. **Message Correlation and Causation**
   - [Core Concepts: Correlation and Causation Tracking](core-concepts.md#correlation-and-causation-tracking)
   - [Code Examples: Handling Correlation and Causation](code-examples/correlation-causation.md)
   - [MessageBuilder and Correlated Messages](api-reference/types/message-builder.md)

2. **Snapshots and Performance**
   - [Code Examples: Implementing Snapshots](code-examples/implementing-snapshots.md)
   - [Performance Optimization Techniques](performance.md#snapshot-strategies)
   - [When to Use Snapshots](usage-patterns.md#when-to-use-snapshots)

3. **Testing Event-Sourced Systems**
   - [Code Examples: Testing Aggregates and Event Handlers](code-examples/testing.md)
   - [Test Fixtures and Helpers](api-reference/types/aggregate-test-fixture.md)
   - [Testing Strategies for Event-Sourced Systems](troubleshooting.md#testing-strategies-and-common-issues)

### Stage 4: Real-World Applications

Finally, see how everything comes together in real-world applications:

1. **Domain-Specific Examples**
   - [Banking Domain Example](code-examples/banking-domain-example.md)
   - [E-Commerce Domain Example](code-examples/ecommerce-domain-example.md)
   - [Inventory Management Example](code-examples/inventory-management-example.md)

2. **Integration and Deployment**
   - [Integration with ASP.NET Core](code-examples/aspnet-integration.md)
   - [Deployment Considerations](deployment.md#production-deployment-considerations)
   - [Scaling Strategies](deployment.md#scaling-strategies)

3. **Advanced Patterns and Practices**
   - [Process Managers and Sagas](api-reference/types/process-manager.md)
   - [Event Versioning and Schema Evolution](troubleshooting.md#event-versioning-and-schema-evolution)
   - [Handling Concurrency Conflicts](troubleshooting.md#handling-concurrency-conflicts)

## Recommended Learning Sequence

For the most effective learning experience, we recommend following this sequence:

1. **Week 1: Foundation**
   - Read the [Core Concepts](core-concepts.md) documentation
   - Set up your development environment
   - Create a simple "Hello World" aggregate

2. **Week 2: Basic Implementation**
   - Implement a basic aggregate with commands and events
   - Create a read model for your aggregate
   - Write tests for your aggregate and read model

3. **Week 3: Advanced Features**
   - Implement correlation and causation tracking
   - Add snapshot support
   - Explore performance optimization techniques

4. **Week 4: Real-World Application**
   - Build a complete application using Reactive Domain
   - Integrate with ASP.NET Core
   - Deploy your application

## Learning Resources

### Books

- **Event Sourcing and CQRS**
  - "Domain-Driven Design" by Eric Evans
  - "Implementing Domain-Driven Design" by Vaughn Vernon
  - "CQRS Documents" by Greg Young
  - "Event Sourcing and CQRS" by Martin Fowler

### Online Resources

- **Blogs and Articles**
  - [Martin Fowler on Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html)
  - [Greg Young's Blog](https://goodenoughsoftware.net/)
  - [CQRS Journey by Microsoft](https://docs.microsoft.com/en-us/previous-versions/msp-n-p/jj554200(v=pandp.10))

- **Videos and Presentations**
  - [Greg Young - CQRS and Event Sourcing](https://www.youtube.com/watch?v=JHGkaShoyNs)
  - [Udi Dahan - CQRS Deep Dive](https://www.youtube.com/watch?v=EqpalkqJD8M)
  - [Event Sourcing You are doing it wrong by David Schmitz](https://www.youtube.com/watch?v=GzrZworHpIk)

### Community Resources

- **Forums and Discussion Groups**
  - [DDD/CQRS Google Group](https://groups.google.com/g/dddcqrs)
  - [EventStoreDB Discussion](https://discuss.eventstore.com/)
  - [Stack Overflow - Event Sourcing Tag](https://stackoverflow.com/questions/tagged/event-sourcing)

- **GitHub Repositories**
  - [EventStoreDB](https://github.com/EventStore/EventStore)
  - [Sample Applications](https://github.com/ReactiveDomain/sample-applications)
  - [Workshop Materials](https://github.com/ReactiveDomain/workshop-materials)

## Interactive Learning

### Workshops and Exercises

1. **Basic Workshop: Account Management**
   - Create a simple banking application with accounts, deposits, and withdrawals
   - Implement event sourcing for account transactions
   - Build read models for account balances and transaction history

2. **Intermediate Workshop: E-Commerce System**
   - Implement an order processing system
   - Handle inventory management
   - Create read models for order history and product catalog

3. **Advanced Workshop: Distributed System**
   - Build a multi-service application
   - Implement process managers for cross-aggregate coordination
   - Handle distributed transactions and eventual consistency

### Coding Exercises

1. **Exercise 1: Create a Simple Aggregate**
   - Create an aggregate for a todo list item
   - Implement commands for creating, updating, and completing todos
   - Write tests for the aggregate

2. **Exercise 2: Build a Read Model**
   - Create a read model for todo lists
   - Implement an event handler to update the read model
   - Write queries against the read model

3. **Exercise 3: Implement Correlation**
   - Add correlation tracking to your todo list application
   - Implement a process manager for managing todo lists
   - Track message flow through the system

## Navigation

**Section Navigation**:
- [← Previous: Core Concepts](core-concepts.md)
- [↑ Parent: Home](README.md)
- [→ Next: Usage Patterns](usage-patterns.md)

**Quick Links**:
- [Home](README.md)
- [Core Concepts](core-concepts.md)
- [API Reference](api-reference/README.md)
- [Code Examples](code-examples/README.md)
- [Troubleshooting](troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
