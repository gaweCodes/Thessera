# Changelog

All notable changes to Thessera are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- `Result<TResult>.Success(...)` and `Result<TResult>.Failed(...)` are no longer public; call
  `Result.Success<TResult>(...)` and the new `Result.Failed<TResult>(...)` instead. This removes the
  last suppressed `CA1000` (static members on a generic type) from the package — the non-generic
  `Result` was already the documented entry point for `Success`, and now is for `Failed` too.

### Added

- A host may now select more than one persistence store: a call to `UseEfCoreStateStore` or
  `UseMartenEventStore` with a `forAggregates` list claims those aggregates for that store, leaving
  the store called without a list as the host's main store, owning every aggregate no other store
  claims. This is what lets a single host keep one aggregate event-sourced and another state-stored,
  narrowing the restriction from "one store per host" to "one store per aggregate" — see
  [ADR 0016](docs/architecture/0016-one-store-per-aggregate-not-per-host.md).
- A seventh Roslyn analyzer, `THSS0007`, in `GaWeCodes.Thessera.Analyzers`: flags a command handler
  whose constructor injects `IRepository<TAggregate, TKey>` for more than one distinct aggregate —
  the compile-time twin of the rule that a command commits through exactly one store.
- An eighth Roslyn analyzer, `THSS0008`, in `GaWeCodes.Thessera.Analyzers`: flags a command handler
  whose constructor injects an EF Core `DbContext`-derived type or a Marten `IDocumentSession`
  directly, instead of only `IRepository<TAggregate, TKey>`. Doing so lets a handler call
  `SaveChangesAsync` itself, splitting the unit of work's one commit into two and risking a change
  that is durable while its domain event is never published — the same guarantee
  `NoPublishOnFailedCommitTests` proves holds when the pipeline's own commit is the only one that
  ever runs. See [ADR 0018](docs/architecture/0018-analyzer-catches-unit-of-work-bypass.md).
- `Examples/MixedPersistence`, proving the above: one host, an event-sourced `Reading` on
  `GaWeCodes.Thessera.Persistence.Marten` (ancillary) alongside a state-stored `Account` on
  `GaWeCodes.Thessera.Persistence.EfCore.Postgres` (main), against the same PostgreSQL database.
- `Examples/MixedPersistence/MixedPersistenceWithMessaging`, next to the plain mixed-persistence
  example, adds RabbitMQ publishing on top of the same two-store host: every domain event either
  aggregate raises is mapped to an integration event and published under one shared context name,
  and tests prove a failed commit on one aggregate's store never publishes that aggregate's event
  while leaving the other aggregate's independent publishing unaffected. The two examples now live
  under `Examples/MixedPersistence/` as siblings, each with its own `.Tests` project.
- `NoPublishOnFailedCommitTests`, proving for both stores that a command whose commit fails (loses a
  concurrency race) never publishes that command's domain event — the unit of work's one-transaction
  guarantee held empirically, not just by reading `CommitAsync`.
- All `Examples/` projects now demonstrate the read-model-rebuild feature: each list query reads
  a small in-memory read model instead of the write store directly, kept in sync by an
  `IReadModelRebuilder<TAggregate, TKey>` and its store's rebuild runner
  (`StateStoredReadModelRebuildRunner<TContext>` or `EventSourcedReadModelRebuildRunner`), rebuilt
  once at startup, again after every successful mutation, and on demand from the console menu. The
  two `MixedPersistence` examples rebuild one read model per aggregate, using each aggregate's own
  runner in the same host.

### Fixed

- Two persistence adapters registered on the same host no longer conflict at startup. Both adapters
  called `DeadLetterHealthCheckRegistration.Register` unconditionally, registering the
  `thessera-dead-letters` health check twice; the second call is now a no-op once the check is
  already registered. Separately, `GaWeCodes.Thessera.Persistence.EfCore`'s outbox wiring and
  `GaWeCodes.Thessera.Persistence.Marten`'s both unconditionally claimed Wolverine's single Main
  message-store slot; an EF Core store now self-selects Main or Ancillary depending on whether
  Marten (which cannot be anything but Main) is also present, enrolling itself against its own
  write context and a derived schema when it is not — see
  [ADR 0017](docs/architecture/0017-wolverine-main-ancillary-message-store.md). Both bugs were only
  reachable once a host actually selected two persistence stores at once, which
  `Examples/MixedPersistence` is the first test in this repository to do.

