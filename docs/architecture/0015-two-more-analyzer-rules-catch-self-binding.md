# 0015 — Two more analyzer rules catch the aggregate- and entity-state self-binding mistake

- **Status:** Accepted
- **Date:** 2026-09-02

## Context

[0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md) rejected a fifth rule for
`AggregateState<TSelf, TKey>`'s self-reference on the grounds that "the C# generic constraint system
already refuses to compile a mismatch here." That claim is too broad. The constraint
`where TSelf : AggregateState<TSelf, TKey>` is checked against whatever type is named as `TSelf`, not
against the type doing the naming: two sibling states that both close the same `TKey` each already
satisfy the other's constraint, so a declaration such as

```csharp
public sealed record FooState : AggregateState<BarState, SharedKey> { /* ... */ }
```

compiles cleanly whenever `BarState` already binds itself correctly to `SharedKey` — the exact case a
copy-pasted state declaration produces. The runtime already carries a check for exactly this,
`AggregateStateSelfBindingCheck`, whose own comment says as much: a mismatched `TSelf` "compiles" and
"fails as an unexplained `InvalidCastException` the first time the aggregate applies an event." That
check, however, only walks the assemblies scanned for aggregates — it inspects
`AggregateState<TSelf, TKey>` and never `EntityState<TSelf, TKey>`, so a child entity state can name
the wrong sibling with nothing catching it at any stage, runtime included.

Both mistakes are a single symbol on a single type declaration: the type's own identity against the
first type argument of whichever base it closes. Neither depends on wiring, DI registration, or
anything outside the compilation unit being analyzed — the same bar [0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md)
set for what belongs in this package.

## Decision

`GaWeCodes.Thessera.Analyzers` gains two more `DiagnosticAnalyzer` implementations:

- `THSS0005` (`AggregateStateSelfBindingAnalyzer`) — a non-abstract type deriving from
  `AggregateState<TSelf, TKey>` whose first type argument does not name the deriving type itself.
- `THSS0006` (`ChildEntityStateSelfBindingAnalyzer`) — the same check for a non-abstract type
  deriving from `EntityState<TSelf, TKey>`.

Both walk the base-type chain for the nearest type closing `AggregateState<,>` or `EntityState<,>`
respectively and compare its first type argument against the type under analysis, mirroring the walk
`AggregateStateSelfBindingCheck` performs by reflection. Both are `Error` severity, enabled by
default, category `Thessera.Design` — the same defaults as `THSS0001`–`THSS0004`.

## Consequences

- A mismatched `TSelf` on either state type is now a build error and a red squiggle while the state
  is being written, instead of an `InvalidCastException` the first time the aggregate (or, for a
  child entity state, nothing at all) applies an event.
- `THSS0006` closes a gap the runtime check never covered: `EntityState<TSelf, TKey>` mismatches were
  previously undetected at every stage. This analyzer is, for that one case, not a compile-time twin
  of an existing check but new coverage entirely.
- [0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md)'s own "Alternatives considered"
  section still states that the compiler already prevents this. That text is left as written — records
  are append-only — but this record is the correction: the compiler prevents a mismatched `TKey`,
  never a same-`TKey` mix-up between sibling states.
- No project under `src` needs to change: every existing `AggregateState<TSelf, TKey>` and
  `EntityState<TSelf, TKey>` already binds to itself.
- `AggregateStateSelfBindingCheck` is not removed or narrowed. It still runs at startup against the
  assemblies a host actually scans, which covers a state built outside the analyzed compilation (a
  referenced package compiled before this analyzer existed, for instance) — the same reasoning
  [0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md) gives for keeping every runtime
  check it does not fully replace.

## Alternatives considered

- **Extending `AggregateStateSelfBindingCheck` to also walk `EntityState<TSelf, TKey>`.** Rejected as
  the narrower fix: it would still only fire for the assemblies a host scans, and not for a state a
  test project builds in isolation. The whole point of this package is catching it earlier than that.
- **One analyzer for both `AggregateState<,>` and `EntityState<,>`.** Rejected for the same reason
  `THSS0003` and `THSS0004` stay separate analyzers rather than one parameterized by base type: two
  small, independently understandable rules read better than one generalized over a distinction
  (aggregate vs. child) that matters to the person reading the diagnostic.
