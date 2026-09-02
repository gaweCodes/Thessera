# Examples

These examples form a six-step adoption ladder for Thessera.

- [DomainOnly](DomainOnly/README.md) is a plain console CRUD app with a hand-written domain object and an in-memory dictionary.
- [DomainApplication](DomainApplication/README.md) keeps the in-memory approach but adopts Thessera's application and domain contracts.
- [StateStored](StateStored/README.md) moves persistence to PostgreSQL through `GaWeCodes.Thessera.Persistence.EfCore.Postgres`.
- [EventSourced](EventSourced/README.md) swaps the state store for Marten-backed event sourcing.
- [StateStoredWithMessaging](StateStoredWithMessaging/README.md) adds RabbitMQ publishing plus a background listener that writes `received-events.log`.
- [EventSourcedWithMessaging](EventSourcedWithMessaging/README.md) combines Marten event sourcing with the same RabbitMQ round-trip logging.

Every tier that defines a Thessera aggregate (all six) also references
`GaWeCodes.Thessera.Analyzers` as a `PrivateAssets="all"` build-time dependency - it turns the six
most common misconfigurations (a missing `[AggregateName]` or `[EventName]`, a non-private
aggregate constructor, a non-internal child entity constructor, and an aggregate- or entity-state
that names the wrong type as itself) into a compiler error instead of a message the first affected
host prints at boot, or in the state-naming case, instead of an `InvalidCastException` on the first
applied event. It applies regardless of which store or broker a tier picks.

Build tiers 2-6 only after refreshing the local package feed:

```powershell
dotnet pack .\Thessera.slnx -c Release -o C:\temp\thessera-local-feed
```

Every Thessera package reference in tiers 2-6 is pinned to `$(ThesseraVersion)`, a single property
in [`Directory.Build.props`](Directory.Build.props). MinVer derives the version from the nearest
reachable Git tag, and that string changes on every commit once `HEAD` moves past a tag - so after
refreshing the feed, check the `.nupkg` file names it produced and update `ThesseraVersion` to
match if they differ. A stale value fails restore with `NU1102` once the feed no longer carries the
old version, rather than silently building against the wrong one.

Docker is required for tiers 3-6 because those examples run against real PostgreSQL containers in tests, and tiers 5-6 also need RabbitMQ.