## [1.0.0-preview.3] - 2026-09-02

### Added

- A new package, `GaWeCodes.Thessera.Analyzers`: six Roslyn analyzers (`THSS0001`–`THSS0006`) that
  catch a missing `[AggregateName]`, a missing `[EventName]`, an aggregate whose parameterless
  constructor is absent or public, a child entity with a public constructor, and an aggregate- or
  entity-state that names the wrong type as itself at build time — the compile-time twin of checks
  `GaWeCodes.Thessera.Testing` and the runtime otherwise perform only in a test or at host startup.
  Ships no runtime code; reference it from every host regardless of store choice.
- A second `.github/dependabot.yml` entry for `Examples/`, so the third-party package pins there
  (Entity Framework Core, Marten, `Microsoft.Extensions.Hosting`, RabbitMQ.Client) are kept current
  the same way the main solution's are, instead of drifting unnoticed because no CI workflow ever
  builds `Examples.slnx`. The `GaWeCodes.Thessera.*` family is excluded from it: its version is
  centralized instead (see below), not tracked by Dependabot.

### Changed

- Every `Examples/*.csproj` now pins its `GaWeCodes.Thessera.*` package references to a single
  `$(ThesseraVersion)` property in `Examples/Directory.Build.props`, instead of repeating the same
  version string in each project file. `Examples/README.md` documents the one-line refresh after a local
  `dotnet pack`.

### Fixed

- Stale third-party lower bounds in `Examples/StateStored`, `EventSourced`,
  `StateStoredWithMessaging` and `EventSourcedWithMessaging`: `Microsoft.EntityFrameworkCore` and
  `Microsoft.Extensions.Hosting` still pinned `10.0.10`, and `Marten` still pinned `9.22.5`, below
  what the packed `GaWeCodes.Thessera.*` packages now require after the `1.0.0-preview.2` dependency
  bump. A clean restore against a freshly packed local feed failed with `NU1605` until these were
  raised to match `Directory.Packages.props`.

## [1.0.0-preview.2] - 2026-09-02

No public API changed in this release: every `PublicAPI.Shipped.txt` is untouched. What changed is
what ships alongside it — the documentation inside the packages, the messages a misconfigured host
prints at startup, and the lower bounds of the dependencies the packages declare.

### Added

- A `docs/` directory for maintainer documentation: a glossary that fixes the vocabulary shared by
  the package READMEs, the error messages and the XML documentation; architecture decision records
  for the decisions behind the design and behind the way the repository is kept honest, which until
  now were stated as fact or as bare rules with no reasoning attached; and a cross-package overview
  of how the packages stack and how a command travels from `ISender` to the broker, which no
  single package README could own.
- XML documentation on the public surface of all packages, so that IntelliSense answers what a
  member does, what its arguments have to satisfy and what it throws — including the constraints
  that previously lived only in the READMEs, such as which key value types a stream key accepts and
  why the others are refused. `CS1591` is no longer relaxed anywhere, so an undocumented public
  member is a build error and the documentation cannot fall behind the code.
- `Persistence.Marten`'s README now documents what its startup checks verify (aggregate-style match,
  schema-provisioning conditions) and lists the failures it returns as a `Result` instead of an
  exception, matching the equivalent sections already present in the state-store README.

### Changed

- The lower bound of several declared dependencies moved up: Entity Framework Core and the
  `Microsoft.Extensions.*` packages to 10.0.11, and Wolverine to 6.32.0. The upper bounds are
  unchanged, so a consuming host that already resolves a newer patch is unaffected.
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

[Unreleased]: https://github.com/GaWeCodes/Thessera/compare/v1.0.0-preview.3...main
[1.0.0-preview.3]: https://github.com/GaWeCodes/Thessera/compare/v1.0.0-preview.2...v1.0.0-preview.3
[1.0.0-preview.2]: https://github.com/GaWeCodes/Thessera/compare/v1.0.0-preview.1...v1.0.0-preview.2
[1.0.0-preview.1]: https://github.com/GaWeCodes/Thessera/commits/v1.0.0-preview.1
