# Todo List for PR 169 - Reactive Documentation

This todo list contains items that need to be addressed for PR 169, which adds comprehensive documentation for the Reactive Domain library.

## Badge and Reference Fixes

- [x] Fix Badge URLs in README.md to point to the main ReactiveDomain repository instead of leopoldodonnell's fork (already completed)
- [x] Update Travis CI badge to point to ReactiveDomain organization (already completed)
- [x] Ensure all documentation links point to the correct repositories (updated links in README.md, sample-applications.md, and workshop-materials.md)

## Documentation for Key Components

- [x] Add/enhance documentation for ReadModelBase (documentation is comprehensive with good examples)
- [x] Add/enhance documentation for MessageBuilder factory (documentation is comprehensive)
- [x] Improve documentation for Command and Event classes that implement ICorrelatedMessage (documentation exists and is detailed)
- [x] Document the relationship between different components (added new Key Component Relationships section to architecture.md and cross-references in component documentation)

## Code Example Corrections

- [x] Fix the use of `Apply()` vs `RaiseEvent()` methods in aggregates (Apply methods are for rehydration, not for creating new events)
- [x] Update examples in event.md to use `RaiseEvent(new AccountCreated(...))` instead of `Apply(...)`
- [x] Review all code examples for technical accuracy in command.md and message-builder.md
- [ ] Add more real-world examples to illustrate concepts

## Architecture Documentation Improvements

- [x] Add more detailed explanations of the CQRS pattern (added comprehensive section with core principles and benefits)
- [x] Enhance documentation of Event Sourcing principles (expanded with detailed explanations and flow diagrams)
- [x] Include diagrams showing the flow of commands and events (added multiple mermaid diagrams)
- [x] Document the relationship between different architectural components (added new section on CQRS and Event Sourcing integration)

## Terminology and Consistency

- [ ] Ensure consistent use of terminology throughout the documentation
- [ ] Review and correct any technical inaccuracies
- [ ] Standardize formatting and style across all documentation files

## API Reference Enhancements

- [ ] Add missing classes and interfaces to API reference
- [ ] Ensure all public APIs are properly documented
- [ ] Add parameter descriptions for important methods
- [ ] Document return values and exceptions

## Navigation and Structure

- [ ] Ensure logical progression through documentation
- [ ] Fix any broken links between documentation pages
- [ ] Improve component navigation to show relationships
- [ ] Verify that the table of contents is accurate and complete

## Learning Resources

- [ ] Enhance the learning path for new users
- [ ] Add links to additional resources and examples
- [ ] Include troubleshooting section for common issues
