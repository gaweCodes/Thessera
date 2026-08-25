# Changelog

All notable changes to Thessera are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial implementation of the Thessera package family.
- Domain abstractions for aggregates, entities, domain events, typed keys, and business rules.
- CQRS abstractions and integration-event contracts.
- `Result` and `Failure` types for application-level outcomes.
- Application composition and dispatching.
- Projection support and startup validation.
- Wolverine-based messaging runtime with outbox support.
- Entity Framework Core persistence support.
- Marten-based event-sourced persistence.
- PostgreSQL support and database-specific error translation.
- Optional RabbitMQ transport for integration events.
- Testing utilities and convention checks.
- XML documentation, symbol packages (`snupkg`), and SourceLink for all packages.

### Changed

- The package family uses a shared version across all Thessera packages.
- The domain model is designed to run with either Entity Framework Core or Marten persistence.

### Security

- No `InternalsVisibleTo` is used by any package.

[Unreleased]: https://github.com/GaWeCodes/Thessera/commits/main
