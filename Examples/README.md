# Examples

These examples form a six-step adoption ladder for Thessera.

- [DomainOnly](DomainOnly/README.md) is a plain console CRUD app with a hand-written domain object and an in-memory dictionary.
- [DomainApplication](DomainApplication/README.md) keeps the in-memory approach but adopts Thessera's application and domain contracts.
- [StateStored](StateStored/README.md) moves persistence to PostgreSQL through `GaWeCodes.Thessera.Persistence.EfCore.Postgres`.
- [EventSourced](EventSourced/README.md) swaps the state store for Marten-backed event sourcing.
- [StateStoredWithMessaging](StateStoredWithMessaging/README.md) adds RabbitMQ publishing plus a background listener that writes `received-events.log`.
- [EventSourcedWithMessaging](EventSourcedWithMessaging/README.md) combines Marten event sourcing with the same RabbitMQ round-trip logging.

Beyond the ladder, [MixedPersistence/](MixedPersistence/) groups two examples that prove the
ladder's two store choices are no longer exclusive: one host runs a state-stored aggregate as its
main store and an event-sourced aggregate as an ancillary store side by side, as introduced in
[ADR 0016](../docs/architecture/0016-one-store-per-aggregate-not-per-host.md).

- [MixedPersistence](MixedPersistence/MixedPersistence/README.md) is the plain two-store host.
- [MixedPersistenceWithMessaging](MixedPersistence/MixedPersistenceWithMessaging/README.md) adds
  RabbitMQ publishing on top of the same two-store host, mirroring what tiers 5-6 add to tiers 3-4.

Every tier that defines a Thessera aggregate (all six, plus both MixedPersistence examples) also
references `GaWeCodes.Thessera.Analyzers` as a `PrivateAssets="all"` build-time dependency - it turns
the eight most common misconfigurations (a missing `[AggregateName]` or `[EventName]`, a non-private
aggregate constructor, a non-internal child entity constructor, an aggregate- or entity-state that
names the wrong type as itself, a command handler that depends on more than one aggregate's
repository, and a command handler that bypasses the unit of work by injecting a raw
`DbContext`/`IDocumentSession` instead of `IRepository<,>`) into a compiler error instead of a
message the first affected host prints at boot, or in the state-naming case, instead of an
`InvalidCastException` on the first applied event. It applies regardless of which store or broker a
tier picks.

Build tiers 2-6 and both MixedPersistence examples only after refreshing the local package feed:

```powershell
dotnet pack .\Thessera.slnx -c Release -o C:\temp\thessera-local-feed
```

Every Thessera package reference in tiers 2-6 is pinned to `$(ThesseraVersion)`, a single property
in [`Directory.Build.props`](Directory.Build.props). MinVer derives the version from the nearest
reachable Git tag, and that string changes on every commit once `HEAD` moves past a tag - so after
refreshing the feed, check the `.nupkg` file names it produced and update `ThesseraVersion` to
match if they differ. A stale value fails restore with `NU1102` once the feed no longer carries the
old version, rather than silently building against the wrong one.

Docker is required for tiers 3-6 and both MixedPersistence examples because those examples run
against real PostgreSQL containers in tests, and tiers 5-6 plus MixedPersistenceWithMessaging also
need RabbitMQ.
