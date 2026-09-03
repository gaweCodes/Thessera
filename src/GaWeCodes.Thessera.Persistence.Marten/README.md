# GaWeCodes.Thessera.Persistence.Marten

Stores Thessera aggregates as an **event stream** in PostgreSQL, through
[Marten](https://martendb.io), with the domain events going into a transactional outbox in the same
commit. This is **one of the family's two store choices**; the other is
`GaWeCodes.Thessera.Persistence.EfCore.Postgres`, which stores the same aggregates as current state.
A host picks one store per *aggregate*, not per host — this package exposes a single entry point,
`UseMartenEventStore(connectionString, forAggregates)`, and brings the rest of the family with it.

**Why not just Wolverine?** Wolverine already integrates with Marten and gives you the outbox, and
this package uses both — openly, by name, in its dependency list. What it adds is the half neither
of them has an opinion about: an aggregate with business rules and typed identifiers, whose events
are persisted under names *you* declared rather than under CLR type names, and whose stream key is a
pinned wire format. And the model stays portable: the very same aggregate class runs on the
state-stored choice too, once that host says `WithoutEventHistory()` — repository, rules and events
untouched. That switch is the one thing no one else hands you.

## When you need this package

- Your bounded context should keep the **history**: the sequence of events is the record, and the
  current state is derived from it.
- You want to rebuild read models by replaying what actually happened.
- You want the events published reliably, in the same transaction that appends them.

## When you don't

- Your context only needs the **current state**. Use
  `GaWeCodes.Thessera.Persistence.EfCore.Postgres` instead. This can run alongside this package in
  the same host for a *different* aggregate — see "Mixing this store with the state store" below —
  but never for the same aggregate: a commit cannot span two stores.
- You want no persistence at all. `GaWeCodes.Thessera.Core` with `UseNoPersistence()` covers it.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Persistence.Marten
```

Requires .NET 10 (`net10.0`) and PostgreSQL. Brings `GaWeCodes.Thessera.Core`,
`GaWeCodes.Thessera.Wolverine`, `GaWeCodes.Thessera.Npgsql`, `Marten` and `WolverineFx.Marten`.

## Getting started

### 1. Derive the aggregate from `EventSourcedAggregateRoot`

Everything else about the domain model is unchanged — the same state record, the same `Apply`, the
same rules. The base class adds replay from history and nothing else.

```csharp
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Naming;

[AggregateName("reading")]
public sealed class Reading : EventSourcedAggregateRoot<ReadingId, ReadingState>
{
    private Reading() : base(ReadingState.Empty)
    {
    }

    public static Reading Record(ReadingId id, int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        var reading = new Reading();
        reading.RaiseEvent(new ReadingRecorded(id, value));
        return reading;
    }
}
```

### 2. Select the store

```csharp
using GaWeCodes.Thessera;

builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(RecordReading).Assembly);
    options.AddDomainEventsFrom(typeof(Reading).Assembly);   // supplies the persisted event names

    options.UseMartenEventStore(writeConnectionString);
});
```

That one call configures Marten with string stream identity, registers the repository, the aggregate
tracker, the unit of work, the Marten and Postgres fault translators, the outbox durability, the
read-model rebuild runner and a dead-letter health check. `AddDomainEventsFrom` is not optional
here: every `[EventName]` in those assemblies is registered as Marten's event type name. There is a
trailing `params Type[] forAggregates`: leave it empty to make this the host's **main store**, owning
every aggregate no other selected store claims — the case above. Name one or more aggregate types to
make this an **ancillary** store next to another one already selected on the same host (typically
`UseEfCoreStateStore`), so an event-sourced aggregate and a state-stored aggregate run side by side —
see "Mixing this store with the state store" below.

Your handlers now resolve `IRepository<Reading, ReadingId>` and never touch a session: loading
fetches the stream and replays it, and the unit of work appends and commits once per command, with
the domain events landing in the outbox in the same transaction.

### 3. Create the schema

Say `ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)` in a migration job — that
applies all configured Marten changes to the database at startup. Services stay on the default
`Never`, so a running service never alters schema.

### 4. Rebuild a read model when a projection changes

```csharp
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;

