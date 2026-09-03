# GaWeCodes.Thessera.Persistence.EfCore

The database-agnostic half of Thessera's state-stored persistence: the state reconciliation, the
repository, the aggregate tracker, the unit of work, the typed-key value converters, the model
check and the read-model rebuild runner — everything an EF Core state store needs *except* the
database. It **defines** `IEfCoreDatabaseDriver`; it does not implement one. This is not a store
choice by itself: the family's two choices are
`GaWeCodes.Thessera.Persistence.EfCore.Postgres` (state) and `GaWeCodes.Thessera.Persistence.Marten`
(event stream), and the first of those is this package plus a PostgreSQL driver.

**Why not just Wolverine?** Wolverine's EF Core integration gives you the transactional outbox, and
this package builds on it by name. What it adds is the part EF Core and Wolverine both leave to you:
reconciling an **immutable** aggregate state — a record that is replaced whole on every event, with
children and grandchildren — back into EF Core's change tracker, so that a save writes what the
aggregate actually holds. Get that wrong and EF Core reports success while storing the old values,
which is exactly the failure this code exists to prevent.

## When you need this package

- You want EF Core state storage on a database Thessera does not ship a driver for. Implement
  `IEfCoreDatabaseDriver` and you have a store.
- You reference `modelBuilder.ApplyEntityKeyConversions()` or the read-model rebuild runner
  explicitly and want the `using` to name the package it comes from.

## When you don't

- You are on PostgreSQL. Install `GaWeCodes.Thessera.Persistence.EfCore.Postgres`, which brings this
  package and the driver.
- You want event sourcing. That is `GaWeCodes.Thessera.Persistence.Marten`.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Persistence.EfCore
```

Requires .NET 10 (`net10.0`). Brings `GaWeCodes.Thessera.Core`, `GaWeCodes.Thessera.Wolverine`,
`Microsoft.EntityFrameworkCore` and `WolverineFx.EntityFrameworkCore`. It contains no database
provider.

## Writing a driver

A driver is four members. Everything else — repository, tracker, unit of work, reconciliation,
rebuild — is already written and database-neutral.

```csharp
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using Wolverine.Persistence.Durability;

internal sealed class SqlServerDatabaseDriver : IEfCoreDatabaseDriver
{
    public static SqlServerDatabaseDriver Instance { get; } = new();

    public void ConfigureContext(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlServer(connectionString);

    public void PersistMessages(WolverineOptions options, string connectionString, MessageStoreRole role, Type? enrollContextType)
    {
        if (role == MessageStoreRole.Main)
        {
            options.PersistMessagesWithSqlServer(connectionString);
            return;
        }

        // Ancillary: another store already claimed Main, so this one needs its own schema and must
        // be enrolled against the write context whose messages belong to it.
        options.PersistMessagesWithSqlServer(connectionString, "wolverine_" + enrollContextType!.Name.ToLowerInvariant(), MessageStoreRole.Ancillary)
            .Enroll(enrollContextType);
    }

    public bool IsTransientFault(Exception exception) => exception is SqlException { IsTransient: true };

    public IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators { get; } = [new SqlServerFaultTranslator()];
}
```

Then give consumers the same shape of entry point the PostgreSQL package has:

```csharp
public static ThesseraOptions UseEfCoreSqlServerStateStore<TContext>(
    this ThesseraOptions options,
    string connectionString,
    Action<DbContextOptionsBuilder>? configureContext = null)
    where TContext : DbContext =>
    options.UsePersistence(
        new EfCorePersistenceAdapter<TContext>(SqlServerDatabaseDriver.Instance, connectionString, configureContext));
```

Keep the vendor in the method name. Two drivers offering `UseStateStore` with the same signature
would give a host that references both a `CS0121` it cannot resolve.

`PersistMessages` is where the transactional outbox is bound to your database — which is also why a
driver ends up referencing WolverineFx through `GaWeCodes.Thessera.Wolverine`. An outbox has to know
the message engine; that is a fact about outboxes, not a leak in this seam.

## Mapping aggregates

Whatever the driver, an aggregate state is mapped as a normal EF Core entity, its child collections
as owned types, its `Version` as the concurrency token, and its typed keys by one call at the end of
`OnModelCreating`:

```csharp
modelBuilder.ApplyEntityKeyConversions();
```

That converts every `IEntityKey<TValue>` property to its bare value, so a column holds a `uuid` or a
`bigint` rather than a serialized object.

At startup the model is checked: every aggregate state has to be mapped and bound to itself, and the
write `DbContext` has to be scoped. Both failures are otherwise silent until the first save.

## Limits

- **`net10.0` only.** No multi-targeting.
- **State-stored, mostly.** Nearly every file here sits under `StateStored/`, but the typed-key
  converters are style-neutral and useful on a read context of an event-sourced service too.
- **Not trim-safe and not AOT-safe.** The adapter reflects over aggregate state, typed keys and
  child collections to build the model and to rehydrate an aggregate; EF Core itself is not fully
  trim-compatible either. Publish without `PublishTrimmed` and without `PublishAot`.
- EF Core is pinned below its next major version on purpose: the state reconciliation reads EF Core
  **metadata** APIs, which are not a stable application-level contract, so a major upgrade is not
  taken sight-unseen.

## The family

Our packages. Exactly two of them are a choice you make; the rest follow from it.

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
- `GaWeCodes.Thessera.Analyzers` — the compile-time twin of eight of those conventions, in every host.

## License

MIT. Source, issues and the full documentation: <https://github.com/GaWeCodes/Thessera>.
