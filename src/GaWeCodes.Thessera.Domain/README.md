# GaWeCodes.Thessera.Domain

Tactical DDD building blocks — aggregates, entities, domain events, typed identifiers and business
rules — written so that the *same* domain code can later be stored as state (EF Core) or as an event
stream (Marten). This package is the base of the Thessera family and makes no store choice: it
declares no package reference at all and depends on nothing but the BCL.

**Why not just Wolverine?** Wolverine is an excellent message engine, and Thessera runs on it further
up. What Wolverine does not give you is an aggregate, business rules, typed identifiers, domain
events with stable persisted names, or a switch between state store and event store. That is what
this family adds — and this package is the layer where none of that has met a framework yet.

## When you need this package

- You are writing a domain model and want aggregate, entity, domain-event, typed-key and rule
  primitives without inheriting a framework.
- You want the option, later, to persist that model as state or as an event stream without
  rewriting it.
- You want a domain project whose dependency graph contains no EF Core, no Marten, no Wolverine and
  no Npgsql — and stays that way, provably.

## When you don't

- You only need messaging, an outbox or a mediator. Use [Wolverine](https://wolverinefx.net)
  directly; this package has nothing to add there.
- You are writing an application or infrastructure layer. Those reference
  `GaWeCodes.Thessera.Application` and `GaWeCodes.Thessera.Core`, which bring this package with them.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Domain
```

Requires .NET 10 (`net10.0`). No other dependency.

## Getting started

An aggregate is a pair: a `record` holding the state and a class holding the behaviour. The state
folds events; the aggregate raises them.

```csharp
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;

public readonly record struct ReadingId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;

    public static ReadingId New() => new(Guid.NewGuid());
}

[EventName("reading-recorded-v1")]
public sealed record ReadingRecorded(ReadingId ReadingId, int Value) : DomainEvent;

public sealed record ReadingValueMustBePositive(int Value) : IDomainValidationRule
{
    public string Code => "reading.value.not-positive";

    public string? Target => nameof(Value);

    public string Message => "A reading must carry a positive value.";

    public bool IsInvalid() => Value <= 0;
}

public sealed record ReadingState(ReadingId Id, int Value) : AggregateState<ReadingState, ReadingId>
{
    public static ReadingState Empty => new(default, 0);

    public override ReadingState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ReadingRecorded recorded => this with { Id = recorded.ReadingId, Value = recorded.Value },
        _ => this,
    };
}

[AggregateName("reading")]
public sealed class Reading : AggregateRoot<ReadingId, ReadingState>
{
    private Reading() : base(ReadingState.Empty)
    {
    }

    public int Value => State.Value;

    public static Reading Record(ReadingId id, int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        var reading = new Reading();
        reading.RaiseEvent(new ReadingRecorded(id, value));
        return reading;
    }
}
```

`Reading.Record(...)` returns an aggregate whose `Version` is 1 and whose `DomainEvents` holds the
one event that produced it. Nothing has been stored — that is the job of a store package, and the
code above does not change when you pick one.

The rules that make this work, and where each of them is caught:

- The parameterless constructor exists and is **private**. A repository reconstitutes through it; a
  caller must go through the factory method. That it exists is checked while the host is composed,
  once a store is selected; that it is private is not — only the convention test below sees that.
- `Apply` returns the state that follows the event and returns `this` unchanged for events it does
  not know. Returning `null` is rejected the moment an event is applied, not at startup.
- The applied event must set a non-empty identity. An aggregate without one cannot be addressed;
  this is checked when the event is applied, too.
- Child entities derive from `Entity<TKey, TState>`, keep an **internal** constructor and raise
  through their root via `IChildOwner<TKey, TState>` — a child hull built without its root would
  have nothing to raise into. Nothing in the runtime checks this one.

`GaWeCodes.Thessera.Testing` moves the two constructor rules — and the presence of
`[AggregateName]` and `[EventName]` — into a single test, before any host starts.

### Which base class

`AggregateRoot<TKey, TState>` is the state-stored form. `EventSourcedAggregateRoot<TKey, TState>`
adds replay from history and nothing else — same state record, same `Apply`, same rules.

The choice is worth making deliberately, because it decides how portable the model is:

- An `EventSourcedAggregateRoot` runs on **both** store choices. On the event store it keeps its
  stream; on the state store it keeps state and version, once that host states the intent with
  `WithoutEventHistory()`. The repo proves this with one aggregate driven through both stores and
  compared.
- A plain `AggregateRoot` runs on the **state** store only. An event store has to replay history to
  reconstitute it, and this base class cannot.

## The names in the attributes are a contract

`[AggregateName]` and `[EventName]` are validated against `NameSegment`: lower-case ASCII letters,
digits and single, non-leading, non-trailing hyphens — `widget-created-v1`, not `WidgetCreated`.

That character set is deliberately narrow because these names leave the process. An aggregate name
prefixes every stream key, an event name is written into every persisted event row, and both
travel on the domain-event envelope. A published integration-event topic is built from the same
segment grammar, so the set also has to survive as a routing key. Renaming an attribute value
orphans everything already stored under the old one; renaming the C# type does not, which is the
entire point of writing the name down.

## What this package promises, and what the runtime adds

- Domain events reaching an outbox in the same transaction as the aggregate's state: a property of
  the shipped `IUnitOfWork` implementations, which pair a persistence store with
  `GaWeCodes.Thessera.Wolverine`. A custom `IUnitOfWork` need not have an outbox at all.
- `BusinessRuleViolationException` and `DomainValidationException` being caught and turned into a
  failed result: done by `ExceptionToResultBehavior`, registered only when the host is composed via
  `AddThessera` (`GaWeCodes.Thessera.Core`).
- An `AggregateState<TSelf, TKey>` bound to the wrong `TSelf`, or an aggregate's event-sourced style
  mismatching the selected store, being caught at startup instead of on first use: startup checks
  registered the same way.
- A typed key's value being restricted to `Guid`, `string`, `int` or `long`, and an empty or
  separator-containing key being refused before it becomes a stream key: enforced by
  `EntityKeyFormatter` in `GaWeCodes.Thessera.Core`, called by whichever store package you picked.
- A type missing `[AggregateName]` or `[EventName]` being refused rather than silently named after
  its CLR type: enforced the same way, when `EntityKeyFormatter` or `DomainEventTypeRegistry`
  (`GaWeCodes.Thessera.Core`) first reads the attribute — or earlier, by the convention test in
  `GaWeCodes.Thessera.Testing`.

## Limits

- **`net10.0` only.** No multi-targeting.
- **This package uses no reflection**, but the family as a whole is **not trim-safe and not
  AOT-safe**: the runtime packages discover your handlers and domain events by scanning assemblies
  and build generic types at run time. Publish without `PublishTrimmed` and without `PublishAot`.
  The affected members carry `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` and say so.

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
