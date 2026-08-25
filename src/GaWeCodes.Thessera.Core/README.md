# GaWeCodes.Thessera.Core

The composition root and the runtime that fulfils the Thessera contracts: `AddThessera(...)`, the
CQRS dispatcher and its pipeline behaviours, domain-event delivery, projection dispatch,
integration-event mapping, typed-key formatting and JSON, telemetry, and the startup checks that
turn silent misconfiguration into a message at boot. It makes **no store choice** — you add exactly
one on top, either `GaWeCodes.Thessera.Persistence.EfCore.Postgres` (state) or
`GaWeCodes.Thessera.Persistence.Marten` (event stream) — and it contains **no Wolverine**: its only
dependencies are `Microsoft.Extensions.*.Abstractions`.

**Why not just Wolverine?** Because that question has two halves and this package answers only one
of them honestly. Wolverine is the message engine, and from the store packages upwards Thessera runs
on it, openly and by name. What Wolverine does not give you is an aggregate, business rules, typed
identifiers, domain events with stable persisted names, or a switch between state and event stream.
This package is the wiring that connects that domain model to a runtime — and it is deliberately
buildable **without** the runtime, so a host that only dispatches commands never restores Wolverine
at all.

## When you need this package

- You are wiring a service host and want one call that registers handlers, domain events,
  projections, integration-event mappers, the unit-of-work behaviour and the startup checks.
- You want the dispatcher and in-process domain-event delivery **without** a broker or a database in
  your dependency graph.
- You are writing a persistence or transport adapter and need the extension seams
  (`IPersistenceAdapter`, `IMessagingTransportAdapter`, `IRuntimeActivator`).

## When you don't

- You are writing a domain or an application project. Reference `GaWeCodes.Thessera.Domain` or
  `GaWeCodes.Thessera.Application` — those stay free of any runtime.
- You already picked a store. Both store packages bring this one with them, so you rarely name it
  explicitly.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Core
```

Requires .NET 10 (`net10.0`). Brings `GaWeCodes.Thessera.Domain`, `GaWeCodes.Thessera.Application`
and the dependency-injection, hosting and logging abstractions. Nothing else.

## Getting started

```csharp
using GaWeCodes.Thessera;

var builder = Host.CreateApplicationBuilder(args);

builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(RecordReading).Assembly);      // commands, queries, behaviours,
    options.AddHandlersFrom(typeof(ReadingProjection).Assembly);  // projections, mappers
    options.AddDomainEventsFrom(typeof(Reading).Assembly);        // the [EventName] catalogue

    options.UseNoPersistence();                                   // or a store package, see below
});

var host = builder.Build();
```

That gives you `ISender` and in-process domain-event delivery. `UseNoPersistence()` is not a
formality: without a persistence choice and without your own `IUnitOfWork`, a host whose scanned
assemblies contain commands fails at startup, because every one of those commands would report
success while nothing is committed and nothing at run time would say so. Saying `UseNoPersistence()`
turns that error into a logged statement of intent.

Use the `IHostApplicationBuilder` overload, not `services.AddThessera(...)`, whenever a runtime is
involved: the runtime activator needs the builder, and the `IServiceCollection` overload cannot
activate it.

With a store and a broker the same block grows by two lines and nothing else changes:

```csharp
using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Core.DependencyInjection;   // for InfrastructureProvisioning

builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(RecordReading).Assembly);
    options.AddDomainEventsFrom(typeof(Reading).Assembly);

    options.UseEfCoreStateStore<ReadingWriteDbContext>(writeConnectionString);
    options.UseWolverineMessaging(rabbitMqUri, exchangeName, contextName: "readings");

    options.SubscribeToIntegrationEvents("readings.integration-events", typeof(Program).Assembly, "orders.*");
    options.ProvisionInfrastructure(InfrastructureProvisioning.Never);
});
```

One `using GaWeCodes.Thessera;` reaches `AddThessera` and **every** `Use*` the family offers, from
any package — the entry points deliberately share that one namespace, for the same reason
`AddConsole()` lives in `Microsoft.Extensions.Logging` rather than in
`Microsoft.Extensions.Logging.Console`.

`AddThessera` is called **once per host** and is deliberately not package-specific: every satellite
package contributes to this same registration through a `Use*` extension on `ThesseraOptions`.

### What the options offer

- `AddHandlersFrom(assembly)` — command and query handlers, pipeline behaviours, projection handlers
  and integration-event mappers, found by scanning.
- `AddDomainEventsFrom(assembly)` — the `[EventName]` catalogue. The event store maps persisted
  names from it, and the envelope serializer resolves incoming names against it.
- `AddPipelineBehavior(openGenericBehavior, order)` — your own cross-cutting behaviour. The built-in
  logging, exception-to-result and unit-of-work behaviours sit at 0, 100 and 300.
- `UsePersistence(adapter)` / `UseNoPersistence()` — a store, or the explicit absence of one. Two
  different stores in one host is an error: a bounded context has one write database, and a commit
  cannot span two.
- `WithoutEventHistory()` — allows an event-sourced-style aggregate on a **state** store. It is a
  door handle you pull deliberately: the state and the version are written correctly and the outbox
  is fed, but no stream is kept, and that loss is silent and permanent.
- `UseMessagingTransport(adapter)` and `SubscribeToIntegrationEvents(endpointName, assembly, patterns)`.
- `ProvisionInfrastructure(Never | AtStartup)` — whether the host may create schema, exchanges and
  queues. Services normally say `Never` and leave it to a migration job.

### What runs at startup

Before hosted services start, the core checks that every discovered command and query has exactly
one handler, that the aggregate style matches the chosen store, that aggregate state binds to
itself, that every integration-event mapper is reachable, and that a unit of work exists when
commands do. Each check exists because the failure it catches is otherwise silent until production.

Add your own with `IStartupCheck` (or `SynchronousStartupCheck`) and a `StartupPhase`.

### Telemetry

The `ActivitySource` is named `Thessera`; add it with `AddSource("Thessera")`. Tags are prefixed
`thessera.` (`thessera.request.name`, `thessera.outcome`, `thessera.aggregate.name`, …). Published
integration events carry the publishing context in the `thessera.source-context` message header,
which is how a service recognises and skips its own events.

## The stream key is a wire format

`EntityKeyFormatter` renders an aggregate identity as `<aggregate-name>/<key-value>` — the `/` is
the separator, the aggregate name comes from `[AggregateName]`. This text is not an internal detail:
it is the stream key in the event store and it appears in persisted rows and in domain-event
envelopes. It is therefore pinned, not left to whatever `ToString()` happens to produce:

- **`Guid`** — format `D`, invariant culture.
- **`string`** — verbatim, and a value containing `/` is rejected, because it would let two
  different aggregates address the same stream.
- **`int` / `long`** — invariant decimal, and negative values are rejected: a negative identity is
  almost always an uninitialised value that would quietly open a stream of its own.
- Any other value type is rejected outright. A `decimal` keeps trailing zeros, an `enum` writes a
  member name and a `DateTime` follows a calendar convention — each of which makes existing streams
  unreachable the day it changes.

Typed keys serialize as their bare value; apply `EntityKeyJsonOptions.Apply(...)` to a
`JsonSerializerOptions` if you serialize domain types yourself.

## Extending it

The seams below are the whole contract for a store or transport author. They were measured against a
throwaway EF Core adapter for SQLite and SQL Server and a throwaway Kafka transport, both written
strictly against the public API and both compiled against exactly these types.

- `IPersistenceAdapter` + `PersistenceRegistrationContext` — announce a store, register its services.
- `IMessagingTransportAdapter` + `MessagingTransportRegistrationContext` — announce a transport.
- `IRuntimeActivator` + `RuntimeActivation` — the runtime a host activates. One per host.
- `AggregateTracker<T>`, `ITrackedAggregate`, `AggregateFactory`, `DomainEventEnvelopeFactory`,
  `EntityKeyFormatter`, `PersistenceFailureCodes`, `ReadModelRebuildWriter` — what a store
  implementation writes against.
- `IPersistenceFaultTranslator` — turn a driver exception into a `Failure`.

Two honest notes. A transactional outbox has to know the message engine, so a store adapter will in
practice reference `GaWeCodes.Thessera.Wolverine` and, through it, WolverineFx — the claim
"runtime is exchangeable" holds only for the case without persistence. And an aggregate's state is
an immutable record: a tracker that keeps the object it loaded, rather than the object the aggregate
holds *after* the events were applied, will store the old state and report success. Track the
aggregate, read `IStateOwner.State` at save time.

## Limits

- **`net10.0` only.** No multi-targeting.
- **Not trim-safe and not AOT-safe.** Handlers, domain events and aggregates are found by scanning
  assemblies; dispatcher, projection and mapper types are built with `MakeGenericType`. Publish
  without `PublishTrimmed` and without `PublishAot`. The affected public members carry
  `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` with the same explanation.
- One consequence deserves naming, because no analyser reports it: the startup check that verifies
  handler registration finds commands with the same reflection the linker has just emptied. On a
  trimmed build it compares nothing with nothing, reports success and lets the host start. This is
  not fixable with this approach — do not trim.

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
