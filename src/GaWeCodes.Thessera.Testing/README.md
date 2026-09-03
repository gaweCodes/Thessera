# GaWeCodes.Thessera.Testing

Three tools for testing a Thessera domain: a convention check that catches the aggregate rules in
your build instead of in a running host, a metadata builder so a projection handler can be called
directly, and a snapshot of every persisted name so a rename cannot slip out unnoticed. It makes no
store choice — the family's two are `GaWeCodes.Thessera.Persistence.EfCore.Postgres` (state) and
`GaWeCodes.Thessera.Persistence.Marten` (event stream) — and it depends on **no test framework**, so
it works with xUnit, NUnit, MSTest or TUnit alike. Reference it from test projects only.

**Why not just Wolverine?** Wolverine has no view of your aggregates, so it cannot tell you that one
of them lacks a private parameterless constructor, that a domain event has no persisted name, or
that the read-model snapshot you approved last month no longer matches the code. Those are Thessera
contracts, and this package is where they get checked at the cheapest possible moment — in a pull
request rather than in a deployed service.

## When you need this package

- You want the aggregate conventions verified by a test instead of discovering the break in a
  running host.
- You test projection handlers by calling them directly and need the `DomainEventMetadata` the
  runtime would have handed them.
- You want the persisted shape — stream key format, event names, serialized property names and
  types — pinned by an approval snapshot, so renaming a C# member cannot silently change what is
  written to the database or put on the wire.

## When you don't

- You are not writing tests. Nothing here belongs in a shipped host.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Testing
```

Requires .NET 10 (`net10.0`). Brings `GaWeCodes.Thessera.Domain`,
`GaWeCodes.Thessera.Application` and `GaWeCodes.Thessera.Core`. No test framework, no store, no
broker.

## Getting started

### Aggregate conventions in one test

```csharp
using GaWeCodes.Thessera.Testing;

[Fact]
public void EveryAggregateFollowsTheConventions() =>
    AggregateConventions.Verify([typeof(Reading).Assembly]);
```

`Verify` checks that every aggregate has a parameterless constructor and that it is **not** public,
that every aggregate carries `[AggregateName]`, that every domain event carries `[EventName]`, and
that child entities keep their constructors internal. It reports **all** violations of a run, not
just the first.

It also refuses to pass on nothing: an assembly containing neither an aggregate nor a domain event
throws rather than reporting success. A convention test that finds nothing passes every check
without asserting anything and stays green forever — which is the most expensive way a guard can
fail.

### Calling a projection handler directly

```csharp
var metadata = TestMetadata.For<Reading>(readingId, version: 2);

await new ReadingProjection(context).HandleAsync(new ReadingRecorded(readingId, 42), metadata, default);
```

`For<TAggregate>(key, version, eventId?, occurredAt?)` builds the metadata the runtime would hand
the handler, deriving the aggregate name and the key text through the **same** code the runtime uses.
That is the point of it: a hand-written stub calling `ToString()` on the key agrees with production
today and stops agreeing the moment the key's value type changes — without a test turning red.

`eventId` defaults to a new value; pass one to test redelivery of the same event. `occurredAt`
defaults to the Unix epoch so tests stay deterministic. Use `version` as the watermark your
projection compares against.

### Pinning the persisted names

```csharp
[Fact]
public void ThePersistedSchemaIsUnchanged() =>
    PersistedSchema.Verify("PersistedSchema.approved.txt", [typeof(Reading).Assembly]);
```

`Verify` renders every aggregate's stream key shape and every domain and integration event — its
persisted name, and the serialized name and type of each property — then compares that to the
approved baseline:

```text
aggregate-stream reading/guid

domain-event reading-recorded-v1
  ReadingId : guid
  Value : int
```

On a mismatch it writes `PersistedSchema.received.txt` next to the approved file, so the diff is a
file comparison and accepting an intended change is a file rename. `Render(assemblies)` gives you
the same text if you prefer to drive the comparison yourself.

## Limits

- **`net10.0` only.** No multi-targeting.
- The baseline path must end in `.approved.txt`, so that the received rendering can be written
  beside it.
- **Not trim-safe and not AOT-safe.** Everything here works by scanning assemblies and reading typed
  keys reflectively — which is fine, because none of it ships in a host.

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
- `GaWeCodes.Thessera.Analyzers` — the compile-time twin of those conventions, in every host.

## License

MIT. Source, issues and the full documentation: <https://github.com/GaWeCodes/Thessera>.
