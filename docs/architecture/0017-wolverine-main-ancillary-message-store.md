# 0017 — Wolverine's own Main/Ancillary message store, mapped from ours

- **Status:** Accepted
- **Date:** 2026-09-16

## Context

[0016](0016-one-store-per-aggregate-not-per-host.md) let a host select more than one persistence
store, each owning a disjoint set of aggregates, and reasoned about the seam entirely in this
family's own vocabulary: a **main** store (no `forAggregates`, catch-all) and one or more
**ancillary** stores (named aggregates only). That record noted, in passing, that Wolverine already
draws a narrower line than "one store per host" — but it did not say that Wolverine's own outbox
has an unrelated, *second* main/ancillary distinction, one level down, that 0016 never touched: not
about which aggregate belongs to which store, but about which store's tables hold the durable
inbox/outbox envelopes for messages nobody enrolled elsewhere. Wolverine allows exactly one message
store tagged `Main` per host; every other one must be `Ancillary` and explicitly enrolled against
the write context whose messages belong to it, or the host fails to start with "There must be
exactly one message store tagged as the 'main' store".

Building `Examples/MixedPersistence` — the first host in this repository to ever actually start
with two persistence adapters registered at once — surfaced this immediately: `MartenPersistenceAdapter`
calls the plain `AddMarten(...).IntegrateWithWolverine()`, which Wolverine hard-codes to `Main` with
no configuration option to change it; `EfCorePersistenceAdapter<TContext>` called
`options.PersistMessagesWithPostgresql(connectionString)`, Wolverine's own default, also `Main`. Two
stores, each unconditionally claiming the same single slot, on the same connection. No existing test
before this example ever started a host with two persistence adapters registered together — every
matrix host and every unit test exercises exactly one — so the conflict had no chance to surface
until a real mixed host tried to run.

Two further facts shaped the fix. First, Wolverine's own architecture makes Marten's side of this
non-negotiable: the *only* way to make a Marten-backed message store `Ancillary` is
`AddMartenStore<TStoreMarker>().IntegrateWithWolverine()`, which requires a marker `IDocumentStore`
interface the consumer must declare — a materially bigger change than this fix's scope, and not
required by anything 0016 promised. Second, `PersistenceRegistrar` calls each selected adapter's
`Register` eagerly, in fluent-chain order, while `MartenPersistenceAdapter.Register` claims `Main`
eagerly too (`AddMarten(...).IntegrateWithWolverine()` runs immediately); an EF Core store, by
contrast, already deferred its own outbox wiring to `Activate()`, once, at the end of `AddThessera`,
through `EfCoreOutboxDurability : IOutboxDurabilityConfigurator`. That existing deferral is what
makes an order-independent decision possible at all: by the time `Activate()` runs, every adapter's
`Register` — Marten's included — has already executed, regardless of which `Use*` call was written
first.

## Decision

Wolverine's Main/Ancillary message-store role is decided per host, independently of which store
0016 designates as *this family's* main aggregate store, using one rule: **Marten, if selected, is
always Wolverine's Main message store — it has no other option — and every EF Core store defers its
own claim to `Activate()` time, taking Main only if nothing has claimed it yet, falling back to
Ancillary otherwise.** With one EF Core store and no Marten, that store still claims Main on its
first (and only) attempt, so a single-store host's Wolverine wiring is unchanged from before this
record, byte for byte.

`WolverineRuntimeActivator` — shared, one per host, reached by every adapter through
`context.UseWolverineRuntime()` — gains `TryClaimMainMessageStore()`: it returns `true` once, to
whichever caller asks first, and `false` after. `MartenPersistenceAdapter.Register` calls it and
discards the result — Marten's own registration already ran unconditionally as Main a few lines
above; the call exists only to announce that fact to whoever asks later.
`EfCoreOutboxDurability.Configure`, which now also carries the shared `WolverineRuntimeActivator`
and the write context's `Type`, calls it inside `Activate()`'s deferred loop and branches on the
result:

```csharp
var role = runtime.TryClaimMainMessageStore() ? MessageStoreRole.Main : MessageStoreRole.Ancillary;
driver.PersistMessages(options, writeConnectionString, role, role == MessageStoreRole.Main ? null : contextType);
```

