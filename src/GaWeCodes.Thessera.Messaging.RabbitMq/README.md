# GaWeCodes.Thessera.Messaging.RabbitMq

Lets a Thessera service publish its integration events to RabbitMQ and subscribe to other services'
events. It is the family's **opt-in transport**, not a store choice — the two store choices are
`GaWeCodes.Thessera.Persistence.EfCore.Postgres` (state) and `GaWeCodes.Thessera.Persistence.Marten`
(event stream). One entry point: `UseWolverineMessaging(rabbitMqUri, exchangeName, contextName)`.

**Without a transport package, no integration event leaves the service.** That is by design and it
is not silent: the runtime falls back to a sink that logs a warning per discarded event. In-process
work — domain events, projections, read models — keeps running untouched. Adding this package is
what turns a self-contained service into a participant in a message-driven system.

**Why not just Wolverine?** Wolverine owns the RabbitMQ connection, the queues and the exchange, and
this package is a thin layer over `WolverineFx.RabbitMQ`, named after what it wraps. What it adds
is the contract above it: integration events that carry a **declared** topic rather than a CLR type
name, a bounded-context segment that a service cannot forge, and a self-event filter
so a service does not consume what it just published. The topic routing itself lives in the
broker-neutral core, so `[IntegrationEventTopic]` works on any transport — that is the part this
package deliberately does *not* own.

## When you need this package

- Your service publishes integration events that other services consume.
- Your service subscribes to another context's events.
- You are on RabbitMQ.

## When you don't

- Your bounded context is self-contained. Leave it out; you get a host with no broker and no
  broker-topology check. The dead-letter health check still runs once a store is selected — it
  has nothing to do with a transport.
- You are on a different broker. Wolverine supports many; implement `IWolverineMessagingTransport`
  in `GaWeCodes.Thessera.Wolverine` against the same seam. That seam was measured against a working
  Kafka transport, which is about as unlike RabbitMQ as a broker gets.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Messaging.RabbitMq
```

Requires .NET 10 (`net10.0`) and RabbitMQ. Brings `GaWeCodes.Thessera.Wolverine` and
`WolverineFx.RabbitMQ`. It brings **no** store — add one of the two store packages as well.

## Getting started

### 1. Declare the event and its topic

```csharp
using GaWeCodes.Thessera.Application.IntegrationEvents;

[IntegrationEventTopic("readings.reading-recorded")]
public sealed record ReadingRecordedIntegrationEvent(
    Guid ReadingId,
    int Value,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
```

The topic is the published routing key: `<context>.<event>`, both segments lower-case kebab-case.
The first segment names the owning bounded context.

### 2. Map a domain event to it

```csharp
public sealed class ReadingMapper : IIntegrationEventMapper<ReadingRecorded>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(ReadingRecorded domainEvent, DomainEventMetadata metadata) =>
        [new ReadingRecordedIntegrationEvent(
            domainEvent.ReadingId.Value, domainEvent.Value, metadata.EventId, metadata.OccurredAt)];
}
```

Mappers are found by scanning the assemblies you pass to `AddHandlersFrom`.

### 3. Select the transport

```csharp
using GaWeCodes.Thessera;

builder.AddThessera(options =>
{
    options.AddHandlersFrom(typeof(ReadingMapper).Assembly);
    options.AddDomainEventsFrom(typeof(Reading).Assembly);
    options.UseEfCoreStateStore<ReadingWriteDbContext>(writeConnectionString);

    options.UseWolverineMessaging(rabbitMqUri, exchangeName: "integration-events", contextName: "readings");
});
```

`contextName` is your bounded context and must be a single lower-case kebab-case word without a dot
— it is the first segment of every routing key this service publishes, and publishing an event
whose topic names a different context is refused rather than allowed to impersonate that service.
`exchangeName` is the shared durable topic exchange; every participating service names the same one.

### 4. Subscribe to someone else's events

```csharp
options.SubscribeToIntegrationEvents(
    endpointName: "readings.integration-events",
    consumerAssembly: typeof(Program).Assembly,
    "orders.*", "billing.invoice-issued");
```

That declares one durable queue with a durable inbox, binds it to the exchange with each pattern,
and scans `consumerAssembly` for the handlers. At least one non-blank pattern is required: a queue
with no binding receives nothing, and neither the broker nor Wolverine calls that an error.

A middleware skips events this service published itself, recognised by the
`thessera.source-context` header — so a service can bind a broad pattern without consuming its own
echo.

### 5. Let something create the topology

Exchange and queues are declared but not created unless you say
`ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)` — appropriate for a migration job or
local development, not for a service in production. A startup check verifies the topology is
actually there rather than letting the host start against a broker that will drop everything.

## What you get on the wire

- A **durable topic exchange**, quorum queues, persistent messages and publisher confirms.
- The **durable outbox**: an integration event is committed with the aggregate that caused it, then
  sent. Delivery is at-least-once — consumers must be idempotent.
- The publishing context in the `thessera.source-context` header.

## Limits

- **`net10.0` only.** No multi-targeting.
- **RabbitMQ only.** Thessera makes no promise of a broker matrix — Wolverine's transport catalogue
  is Wolverine's achievement, not this family's, and a matrix nobody maintains is worse than none.
  The seam is documented and proven once; further transports happen on demand.
- **Not trim-safe and not AOT-safe.** Publish without `PublishTrimmed` and without `PublishAot`.
- `WolverineFx.RabbitMQ` is pinned below its next major version; the exact range is on this
  package's dependency list.

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
