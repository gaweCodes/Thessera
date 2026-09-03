# 0016 — One store per aggregate, not one store per host

- **Status:** Accepted
- **Date:** 2026-09-15

## Context

[0004](0004-one-store-per-host.md) decided that a host selects exactly one persistence strategy,
because a command commits once, in one transaction, and a second write store would mean a second
transaction that nothing coordinates. That reasoning is correct about the *command*: it still
commits exactly once. It was, however, applied to the wrong scope — the whole host — when the
actual constraint is narrower: one command, one store. Nothing about a second store per se breaks
the single-transaction guarantee, as long as no single command ever needs both.

Wolverine, whose outbox this family's own `Wolverine` package builds on, already draws that
narrower line. Marten supports several `IDocumentStore`s in one process through
`[MartenStore(typeof(IX))]` / `[Storage(typeof(IX))]`, resolved *per handler*, not per host. EF Core
never needed an equivalent attribute, because the `DbContext` type a handler asks for already picks
its store implicitly — two different `DbContext` types simply coexist. Wolverine's actual invariant,
in other words, is "one store per command", never "one store per host"; the latter was this
family's own, unnecessarily strict, reading.

The consumer-visible cost of the stricter reading is real: a service that has one aggregate for
which event history matters and a dozen others for which it does not is forced to either run two
services, or write every aggregate to the same store and accept `WithoutEventHistory()`'s silent,
permanent loss of history for the ones that did not need to make that trade.

## Decision

A host may select more than one persistence store, as long as every aggregate is claimed by at most
one of them and every command reaches only aggregates claimed by the same store. `UsePersistence`
takes an optional trailing `params Type[] forAggregates`; so do `UseEfCoreStateStore` and
`UseMartenEventStore`. A call with no aggregates listed is the **main** store — the implicit,
catch-all choice every aggregate falls into unless a later call claims it — and at most one main
store may be selected, unchanged from [0004](0004-one-store-per-host.md)'s single-store host. A
call that does list aggregates is an **ancillary** store, claiming exactly those aggregates and no
others; claiming the same aggregate from two calls, in any combination of main and ancillary, is
still the error `PersistenceSelection.Select` reported before this record, just resolved against
the wider set of choices.

`PersistenceSelection` becomes a routing table — a list of `PersistenceChoice`, one per selected
store, each carrying its claimed aggregates and, for an ancillary store, a `StoreId` used to key its
registrations — instead of the single `Choice` this family carried before. `AggregatePersistenceMatchCheck`
resolves each aggregate's owning choice through it and validates the match exactly as before, once
per choice rather than once globally. The main store keeps its original, unkeyed registration —
open-generic `IRepository<,>`, unkeyed `IUnitOfWork` — so a single-store host is wired identically
to before this record, byte for byte. An ancillary store instead registers a closed-generic
`IRepository<TAggregate, TKey>` per claimed aggregate (reflected through the new
`AggregateKeyType.Of` helper) and a **keyed** `IUnitOfWork`, keyed by its `StoreId`.

Routing a command to its store is a startup-time concern, not a per-request lookup: a new
`CommandStoreRoutingCheck` — a no-op whenever one store or none is selected, so it costs nothing in
the common case — walks every registered `ICommandHandler<>` / `ICommandHandler<,>` implementation
once, reflects the aggregate types its constructor's `IRepository<,>` parameters name, resolves each
to its owning choice, and throws the same way `AggregatePersistenceMatchCheck` already does if a
single handler's repositories resolve to more than one store. Otherwise it records, in the new
`CommandStoreRouter` singleton, which command types route to which `StoreId`. `UnitOfWorkBehavior`
resolves its `IUnitOfWork` from `IServiceProvider` at construction, asking the router for the
current command's `StoreId` and requesting the unkeyed service when it is `null` (main store, or the
single-store case unchanged from before) and the matching keyed service otherwise.

The "one command, one store" invariant `CommandStoreRoutingCheck` enforces at startup is also given
a compile-time twin: `THSS0007` (`CommandHandlerSingleStoreAnalyzer`) flags a command handler whose
constructor injects `IRepository<TAggregate, TKey>` for more than one aggregate, unconditionally —
independent of how many stores the host under edit actually selects, since a handler shaped that way
cannot be routed to one store the moment a second one is ever added.

## Consequences

- A single-store host — still the overwhelmingly common case — is registered, resolved and dispatched
  exactly as before this record: no new indirection, no keyed lookup, no routing check runs.
- A host may now genuinely mix an event-sourced and a state-stored aggregate, the case
  [0001](0001-one-domain-model-two-stores.md) already made the domain model agnostic to but this
  family's own wiring refused. `WithoutEventHistory()` remains available and unchanged for the
  aggregates that stay on a shared store; ancillary stores are the alternative for the aggregates
  that do not.
- The single-transaction guarantee this record depends on is unchanged: it was always scoped to one
  command, and a command still resolves to exactly one `IUnitOfWork`, main or keyed.
- `PersistenceRegistrationContext`'s constructor gains `storeId` and `claimedAggregates` — a breaking
  change to an already-shipped public constructor, tracked in `PublicAPI.Shipped.txt` like any other.
  An adapter author who calls it directly for the main store passes `PersistenceChoice`'s
  `StoreId`/no claimed aggregates unchanged; nothing else about writing an adapter changes.
  `AggregateKeyType.Of` is now public for the same reason `AggregateFactory` already is: an adapter
  needs it to build a closed-generic repository registration for a claimed aggregate, and no adapter
  lives in this assembly.
- `THSS0007` does not replace `CommandStoreRoutingCheck`. The analyzer catches the shape of a handler
  from its source alone and reports it even in a single-store host, before the mistake can compound;
  the runtime check is still what confirms, against the assembled DI container, which store each
  aggregate actually resolved to.

## Alternatives considered

**Keep [0004](0004-one-store-per-host.md) as written and tell a mixed-persistence consumer to run two
services.** This was the family's position until this record. Rejected once Wolverine's own
ancillary-store precedent made clear the single-transaction guarantee this family actually depends
on was never a single-*host* guarantee — a second service was a heavier answer than the constraint
required.

**Route a command to its store per request, via a marker interface or attribute on the command
itself**, mirroring Wolverine's `[MartenStore]` / `[Storage]` attributes more literally. Rejected:
this family's commands are plain records with no dependency on a persistence package
([0002](0002-core-carries-no-runtime.md)), and a command already implies exactly one store through
the aggregate(s) its handler touches — reflecting the handler once at startup gets the same answer
without adding a persistence-flavoured attribute to `Application`.

**Make `CommandStoreRoutingCheck` always run, even for a single-store host.** Rejected on cost: the
check exists to catch a mistake that is only reachable once a second store is selected, and running
it unconditionally would mean every host pays a reflection pass over every handler for a guarantee
the single-store case already gets for free from `AggregatePersistenceMatchCheck` alone.

**Leave the "one command, one store" rule to the runtime check alone, without `THSS0007`.** Rejected
for the same reason [0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md) added a
compile-time twin for four other runtime checks: a handler spanning two aggregates is visible from
its constructor alone, and today it only fails once someone adds a second store — which may be long
after the handler that will not survive it was written.
