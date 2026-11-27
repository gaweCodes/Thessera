# 0020 — An aggregate's parameterless constructor must be private

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

An aggregate keeps a parameterless constructor for one caller: the repository, which builds an
empty hull through it and then replays or restores the state. Everyone else is meant to go through
a named factory method, which raises the event that creates the aggregate and so checks its rules.
The Domain README has always stated the rule as "the parameterless constructor exists and is
**private**".

What enforced it was weaker. `THSS0003` ([0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md))
and `AggregateConventions.Verify` flagged a missing parameterless constructor and a `public` one,
and let every other accessibility through. An `internal`, `protected`, `protected internal` or
`private protected` constructor compiled cleanly and passed the convention test, although any of
them lets code outside the aggregate create a hull that never went through its factory — an
`internal` one to the whole domain assembly. No record says the gap was chosen; 0014 simply
describes the rule as "missing or public".

Every aggregate in this repository's examples, test fixtures and the VitalSync samples already
declares the constructor `private`. The repository reaches it through reflection, so nothing in the
runtime needs more than private.

## Decision

`THSS0003` and `AggregateConventions.Verify` flag a parameterless aggregate constructor whose
accessibility is anything other than `private`. The analyzer names the accessibility it found. The
check that the constructor exists, and the runtime check while a host is composed, are unchanged.

This narrows the `THSS0003` description in 0014 from "missing or public" to "missing or not
private"; the rest of 0014 stands.

## Consequences

- The documented rule and the enforced rule are the same again; the Domain README no longer has to
  explain a gap between them.
- A consumer whose aggregate declares a non-private parameterless constructor gets a build error
  after the upgrade, and a failing convention test. That is a breaking change and is called out in
  the changelog. The fix is to make the constructor `private`; no stored data, schema or event
  changes with it.
- A concrete aggregate can no longer be subclassed through its parameterless constructor. An
  abstract base is not checked and may keep a `protected` one, so shared behaviour between
  aggregates still has a place to live.

## Alternatives considered

**Loosen the documentation to "not public".** Rejected: it keeps a hole the factory method exists
to close, for no case anyone in this repository or its consumers relies on.

**Allow `private protected` as well.** Rejected: it only matters for a concrete aggregate that is
itself subclassed inside the same assembly, which then needs an `[AggregateName]` and a store
mapping of its own — a shape nothing here uses and the abstract-base route already covers.
