# 0014 — A compile-time analyzer catches four of the startup checks

- **Status:** Accepted
- **Date:** 2026-09-01

## Context

Four Thessera conventions were, before this record, verifiable in exactly two ways: at runtime, when
the composition root's startup checks or the event-catalogue build first touched the offending type,
or in a test, if a project happened to reference `GaWeCodes.Thessera.Testing` and call
`AggregateConventions.Verify`. Neither is available to the person writing the type: an aggregate
missing `[AggregateName]`, a domain event missing `[EventName]`, an aggregate whose parameterless
constructor is absent or public, and a child entity with a public constructor all compile cleanly and
fail only later — at the first host start, or the first CI run of a test nobody is required to have
written.

`plan.md` for the VitalSync project, the first consumer to adopt this family end to end, named this
gap explicitly and proposed a Roslyn analyzer package as its last remaining phase, deliberately
scheduled after the family's first release and after VitalSync's own migration — so that the analyzer
would be validated against a real, already-working consumer rather than designed in the abstract
against the family's own test fixtures.

## Decision

A new package, `GaWeCodes.Thessera.Analyzers`, ships four `DiagnosticAnalyzer` implementations,
`THSS0001`–`THSS0004`, each the compile-time twin of one of the four checks above:

- `THSS0001` (`AggregateNameAnalyzer`) — a non-abstract type deriving from
  `AggregateRoot<TKey, TState>` (directly, or through `EventSourcedAggregateRoot<TKey, TState>`)
  without `[AggregateName]` declared directly on it.
- `THSS0002` (`DomainEventNameAnalyzer`) — a non-abstract type implementing `IDomainEvent` without
  `[EventName]` declared directly on it.
- `THSS0003` (`AggregateConstructorAnalyzer`) — a non-abstract type deriving from
  `AggregateRoot<TKey, TState>` with no parameterless constructor, or with one that is `public`.
- `THSS0004` (`ChildEntityConstructorAnalyzer`) — a non-abstract type deriving from
  `Entity<TKey, TState>` that exposes any `public` constructor.

All four are `Error` severity and enabled by default, category `Thessera.Design`. The package targets
`netstandard2.0` — the one exception to the family's `net10.0` target, because an analyzer runs
inside the compiler process, not inside the consumer's build output — and packs its assembly under
`analyzers/dotnet/cs`, never under `lib/`. It carries no reference, at compile time or at run time, to
`GaWeCodes.Thessera.Domain`: every rule resolves `AggregateRoot<TKey, TState>`, `Entity<TKey, TState>`,
`IDomainEvent`, `AggregateNameAttribute` and `EventNameAttribute` by metadata name against the
compilation it is handed, and does nothing when a name resolves to nothing. That is what lets the
package be referenced from every host regardless of what else that host references, including a host
that references none of the above at all.

## Consequences

- Four violations that previously surfaced at a host's first start, or not at all, now fail
  `dotnet build` and appear as a red squiggle while the type is being written.
- Neither the runtime startup checks (`AggregatePersistenceMatchCheck`, `HandlerRegistrationCheck`)
  nor `AggregateConventions.Verify` in `GaWeCodes.Thessera.Testing` are replaced. The runtime checks
  reason about the assembled DI container and about persistence-strategy selection — neither is
  knowable from source alone — and `AggregateConventions.Verify` remains the way a project pins that
  its whole aggregate surface follows every convention in one assertion, including the two this
  analyzer does not attempt: persisted-schema drift and the vacuous-assembly guard. A test project
  that deliberately exercises a *violating* fixture — as several already do, to prove
  `AggregateConventions.Verify` reports it — must not reference this analyzer package, or the fixture
  would fail to compile before the test describing its failure could run.
- `Examples/` references the package from all six example projects, regardless of which store or
  broker each one demonstrates, because all six declare aggregates.
- No project under `src` needs to change to satisfy the four rules: every existing aggregate, entity
  and domain event already follows the conventions the analyzer checks, since they are the same
  conventions `AggregateConventions.Verify` already checks in `tests/GaWeCodes.Thessera.Testing.Tests`.
- Extending this package to the checks it deliberately does not cover — a persistence strategy that
  does not match its store, a handler registered for an aggregate the DI container never sees, or the
  `AggregateState<TSelf, TKey>` self-reference the compiler's own generic constraints already refuse
  — is left to a later record. Nothing here forecloses that; it is simply not what four hand-written
  rules should grow into without their own justification.

## Alternatives considered

- **`Microsoft.CodeAnalysis.Testing` for the test project.** The family otherwise adds no assertion
  library beyond xUnit's own (see [0012](0012-xunit-built-in-asserts.md)), and a hand-rolled harness —
  compile a snippet with `CSharpCompilation.Create`, run the analyzer under test with
  `WithAnalyzers`, and assert on the returned diagnostics directly — covers four rules without a new
  package dependency. `Microsoft.CodeAnalysis.Testing` remains an option if a future rule needs code
  fixes or more elaborate fixture management than this harness offers.
- **Checking the store/aggregate persistence-strategy match at compile time.** Rejected for this
  version: which store a host selects is a DI-time decision, not visible in the syntax or symbols of
  any single compilation unit. `AggregatePersistenceMatchCheck` remains the place this is verified.
- **A rule for the `AggregateState<TSelf, TKey>` self-reference.** Rejected as it would duplicate a
  check the C# generic constraint system already performs for free: a mismatched `TSelf` simply does
  not compile.
- **Doing nothing, and leaving the gap to `AggregateConventions.Verify` alone.** Rejected because a
  convention test only fires if a project both references `GaWeCodes.Thessera.Testing` and someone
  wrote the assertion — the failure mode `plan.md` named was exactly a violation reaching a running
  host because neither had happened yet.
