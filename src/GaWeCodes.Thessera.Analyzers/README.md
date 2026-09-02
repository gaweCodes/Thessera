# GaWeCodes.Thessera.Analyzers

Six Roslyn analyzers, each the compile-time twin of a check the runtime otherwise performs only
when a host starts, or that `GaWeCodes.Thessera.Testing` otherwise performs only when a test runs.
It makes no store choice, brings no runtime dependency, and ships no code that runs in a deployed
host — the compiled analyzer runs inside the compiler process only. Reference it from **every**
host project, regardless of which store or broker it selects.

**Why not just the startup checks or `GaWeCodes.Thessera.Testing`?** Both exist and stay exactly as
they are — this package does not replace either. A startup check needs a running host to fail, and
a convention test needs someone to have written and be running one. This package moves six of
those same violations into the build itself, so a misconfigured aggregate, event or state is a red
squiggle in the editor and a failed `dotnet build`, not a message the first affected host prints at
boot or a test a project happened to include.

## When you need this package

- You want a missing `[AggregateName]`, a missing `[EventName]`, an aggregate whose parameterless
  constructor is absent or public, a child entity with a public constructor, or an aggregate- or
  entity-state that names the wrong type as itself caught while you are writing the type — not the
  first time a repository, the event-catalogue build, or the first applied event touches it.
- You maintain more than one host or more than one team writes aggregates, and want the same six
  rules enforced identically everywhere without relying on everyone remembering to write (and run)
  the equivalent `GaWeCodes.Thessera.Testing` check.

## When you don't

- Nothing here belongs in a library that only defines contracts and never derives from
  `AggregateRoot<TKey, TState>`, `EventSourcedAggregateRoot<TKey, TState>`, `Entity<TKey, TState>`,
  `AggregateState<TSelf, TKey>`, `EntityState<TSelf, TKey>` or `IDomainEvent` — every rule is a
  no-op on a compilation that never references `GaWeCodes.Thessera.Domain` at all, so referencing
  this package there costs nothing, but it also adds nothing.

## Install

```bash
dotnet add package GaWeCodes.Thessera.Analyzers
```

```xml
<PackageReference Include="GaWeCodes.Thessera.Analyzers" PrivateAssets="all" />
```

`PrivateAssets="all"` keeps the analyzer from being offered transitively to whatever references your
project — it is a development-time tool, not a runtime dependency, and every consuming project should
add its own reference rather than inherit one. The package carries no `lib/` assembly: nothing is
added to your build output, and nothing needs a version bump when the rest of the family does.

## The six rules

| Diagnostic | Flags |
| --- | --- |
| `THSS0001` | A non-abstract type deriving from `AggregateRoot<TKey, TState>` (directly, or through `EventSourcedAggregateRoot<TKey, TState>`) that does not carry `[AggregateName]` directly. |
| `THSS0002` | A non-abstract type implementing `IDomainEvent` that does not carry `[EventName]` directly. |
| `THSS0003` | A non-abstract type deriving from `AggregateRoot<TKey, TState>` that has no parameterless constructor, or whose parameterless constructor is `public`. |
| `THSS0004` | A non-abstract type deriving from `Entity<TKey, TState>` that exposes any `public` constructor. |
| `THSS0005` | A non-abstract type deriving from `AggregateState<TSelf, TKey>` whose first type argument does not name the deriving type itself. |
| `THSS0006` | A non-abstract type deriving from `EntityState<TSelf, TKey>` whose first type argument does not name the deriving type itself. |

Every rule is `Error` severity and enabled by default — a build with one of these violations does not
merely warn, it fails, the same way a build with a missing `using` fails. All six are read directly
off the compilation being built through `Compilation.GetTypeByMetadataName`, never by referencing
`GaWeCodes.Thessera.Domain` from this package's own code; a compilation that does not reference it at
all resolves nothing and every rule silently reports zero diagnostics for it.

Read the reasoning behind each rule in
[ADR 0014](../../docs/architecture/0014-a-compile-time-analyzer-catches-four-startup-checks.md) and
[ADR 0015](../../docs/architecture/0015-two-more-analyzer-rules-catch-self-binding.md).

## What this deliberately does not check

- **Persistence-strategy matches its store**, and **a handler that is registered but has no
  aggregate to dispatch to** — both are `HandlerRegistrationCheck`/`AggregatePersistenceMatchCheck`
  concerns that depend on the DI container actually being assembled; there is nothing for a syntax-
  or symbol-level analyzer to look at before that happens.

That remains exactly what it is today: a startup check, not a build error. Extending this package
to it is a decision for a later ADR, not an accident of scope creep in this one.

## Getting started

```csharp
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Naming;

// THSS0001: missing [AggregateName].
public sealed class Reading : AggregateRoot<ReadingId, ReadingState>
{
    private Reading(ReadingState state) : base(state) { }
}

// Compiles cleanly.
[AggregateName("reading")]
public sealed class Reading : AggregateRoot<ReadingId, ReadingState>
{
    private Reading(ReadingState state) : base(state) { }
}
```

```csharp
// THSS0005: ReadingState names Reading2State as its own TSelf.
public sealed record ReadingState(ReadingId Id) : AggregateState<Reading2State, ReadingId>
{
    public override Reading2State Apply(IDomainEvent domainEvent) => new(Id);
}

// Compiles cleanly.
public sealed record ReadingState(ReadingId Id) : AggregateState<ReadingState, ReadingId>
{
    public override ReadingState Apply(IDomainEvent domainEvent) => this;
}
```

## Limits

- **`netstandard2.0` only**, because a Roslyn analyzer runs inside whatever compiler loaded it, not
  inside your project's own target framework. This is the one package in the family not built on
  `net10.0`.
- Six rules, deliberately. See "What this deliberately does not check" above for what stays a
  startup check on purpose.


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
