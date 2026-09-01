# GaWeCodes.Thessera.Persistence.EfCore.Postgres

Stores Thessera aggregates as **state** in PostgreSQL, through EF Core, with the domain events going
into a transactional outbox in the same commit. This is **one of the family's two store choices**;
the other is `GaWeCodes.Thessera.Persistence.Marten`, which stores the same aggregates as an event
stream. Pick exactly one per service — the package exposes a single entry point,
`UseEfCoreStateStore<TContext>(connectionString)`, and brings the rest of the family with it.

**Why not just Wolverine?** Wolverine already gives you the outbox and the EF Core integration, and
this package uses both — openly, by name, in its dependency list. What it adds is the half Wolverine
has no opinion about: an aggregate whose state is an immutable record, reconciled into EF Core's
change tracker across children and grandchildren; typed identifiers mapped to bare column values; an
optimistic-concurrency version; and domain events that leave under stable, persisted names. And the
model stays portable: an aggregate written against `EventSourcedAggregateRoot` runs on *this* store
as well — add `WithoutEventHistory()` and you keep the state without the stream — so moving a
context between the two store choices is a change of wiring, not a rewrite of the domain.

## When you need this package

- Your bounded context keeps the **current state** of its aggregates in PostgreSQL and you want
  EF Core for it.
- You want domain events published reliably, in the same transaction that writes the state.
- You want the read-model rebuild runner for state-stored contexts.

## When you don't

- Your context should keep the **history**, not just the state. Use
  `GaWeCodes.Thessera.Persistence.Marten` instead. Never both in one host: a bounded context has one
  write database, and a commit cannot span two.
- You need EF Core on a database other than PostgreSQL. Reference
  `GaWeCodes.Thessera.Persistence.EfCore` and implement `IEfCoreDatabaseDriver` — the whole
  Postgres-specific part of this package is a driver and the extension method that selects it.
- You want no persistence at all. `GaWeCodes.Thessera.Core` with `UseNoPersistence()` covers it.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Persistence.EfCore.Postgres
```

Requires .NET 10 (`net10.0`) and PostgreSQL. Brings `GaWeCodes.Thessera.Persistence.EfCore`,
`GaWeCodes.Thessera.Npgsql`, `GaWeCodes.Thessera.Core`, `GaWeCodes.Thessera.Wolverine`,
`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` and `WolverineFx.Postgresql`.

## Getting started

### 1. Map the aggregate state, not the aggregate

EF Core sees the state record. Call `ApplyEntityKeyConversions()` last, so typed identifiers persist
as their bare value, and map `Version` as the concurrency token.

```csharp
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

public sealed class ReadingWriteDbContext(DbContextOptions<ReadingWriteDbContext> options) : DbContext(options)
{
    public DbSet<ReadingState> Readings => Set<ReadingState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReadingState>(entity =>
        {
            entity.ToTable("readings");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Value).HasColumnName("value");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            // Child collections are owned types, keyed by the child's own typed key.
            entity.OwnsMany(state => state.Samples, samples =>
            {
                samples.ToTable("reading_samples");
                samples.WithOwner().HasForeignKey("reading_id");
                samples.HasKey(sample => sample.Id);
            });
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
```

### 2. Select the store

```csharp
using GaWeCodes.Thessera;

builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(RecordReading).Assembly);
    options.AddDomainEventsFrom(typeof(Reading).Assembly);

    options.UseEfCoreStateStore<ReadingWriteDbContext>(writeConnectionString);
});
```

That one call registers the `DbContext` with Wolverine's EF Core integration, the repository, the
aggregate tracker, the unit of work, the Postgres fault translators, the outbox durability, the
read-model rebuild runner and a dead-letter health check. There is an optional third parameter,
`Action<DbContextOptionsBuilder>`, if you need to configure the context further.

Your handlers now resolve `IRepository<Reading, ReadingId>` and never call `SaveChanges`: the unit
of work commits once per command, and the domain events land in the outbox in that same
transaction.

### 3. Create the schema

The package does not run migrations for you. Use EF Core migrations for your own tables and let
Wolverine build its message storage — say `ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)`
in the migration job only, and leave services on the default `Never`.

### 4. Rebuild a read model when a projection changes

```csharp
using GaWeCodes.Thessera.Persistence.EfCore.ReadModels;

await serviceProvider
    .GetRequiredService<StateStoredReadModelRebuildRunner<ReadingWriteDbContext>>()
    .RebuildAsync<Reading, ReadingId, ReadingState>(cancellationToken);
```

The runner is registered for you. It reads the stored state in batches, rehydrates each aggregate,
and hands it to your `IReadModelRebuilder<Reading, ReadingId>` — which you register. A state store
has no history to replay, so a rebuild reconstructs the read model from the current state.

## What it checks at startup

- The chosen **aggregate style matches the store**. An aggregate derived from
  `EventSourcedAggregateRoot` on a state store is refused by default: the state and the version are
  written correctly and the outbox is fed, but no stream is kept, and that loss is silent and
  permanent. If you mean it — a spike, or a migration you have planned — say
  `WithoutEventHistory()`.
- Every aggregate state is **mapped and self-bound** in the model.
- The write `DbContext` is registered with a **scoped** lifetime.

## Failures you get back as `Result`, not exceptions

A unique-constraint violation becomes `Failure.Conflict` with the code from
`PersistenceFailureCodes`, naming the constraint. A concurrency conflict becomes a conflict failure
too. Transient Npgsql faults are retried with a cooldown before anything reaches the error queue.

## Limits

- **`net10.0` only.** No multi-targeting.
- **PostgreSQL only.** For another database, implement `IEfCoreDatabaseDriver` in
  `GaWeCodes.Thessera.Persistence.EfCore`.
- **Not trim-safe and not AOT-safe.** The adapter reflects over aggregate state, typed keys and
  child collections to build the model and to rehydrate an aggregate; EF Core itself is not fully
  trim-compatible either. Publish without `PublishTrimmed` and without `PublishAot`.
- EF Core is pinned below its next major version on purpose: the state reconciliation reads EF Core
  **metadata** APIs, which are not a stable application-level contract, so a major upgrade is not
  taken sight-unseen.

## The family

Ten packages. Exactly two of them are a choice you make; the rest follow from it.

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

## License

MIT. Source, issues and the full documentation: <https://github.com/GaWeCodes/Thessera>.
