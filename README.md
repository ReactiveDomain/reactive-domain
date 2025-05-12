[![Build status](https://ci.appveyor.com/api/projects/status/oir89k5nyyouqtsm?svg=true)](https://ci.appveyor.com/project/jageall/reactive-domain)
[![Build Status](https://travis-ci.org/ReactiveDomain/reactive-domain.svg?branch=master)](https://travis-ci.org/ReactiveDomain/reactive-domain)
[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg)](code_of_conduct.md)
[![Documentation](https://img.shields.io/badge/docs-latest-brightgreen.svg)](https://reactivedomain.github.io/reactive-domain/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/ReactiveDomain.svg)](https://www.nuget.org/packages/ReactiveDomain/)
[![GitHub stars](https://img.shields.io/github/stars/ReactiveDomain/reactive-domain.svg)](https://github.com/ReactiveDomain/reactive-domain/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/ReactiveDomain/reactive-domain.svg)](https://github.com/ReactiveDomain/reactive-domain/issues)

# Reactive Domain

## Overview

Reactive Domain is an open source framework for implementing event sourcing in .NET projects using reactive programming principles. This includes interfaces for using [EventStoreDB](https://eventstore.com). It also provides a messaging framework and other tools for using CQRS.

The framework is highly opinionated. It focuses on using a small number of consistent patterns and design principles in its public interfaces to enable developers to get up to speed quickly. Ease of use and "design for code review" have been the driving forces behind the framework's evolution. Where trade-offs have been necessary, these principles have been emphasized over performance.

## Documentation

Comprehensive documentation for Reactive Domain is available in the [docs](./docs) directory. The documentation includes:

- [Introduction and Overview](./docs/README.md) - Get started with Reactive Domain
- [Core Concepts](./docs/core-concepts.md) - Learn about event sourcing fundamentals
- [Usage Patterns](./docs/usage-patterns.md) - Discover common patterns and best practices
- [API Reference](./docs/api-reference/README.md) - Explore the API documentation
- [Troubleshooting Guide](./docs/troubleshooting.md) - Solve common issues
- [Architecture Guide](./docs/architecture.md) - Understand the system architecture
- [Migration Guide](./docs/migration.md) - Migrate between versions
- [Security Guide](./docs/security.md) - Implement security best practices
- [Performance Optimization](./docs/performance.md) - Optimize your application

For developers new to Reactive Domain, we recommend starting with the [Introduction](./docs/README.md) followed by the [Core Concepts](./docs/core-concepts.md) guide.

## Contributing

Pull requests are welcome! Take a look at the open issues, join our [discussion on Slack](https://reactivedomain.slack.com), or contribute in an area where you see a need. Contributors and participants on our Slack channels are expected to abide by the project's [code of conduct](CODE_OF_CONDUCT.md). Read the full guidelines on [contributing](CONTRIBUTING.md).