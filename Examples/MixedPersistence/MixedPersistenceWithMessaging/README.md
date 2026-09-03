# MixedPersistenceWithMessaging

This example takes [MixedPersistence](../MixedPersistence/README.md) — one host, two aggregates,
two stores — and adds the same RabbitMQ publishing layer the messaging tiers use. `Reading` still
commits through Marten (ancillary), `Account` still commits through EF Core Postgres (main store),
and every domain event either aggregate raises is mapped to an integration event and published
under the shared context name `mixed-persistence`, so a subscriber sees one coherent stream —
`mixed-persistence.account-opened`, `mixed-persistence.reading-created`, and so on — regardless of
which store persisted the aggregate that raised the event.

The point this example proves that `MixedPersistence` alone does not: publishing is per-transaction,
not per-host. Each command still commits through exactly one store, and Wolverine's outbox flushes
only the events written by *that* commit. If `Account`'s EF Core transaction fails, no
`Account`-side integration event is published — `Reading`'s Marten-backed publishing pipeline is
entirely unaffected, and vice versa. There is no cross-store, cross-aggregate transaction to fail
together in the first place.

A background `ReceivedEventsPollingService` binds a temporary queue to `mixed-persistence.*` and
appends every delivered payload to `received-events.log` in the working directory, so both the
console output (what was sent) and the log file (what the broker actually redelivered) can be
inspected after a run.

Like plain [MixedPersistence](../MixedPersistence/README.md), both list queries read a dedicated
in-memory read model instead of their write store, kept in sync by `IReadModelRebuilder<Account, AccountId>`
/ `StateStoredReadModelRebuildRunner<AccountDbContext>` for accounts and `IReadModelRebuilder<Reading, ReadingId>`
/ `EventSourcedReadModelRebuildRunner` for readings. `RebuildReadModelsAsync` runs both runners
together — once at startup, again after every successful mutation of either aggregate (independently
of whether its integration event round-trip through RabbitMQ succeeded), and on demand via menu
option 10.

Each command handler still resolves `IRepository<,>` for exactly one aggregate — `THSS0007` still
fails the build the moment a handler asks for both — and no handler here calls `SaveChangesAsync`
or `IDocumentSession` directly either — `THSS0008` fails the build the moment a handler injects the
raw `DbContext`/`IDocumentSession` instead of going through the unit of work `IRepository<,>` wraps.

Prerequisites: refresh `C:\temp\thessera-local-feed`, have PostgreSQL and RabbitMQ available, and
optionally set `THESSERA_EXAMPLE_POSTGRES` plus `THESSERA_EXAMPLE_RABBITMQ`. Run with
`dotnet run --project Examples\MixedPersistence\MixedPersistenceWithMessaging` and test with
`dotnet test Examples\MixedPersistence\MixedPersistenceWithMessaging.Tests`.

See [ADR 0016](../../../docs/architecture/0016-one-store-per-aggregate-not-per-host.md) and
[ADR 0017](../../../docs/architecture/0017-wolverine-main-ancillary-message-store.md) for why mixing
stores on one host is possible at all and how Wolverine's own main/ancillary message-store split
interacts with it, and [ADR 0018](../../../docs/architecture/0018-analyzer-catches-unit-of-work-bypass.md)
for the `THSS0008` rule this example's handlers are checked against.
