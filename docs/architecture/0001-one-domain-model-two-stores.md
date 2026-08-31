# 0001 — One domain model, two stores

- **Status:** Accepted
- **Date:** 2026-08-29

## Context

A tactical DDD library normally makes the persistence style part of the model. An aggregate written
for an event store carries replay in its base class; one written for a state store does not. The
choice therefore happens on the first day, with the least information anyone will ever have about
the context, and moving afterwards is a rewrite of the domain rather than a change of
infrastructure.

The bet behind this family is that the aggregate a developer writes should not have to know which
store sits underneath it, and that the store should be a wiring decision made — and revisited — by
the host.

## Decision

The same domain model runs on either of two stores: a state store
(`GaWeCodes.Thessera.Persistence.EfCore.Postgres`, selected with `UseEfCoreStateStore<TContext>`)
or an event store (`GaWeCodes.Thessera.Persistence.Marten`, selected with `UseMartenEventStore`).
The host names one of them and nothing else changes.

`AggregateStyle` has exactly two members, `StateStored` and `EventSourced`. An aggregate's style is
not configured: it is read from its base class, by whether the type implements
`IEventSourcedAggregateRoot<TKey>`. A store declares the style it supports through
`IPersistenceAdapter.AggregateStyle`.

## Consequences

- Moving a context between the two stores is a change of one `Use*` call.
  `SameAggregateOverBothStoresTests` drives one aggregate through both and compares the result,
  so the claim is tested rather than asserted in prose.
- The two directions are **not** symmetric, and this is the part that surprises people. An
  `EventSourcedAggregateRoot` runs on both stores. A plain `AggregateRoot` runs on the state store
  only, because an event store has to replay history to reconstitute an aggregate and that base
  class keeps none.
- Because style is per type and the store is per host, the two can disagree. That disagreement has
  to be caught rather than tolerated — `AggregatePersistenceMatchCheck` does it at startup, and
  `WithoutEventHistory()` is the deliberate waiver. See [0004](0004-one-store-per-host.md).
- Every store adapter has to fit the same seam (`IPersistenceAdapter`,
  `PersistenceRegistrationContext`), which limits what a store may expose. A feature only one store
  can offer has no place to surface.

## Alternatives considered

**Two separate libraries, one per persistence style.** By far the cheapest to build and the easiest
to explain. It is also precisely the outcome this family exists to avoid: the choice returns to day
one, and the second library's users are the ones who guessed wrong.

**One repository abstraction that hides the style completely.** Rejected because the styles differ
in what they can actually do — replay, audit, inspection as of an earlier point in time. Hiding
that difference does not remove it; it converts an explicit error into silent, permanent data loss
the day someone runs an event-sourced aggregate on a state store without knowing.

**Letting the state store synthesize a stream from state changes.** Rejected: a stream reconstructed
from current state is not the history. It would be indistinguishable from a real one at the API
while being unable to answer the questions a history exists to answer.
