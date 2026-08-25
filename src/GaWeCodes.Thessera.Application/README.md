# GaWeCodes.Thessera.Application

The contracts an application layer talks to: commands, queries and their handlers, the pipeline
behaviour, `Result`/`Failure`, the repository and unit-of-work contracts, projection handlers and
the integration-event contracts. **Contracts only** — no dispatcher, no composition root, no store
choice. It depends on `GaWeCodes.Thessera.Domain` and on nothing else.

**Why not just Wolverine?** Wolverine can already dispatch a message and give you an outbox. What it
does not give you is an application layer that compiles against a repository over _your_ aggregate,
a uniform failure channel your API can map to status codes, and a projection contract that survives
a switch between state store and event stream. Those are the contracts in this package — and none of
them drags a runtime into your use-case project.

## When you need this package

- You are writing use cases and want to reference command, query, repository and result contracts
  **without** inheriting a dispatcher, a DI container or a message broker.
- You want your handlers testable by calling them directly, with no host in the picture.
- You are mapping domain events to integration events, or projecting them into a read model.

## When you don't

- You are only writing the domain model. `GaWeCodes.Thessera.Domain` is enough.
- You are wiring a host. `GaWeCodes.Thessera.Core` implements these contracts and brings this
  package with it.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Application
```

Requires .NET 10 (`net10.0`). Pulls in `GaWeCodes.Thessera.Domain`; no third-party dependency.

## Getting started

A command, its handler, and a query — the shape of nearly everything in this layer:

```csharp
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;

public sealed record RecordReading(int Value) : ICommand<ReadingId>;

public sealed class RecordReadingHandler(IRepository<Reading, ReadingId> repository)
    : ICommandHandler<RecordReading, ReadingId>
{
    public async Task<Result<ReadingId>> HandleAsync(RecordReading command, CancellationToken cancellationToken)
    {
        var reading = Reading.Record(ReadingId.New(), command.Value);
        await repository.AddAsync(reading, cancellationToken).ConfigureAwait(false);

        return reading.Id;   // implicitly converted to Result<ReadingId>
    }
}

public sealed record GetReading(ReadingId Id) : IQuery<ReadingView>;

public sealed class GetReadingHandler(IReadingStore store) : IQueryHandler<GetReading, ReadingView>
{
    public async Task<Result<ReadingView>> HandleAsync(GetReading query, CancellationToken cancellationToken)
    {
        var view = await store.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return view is null
            ? Failure.NotFound("reading.not_found", "No such reading.")
            : view;          // both branches convert implicitly
    }
}
```

The handler never commits. `IUnitOfWork` is committed once per command by the runtime, in the same
transaction that writes the outbox. `IRepository<TAggregate, TKey>` is deliberately narrow —
`GetByIdAsync` and `AddAsync` — because everything else an aggregate needs is a method on the
aggregate.

To run these you need a dispatcher; `GaWeCodes.Thessera.Core` registers one and exposes `ISender`.
That dispatcher is glue, not a product: if you are shopping for a mediator, this is not the reason
to choose Thessera.

### Cross-cutting behaviour

```csharp
public sealed class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestPipeline<TResponse> pipeline,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await pipeline.NextAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Log(typeof(TRequest).Name, Stopwatch.GetElapsedTime(started));
        }
    }
}
```

Register it with `options.AddPipelineBehavior(typeof(TimingBehavior<,>), order)` inside
`AddThessera`. Lower order runs further out; the built-in behaviours sit at
`ThesseraOptions.LoggingBehaviorOrder` (0), `ExceptionToResultBehaviorOrder` (100) and
`UnitOfWorkBehaviorOrder` (300), so you can place yours relative to them by name.

A behaviour that wants to stop the request calls `pipeline.Failed(failure)` instead of
`pipeline.NextAsync(...)` — that produces the correctly typed failed response without the behaviour
having to know whether `TResponse` is `Result` or `Result<T>`.

### Projections and integration events

```csharp
public sealed class ReadingProjection(ReadDbContext context) : IProjectionHandler<ReadingRecorded>
{
    public Task HandleAsync(
        ReadingRecorded domainEvent,
        DomainEventMetadata metadata,
        CancellationToken cancellationToken)
    {
        // metadata.Version is the aggregate version. Use it as a watermark and ignore anything not
        // newer, so a redelivered event cannot move the read model backwards.
        ...
    }
}

[IntegrationEventTopic("readings.reading-recorded")]
public sealed record ReadingRecordedIntegrationEvent(
    Guid ReadingId,
    int Value,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;

public sealed class ReadingMapper : IIntegrationEventMapper<ReadingRecorded>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(ReadingRecorded domainEvent, DomainEventMetadata metadata) =>
        [new ReadingRecordedIntegrationEvent(
            domainEvent.ReadingId.Value, domainEvent.Value, metadata.EventId, metadata.OccurredAt)];
}
```

Handlers, projections and mappers are discovered by assembly scanning; you hand the assemblies to
`AddThessera`. Delivery is at-least-once, so write projections idempotently — the `Version`
watermark above is the intended way.

## Two contracts worth reading before you depend on them

**`FailureCategory` is allowed to grow.** It has five members today — `Validation`, `BusinessRule`,
`NotFound`, `Conflict`, `Forbidden` — and new ones may be added in a **minor** version. That is a
deliberate trade: a closed enum would force a major version for every failure kind the family ever
learns. It means a `switch` over the category must carry a `_` arm, and that arm should map to a
generic server-side failure rather than throw. Code without one compiles today and breaks on an
upgrade that is otherwise not breaking.

**An integration-event topic is a published routing key.** `[IntegrationEventTopic]` requires the
form `<context>.<event>`, both segments lower-case kebab-case (`readings.reading-recorded`). The
first segment names the owning bounded context and is checked at publish time against the context
name the host registered — a service cannot publish under a foreign context. Consumers bind to
patterns such as `readings.*`, so the value is a contract with everyone who has ever subscribed,
and it is deliberately independent of the CLR type the attribute happens to sit on.

## Limits

- **`net10.0` only.** No multi-targeting.
- `Result<Failure>` is rejected at construction: a failure is never a success value, and both
  implicit conversions of `Result<TResult>` would apply to it. Use the non-generic `Result` for an
  operation with no return value.
- The family is **not trim-safe and not AOT-safe**. Handler discovery scans assemblies and the
  dispatcher builds generic types at run time. Publish without `PublishTrimmed` and without
  `PublishAot`.

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