`IEfCoreDatabaseDriver.PersistMessages` gained the `role` and `enrollContextType` parameters to
carry this decision to the vendor-specific call. `PostgresDatabaseDriver`'s `Main` branch is the
original, unchanged `options.PersistMessagesWithPostgresql(connectionString)`; its `Ancillary`
branch derives a schema from the enrolled context's own name —
`wolverine_<writecontexttypename>`, lower-cased, since Wolverine rejects anything else — and calls
`options.PersistMessagesWithPostgresql(connectionString, schemaName, MessageStoreRole.Ancillary).Enroll(enrollContextType)`.
The derived schema, not a name the consumer chooses, is deliberate: it only has to be distinct from
whatever the Main store already claimed on the same connection, and deriving it removes one more
thing a consumer wiring a mixed host would otherwise have to get right.

If two EF Core stores ever share a host with no Marten, the same rule generalizes without change:
whichever one's deferred configurator runs first in `Activate()`'s loop claims Main, the rest fall
back to Ancillary, each with its own derived schema — an extension of 0016's own main/ancillary
split, arrived at for free rather than designed separately.

## Consequences

- A single-store host, EF Core or Marten, is wired identically to before this record — no schema
  change, no new call, no migration. Only a host that actually selects two or more stores is
  affected, matching 0016's own stated consequence for its half of this same seam.
- `IEfCoreDatabaseDriver.PersistMessages`'s signature change is a breaking change to an already-shipped
  public member, tracked in `GaWeCodes.Thessera.Persistence.EfCore`'s `PublicAPI.Shipped.txt` like
  any other — this family is still `preview`, so the change lands directly rather than through a
  deprecation cycle. `WolverineRuntimeActivator.TryClaimMainMessageStore()` is a purely additive
  member on the same public type 0002's `IRuntimeActivator` seam already exposes.
- A driver author implementing `IEfCoreDatabaseDriver` for a database other than PostgreSQL must now
  handle both roles to compile at all; the `README`s for `GaWeCodes.Thessera.Persistence.EfCore` and
  `GaWeCodes.Thessera.Persistence.EfCore.Postgres` carry the updated shape.
- This is a second, independent main/ancillary distinction living beside 0016's — one about
  Wolverine's own delivery guarantees, the other about which aggregate belongs to which store. A
  reader of 0016 alone would reasonably assume it was the only one; this record exists so that
  assumption is corrected in writing rather than only in the code.
- `Examples/MixedPersistence` is the first host in this repository that actually starts with two
  persistence adapters registered together, and its test suite — run against real PostgreSQL — is
  what caught this, together with an unrelated, already-fixed duplicate `thessera-dead-letters`
  health-check registration bug the same host exposed. Neither bug had a chance to surface before,
  because no test until this example ever started a host wired that way.

## Alternatives considered

**Make Marten's message store Ancillary instead of EF Core's**, via
`AddMartenStore<TStoreMarker>().IntegrateWithWolverine()`. Rejected on scope: it requires a marker
`IDocumentStore` interface the consumer declares, a change to `MartenPersistenceAdapter`'s own shape
disproportionate to what this record needed to fix, and Wolverine's plain `AddMarten` path — the one
`MartenPersistenceAdapter` already uses and that every existing Marten-only host depends on —
supports no other role regardless.

**Let the consumer choose which store is Wolverine's Main explicitly**, e.g. a new parameter on
`UseEfCoreStateStore`/`UseMartenEventStore`. Rejected: the "first store to claim it wins, Marten
always eagerly" rule already produces the only sensible outcome given Marten's hard constraint, so a
consumer-facing switch would only ever be set one way — an option nobody needs is not a feature, it
is a footgun waiting for a host that sets it wrongly.

**Give every EF Core store an explicit, always-on schema, decided at `Register()` time regardless of
role.** Rejected: it would change the Wolverine schema of every existing single-store EF Core
deployment, a real migration cost for zero benefit in the common case; deferring the schema decision
to the same `Activate()`-time branch that already decides the role keeps a single-store host
untouched.
