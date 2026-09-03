# GaWeCodes.Thessera.Wolverine

The runtime leg of Thessera: it activates [Wolverine](https://wolverinefx.net) for a host, routes
domain events and projections through durable local queues, applies the outbox and idempotency
policies, publishes integration events under their declared topics, and registers a dead-letter
health check. It is **not a store choice** — the two of those are
`GaWeCodes.Thessera.Persistence.EfCore.Postgres` and `GaWeCodes.Thessera.Persistence.Marten`, and
each of them brings this package with it.

**Why not just Wolverine?** You *are* using Wolverine — that is the point of this package, and
Thessera never pretends otherwise. Wolverine is named in this package's id, in its dependency list
and in its API. What it does not give you is an aggregate, business rules, typed identifiers, domain
events with stable persisted names, or a switch between state store and event store. This package
is the seam where that domain model meets Wolverine's engine, so that the transactional outbox
carries *your* domain events and not a hand-rolled equivalent.

## When you need this package

- You want to reach Wolverine's own configuration from inside a Thessera host —
  `CustomizeWolverine(...)`.
- You are writing a persistence adapter and need `UseWolverineRuntime()` plus
  `IOutboxDurabilityConfigurator` so your store's transaction and the outbox commit together.
- You are writing a transport adapter and need `IWolverineMessagingTransport`.

## When you don't

- You picked a store package. It already depends on this one; you do not name it again.
- You want the dispatcher and in-process domain events **without** a broker or a database. Use
  `GaWeCodes.Thessera.Core` alone — it has no Wolverine dependency, and that is verified rather than
  claimed.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Wolverine
```

Requires .NET 10 (`net10.0`). Brings `GaWeCodes.Thessera.Core`, `WolverineFx.RuntimeCompilation` and
`Microsoft.Extensions.Diagnostics.HealthChecks`.

## Getting started

You do not activate this runtime yourself. A store or transport package does it, and from your host
it is one optional line:

```csharp
using GaWeCodes.Thessera;

builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(RecordReading).Assembly);
    options.AddDomainEventsFrom(typeof(Reading).Assembly);
    options.UseEfCoreStateStore<ReadingWriteDbContext>(writeConnectionString);

    options.CustomizeWolverine(wolverine =>
    {
        wolverine.Policies.AutoApplyTransactions();
        wolverine.Durability.Mode = DurabilityMode.Balanced;
    });
});
```

`CustomizeWolverine` is applied on top of everything Thessera configures, so it can override any of
it. Use it for what belongs to you — extra endpoints, retry policies, durability mode — and leave
the outbox and the domain-event routing alone unless you know what you are replacing.

A host runs exactly **one** runtime. Selecting two is an error, and the message says why: the
runtime owns the outbox, the inbox and the local queues every domain event travels through, so two
of them would each hold half of the delivery guarantees.

### What it wires

- **System.Text.Json with typed-key support**, so an aggregate identity round-trips as its bare
  value rather than as an object.
- **Two durable local queues**, one for domain events and one for projections, so a slow projection
  cannot block domain-event delivery and a crash loses neither. Both partition by
  `<aggregate-name>/<id>`, so the events of one aggregate are handled in order while different
  aggregates run in parallel.
- **The transactional outbox**, when a store is selected: domain events are written in the same
  transaction as the aggregate state.
- **A retry policy** that separates three cases, regardless of whether a store is selected — a
  malformed message or a broken domain rule goes straight to the error queue, a transient fault
  (defined by the store) is retried with a cooldown, anything else is retried briefly and then
  given up on.
- **A seven-day idempotency window**, when a store is selected: how long a processed message's
  identity is kept in the durable message store, so a redelivered copy is recognized and not
  handled twice.
- **Integration-event topics** on `Policies.AllSenders`, filtered to `IIntegrationEvent`. This is
  brokerneutral: `[IntegrationEventTopic]` takes effect on **any** transport, and a transport author
  contributes nothing to make it work.
- **A dead-letter health check**, reported as *degraded* rather than *unhealthy* — the host keeps
  serving, but the work in those messages did not happen, and a dead-lettered projection envelope
  means a read model that stays wrong until it is rebuilt.

### Writing an adapter against it

```csharp
public sealed class MyStoreAdapter : IPersistenceAdapter
{
    public void Register(PersistenceRegistrationContext context)
    {
        // ... register repository, tracker, unit of work ...
        context.UseWolverineRuntime()
               .AddOutboxDurability(new MyOutboxDurability(connectionString));
    }
}

internal sealed class MyOutboxDurability(string connectionString) : IOutboxDurabilityConfigurator
{
    public void Configure(WolverineOptions options) => options.PersistMessagesWithMyDatabase(connectionString);
}
```

A transport adapter implements `IWolverineMessagingTransport` instead and gets `Configure(options,
provisionInfrastructure)` and `ConfigureSubscription(options, subscription)`. Selecting a transport
that does **not** implement it fails at startup with an explicit message, rather than starting a
host whose integration events are dropped in silence.

Be aware of what this implies: a transactional outbox has to know the message engine, so any store
adapter ends up referencing WolverineFx through this package. The family's claim that the runtime is
exchangeable holds only for the case **without** persistence.

## Limits

- **`net10.0` only.** No multi-targeting.
- **Not trim-safe and not AOT-safe.** Wolverine generates and compiles handler code at run time, and
  Thessera discovers handlers and domain events by scanning. Publish without `PublishTrimmed` and
  without `PublishAot`.
- The dependency on WolverineFx is pinned below its next major version. This package configures
  Wolverine through APIs that are not an application-level contract, so a major upgrade is
  deliberately not taken sight-unseen.

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
