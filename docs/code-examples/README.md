# Reactive Domain Code Examples

[← Back to Table of Contents](../README.md)

This section provides practical code examples that demonstrate how to use Reactive Domain in real-world scenarios.

## Table of Contents

### Basic Examples
1. [Creating a New Aggregate Root](creating-aggregate-root.md)
2. [Handling Commands and Generating Events](handling-commands-events.md)
3. [Saving and Retrieving Aggregates](saving-retrieving-aggregates.md)
4. [Setting Up Event Listeners](event-listeners.md)
5. [Implementing Projections](implementing-projections.md)
6. [Handling Correlation and Causation](correlation-causation.md)
7. [Implementing Snapshots](implementing-snapshots.md)
8. [Testing Aggregates and Event Handlers](testing.md)
9. [Integration with ASP.NET Core](aspnet-integration.md)

### Real-World Domain Examples
10. [Banking Domain Example](banking-domain-example.md)
11. [E-Commerce Domain Example](ecommerce-domain-example.md)
12. [Inventory Management Example](inventory-management-example.md)
13. [Complete Sample Applications](sample-applications.md)

Each example includes:

- Complete code snippets that you can copy and use in your own projects
- Explanations of key concepts and patterns
- Best practices and common pitfalls to avoid
- Variations for different use cases

## How to Use These Examples

The examples in this section are designed to be practical and reusable. You can:

1. Copy and paste the code into your own projects
2. Use them as templates for your own implementations
3. Adapt them to your specific requirements
4. Learn from them to understand how Reactive Domain works in practice

## Prerequisites

To run these examples, you'll need:

- .NET Core 3.1 or later
- EventStoreDB (for examples that use event storage)
- Basic understanding of event sourcing and CQRS concepts

## Getting Started

If you're new to Reactive Domain, we recommend starting with the basic examples:
- [Creating a New Aggregate Root](creating-aggregate-root.md)
- [Handling Commands and Generating Events](handling-commands-events.md)
- [Saving and Retrieving Aggregates](saving-retrieving-aggregates.md)

For intermediate to advanced scenarios, explore our real-world domain examples:
- [Banking Domain Example](banking-domain-example.md) - For financial applications
- [E-Commerce Domain Example](ecommerce-domain-example.md) - For online retail systems
- [Inventory Management Example](inventory-management-example.md) - For warehouse and stock management

These real-world examples demonstrate complete implementations including:
- Command and event definitions with proper validation
- Aggregate implementations with business rules
- Command handlers with error handling
- Read model projections
- Process managers and sagas for complex workflows
- API integration examples

---

**Navigation**:
- [← Previous: Usage Patterns](../usage-patterns.md)
- [↑ Back to Top](#reactive-domain-code-examples)
- [→ Next: API Reference](../api-reference/README.md)
