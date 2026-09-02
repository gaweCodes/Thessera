# GaWeCodes.Thessera.Npgsql

The PostgreSQL error knowledge both Thessera stores share: which Npgsql exception is transient and
worth retrying, and how a unique-constraint violation becomes a `Failure.Conflict` instead of an
exception escaping the handler. Two public types, one dependency — `Npgsql`. It is **not a store
choice**: the family's two choices are `GaWeCodes.Thessera.Persistence.EfCore.Postgres` (state) and
`GaWeCodes.Thessera.Persistence.Marten` (event stream), and **both already contain this package**.

**Why not just Wolverine?** Wolverine will retry a failed message for you, but it has no opinion on
which PostgreSQL error deserves a retry and which one is a business answer your API should return.
This package is that opinion, written once so the EF Core store and the Marten store cannot drift
apart on it — the name cuts across the two choices rather than standing beside them.

## When you need this package

Almost never directly. You need it when you write your own PostgreSQL-backed persistence adapter and
want the same fault translation the shipped stores use — for example an `IEfCoreDatabaseDriver` for
a second PostgreSQL configuration, or an adapter for a store Thessera does not cover.

## When you don't

- You use either shipped store. Both reference this package; naming it again adds nothing.
- You are not on PostgreSQL. There is nothing here for you; implement `IPersistenceFaultTranslator`
  for your own driver instead.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Npgsql
```

Requires .NET 10 (`net10.0`) and PostgreSQL. Brings `GaWeCodes.Thessera.Core` and `Npgsql`.

## Getting started

```csharp
using GaWeCodes.Thessera.Npgsql;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class MyPostgresDriver : IEfCoreDatabaseDriver
{
    public IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators { get; } = [new PostgresFaultTranslator()];

    public bool IsTransientFault(Exception exception) => PostgresTransientFaults.IsTransient(exception);

    // ... ConfigureContext, PersistMessages ...
}
```

- `PostgresFaultTranslator` turns a `PostgresException` with SQL state `23505` into
  `Failure.Conflict(PersistenceFailureCodes.UniqueViolation, ...)`, naming the violated constraint
  when the driver reports one. The command comes back as a failed `Result`, not as a thrown
  exception, so the caller can map it to a status code.
- `PostgresTransientFaults.IsTransient(exception)` answers whether Npgsql considers the fault
  transient. The runtime uses that answer to decide between a retry with cooldown and the error
  queue.

Register a translator with the runtime by returning it from `IEfCoreDatabaseDriver.FaultTranslators`,
or directly as an enumerable `IPersistenceFaultTranslator` singleton in your adapter's `Register`.

## Limits

- **`net10.0` only.** No multi-targeting.
- Two error cases are covered — unique violation and transient faults. Everything else falls through
  to the next translator and, failing that, to the runtime's generic handling.
- Npgsql is pinned below its next major version.
- The family is **not trim-safe and not AOT-safe**. Publish without `PublishTrimmed` and without
  `PublishAot`.

## The family

Eleven packages. Exactly two of them are a choice you make; the rest follow from it.

- `GaWeCodes.Thessera.Domain` — aggregates, entities, domain events, typed keys, rules. BCL only.
- `GaWeCodes.Thessera.Application` — CQRS, persistence and integration-event contracts,
  `Result`/`Failure`. Contracts only, no runtime.
- `GaWeCodes.Thessera.Core` — composition root, dispatcher, projections, startup checks. No Wolverine.
- `GaWeCodes.Thessera.Wolverine` — the runtime that owns the outbox. Arrives with either store.
- `GaWeCodes.Thessera.Persistence.EfCore.Postgres` — **store choice 1**: aggregates as state in PostgreSQL.
- `GaWeCodes.Thessera.Persistence.Marten` — **store choice 2**: aggregates as an event stream in PostgreSQL.
- `GaWeCodes.Thessera.Persistence.EfCore` — the database-agnostic half of choice 1; write your own driver here.
- `GaWeCodes.Thessera.Npgsql` — PostgreSQL error translation, shared by both choices.
- `GaWeCodes.Thessera.Messaging.RabbitMq` — opt-in transport. Without one, no integration event leaves the service.
- `GaWeCodes.Thessera.Testing` — convention checks and test helpers for all of the above.
- `GaWeCodes.Thessera.Analyzers` — the compile-time twin of six of those conventions, in every host.

## License

MIT. Source, issues and the full documentation: <https://github.com/GaWeCodes/Thessera>.
