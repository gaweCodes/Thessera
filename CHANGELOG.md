# Changelog

All notable changes to Thessera are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0-preview.2] - 2026-08-31

No public API changed in this release: every `PublicAPI.Shipped.txt` is untouched. What changed is
what ships alongside it — the documentation inside the packages, and the messages a misconfigured
host prints at startup.

### Added

- A `docs/` directory for maintainer documentation: a glossary that fixes the vocabulary shared by
  the package READMEs, the error messages and the XML documentation; architecture decision records
  for the decisions behind the design and behind the way the repository is kept honest, which until
  now were stated as fact or as bare rules with no reasoning attached; and a cross-package overview
  of how the ten packages stack and how a command travels from `ISender` to the broker, which no
  single package README could own.
- XML documentation on the public surface of all ten packages, so that IntelliSense answers what a
  member does, what its arguments have to satisfy and what it throws — including the constraints
  that previously lived only in the READMEs, such as which key value types a stream key accepts and
  why the others are refused. `CS1591` is no longer relaxed anywhere, so an undocumented public
  member is a build error and the documentation cannot fall behind the code.
- `Persistence.Marten`'s README now documents what its startup checks verify (aggregate-style match,
  schema-provisioning conditions) and lists the failures it returns as a `Result` instead of an
  exception, matching the equivalent sections already present in the state-store README.

### Changed

- The package READMEs now describe dependency pins and file counts rather than quoting them, so
  they cannot go stale on the next version bump.
- One wording for one idea across the family: the switch between the two store choices is described
  as "state store and event store" everywhere, instead of pairing a component with a data shape.
  The identity that addresses a stream is called the stream key throughout.

### Fixed

- Corrupted em dashes in the startup-check error messages of `Core`, `Wolverine` and
  `Messaging.RabbitMq`, which reached operators as mojibake.
- A stale product name in the Wolverine runtime check, which spoke of "Building Block" capabilities.
- Several factual errors in the package READMEs: outdated dependency version ranges, outdated file
  and line counts, a claim that the runtime verifies aggregate conventions at startup when most of
  them are enforced elsewhere or not at all, a claim that an event-sourced aggregate on a state
  store is only a warning when it is refused, and a reference to a SQL Server adapter that does not
  exist.
- A claim in `Wolverine`'s README that its seven-day idempotency window applies regardless of
  configuration, when only the retry policy does; the idempotency window runs only once a store is
  selected.
- A claim in `Messaging.RabbitMq`'s README that leaving the package out means no dead-letter
  assumptions. The dead-letter health check is registered by whichever store is selected, not by the
  transport, so it keeps running with or without this package.
- A claim in `Testing`'s XML documentation that the runtime always checks an aggregate's
  parameterless constructor at startup. That check runs only for a host that selected a persistence
  strategy; a host on `UseNoPersistence()` never gets it.
- The entry below claiming XML documentation for `1.0.0-preview.1`. That release generated
  documentation files but left them empty, so the claim was untrue; it has been corrected in place
  and the documentation itself arrives with this release.

## [1.0.0-preview.1] - 2026-08-28

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
- Symbol packages (`snupkg`) and SourceLink for all packages.

### Changed

- The package family uses a shared version across all Thessera packages.
- The domain model is designed to run with either Entity Framework Core or Marten persistence.

### Security

- No `InternalsVisibleTo` is used by any package.

[Unreleased]: https://github.com/GaWeCodes/Thessera/compare/v1.0.0-preview.2...main
[1.0.0-preview.2]: https://github.com/GaWeCodes/Thessera/compare/v1.0.0-preview.1...v1.0.0-preview.2
[1.0.0-preview.1]: https://github.com/GaWeCodes/Thessera/commits/v1.0.0-preview.1
