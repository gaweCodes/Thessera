# 0004 — One store per host

- **Status:** Superseded by [0016](0016-one-store-per-aggregate-not-per-host.md) for a host that
  claims aggregates for more than one store; unchanged for the single-store host, which remains the
  common case.
- **Date:** 2026-08-29

## Context

A command is committed once, by the unit of work, in the transaction that also writes the outbox.
That single transaction is what makes "the aggregate was saved" and "its events will be published"
one decision rather than two.

A second write store in the same host would mean a second transaction. Nothing would coordinate
them, so a command touching both could commit one and fail the other — and the outbox guarantee
would hold for whichever store happens to own it and silently not for the other.

## Decision

A host selects exactly one persistence strategy. `UsePersistence` may be called repeatedly with the
same adapter, but a second, different one throws in `PersistenceSelection.Select`.
`UseNoPersistence()` is the explicit third option and cannot be combined with a store either.

Saying nothing at all is also an error, but a different one: a host whose scanned assemblies contain
commands, with neither a store nor an `IUnitOfWork` of its own, fails at startup
(`UnitOfWorkPresenceCheck`) — because every one of those commands would otherwise report success
while nothing was committed, and nothing at run time would say so.

## Consequences

- A service that genuinely needs two write databases is two services. That is the intended reading,
  not a limitation to work around.
- `UseNoPersistence()` is not ceremony. It is the difference between "no store has been chosen yet"
  and "this host deliberately commits nothing", and only the second is safe to start.
- Because the store is a per-host decision while the aggregate style is a per-type one
  ([0001](0001-one-domain-model-two-stores.md)), the two can conflict. `WithoutEventHistory()` is
  the deliberate waiver for the one direction that is survivable — an event-sourced aggregate on a
  state store, where the state and version are still written correctly and the outbox is still fed,
  but no stream is kept. That loss is silent and permanent, which is why waiving it takes an
  explicit call rather than a configuration default.
- The waiver is itself constrained: `WithoutEventHistory()` on an event store, or without any
  store, is an error. It waives a history that in those cases is either the one being written or
  one that never existed.

## Alternatives considered

**Allow several stores and put the outbox on one of them.** Rejected because the delivery guarantee
would then be true for part of the system and false for the rest, with nothing in the API marking
the difference. A guarantee that holds sometimes is harder to reason about than one that does not
exist.

**Make the mismatch between store and aggregate style a warning instead of an error.** Rejected:
the failure it prevents is silent, permanent loss of history, and a warning in a log is not a
guard against that. The escape hatch exists — it is just spelled as a method call in the host's own
composition, where someone has to mean it.

**Coordinate two stores with a distributed transaction.** Rejected without much deliberation: it
buys a capability nobody asked for at a cost — operational and conceptual — that dwarfs the problem
of running two services.