await serviceProvider
    .GetRequiredService<EventSourcedReadModelRebuildRunner>()
    .RebuildAsync<Reading, ReadingId>(cancellationToken);
```

The runner is registered for you. It walks the streams of that aggregate, replays each one, and
hands the rebuilt aggregate to your `IReadModelRebuilder<Reading, ReadingId>` — which you register.

## The stream key is a wire format

A stream is keyed `<aggregate-name>/<key-value>`: the name from `[AggregateName]`, the separator
`/`, and the identity rendered by a pinned format — `Guid` as `D`, `string` verbatim (and rejected
if it contains `/`), `int`/`long` as invariant decimals and never negative. Any other key value type
is refused outright, because a `decimal`, an `enum` or a `DateTime` renders differently the day a
convention changes and makes every existing stream unreachable.

The same holds for `[EventName]`: it is the event type name in the database. Renaming the C# type is
free; changing the attribute value orphans every event already written under the old one.

## Mixing this store with the state store

A host is no longer one store — it is one store *per aggregate*. Claim each aggregate on the store
that fits it:

```csharp
builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(RecordReading).Assembly);
    options.AddDomainEventsFrom(typeof(Reading).Assembly);

    options.UseEfCoreStateStore<BillingWriteDbContext>(writeConnectionString, forAggregates: typeof(Invoice));
    options.UseMartenEventStore(writeConnectionString, typeof(Reading));
});
```

One of the two calls may omit `forAggregates` and become the host's main store, owning every
aggregate the other does not claim by name — useful when only a handful of aggregates need the other
store's shape. A command handler still resolves `IRepository<TAggregate, TKey>` for exactly one
aggregate and commits through that aggregate's own store; nothing here lets a single command span
two stores. THSS0007 (in `GaWeCodes.Thessera.Analyzers`) flags a command handler whose constructor
injects `IRepository<,>` for more than one aggregate, so a handler that would need to span stores is
caught at compile time, not at startup.

Which of the two calls omits `forAggregates` here is a free choice — it only decides which aggregate
falls into the catch-all. It is a different question, at a lower level, which store holds
Wolverine's own inbox/outbox tables: Wolverine allows exactly one message store tagged Main per
host, and Marten's plain `IntegrateWithWolverine()` — the one this package uses — can only ever be
that Main store; it has no ancillary mode. So whenever this store shares a host with an EF Core one,
the EF Core store is always the one that becomes Wolverine's Ancillary message store, regardless of
which one is *this family's* main aggregate store above. See
[ADR 0017](../../docs/architecture/0017-wolverine-main-ancillary-message-store.md).

## What it checks at startup

- The chosen **aggregate style matches the store**. Every aggregate a registered handler asks for
  through `IRepository<TAggregate, TKey>` is checked before the host finishes starting, by name: a
  plain `AggregateRoot` aggregate selected with this store has no history to replay and is refused —
  there is no `WithoutEventHistory()` for this direction, unlike the mirror case on the state store.
  This is the same check either store registers; only the direction of the mismatch differs.
- Schema is applied only when `ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)` was
  selected; a service that did not select it never touches schema, whatever Marten changes are
  pending.

## Failures you get back as `Result`, not exceptions

A concurrency conflict becomes `Failure.Conflict` with the code from `PersistenceFailureCodes`. A
unique-constraint violation on the underlying PostgreSQL tables becomes a conflict failure too, and
transient Npgsql faults are retried with a cooldown before anything reaches the error queue — the
same translators the state store uses.

## Limits

- **`net10.0` only.** No multi-targeting.
- **PostgreSQL only** — that is Marten, not a restriction added here.
- **Not trim-safe and not AOT-safe.** Domain events and handlers are discovered by scanning, typed
  keys are read reflectively, and repository types are built with `MakeGenericType`. Publish without
  `PublishTrimmed` and without `PublishAot`.
- Marten and WolverineFx are both pinned below their next major version: this package configures
  both through APIs that are not application-level contracts.

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
- `GaWeCodes.Thessera.Analyzers` — the compile-time twin of eight of those conventions, in every host.

## License

MIT. Source, issues and the full documentation: <https://github.com/GaWeCodes/Thessera>.
