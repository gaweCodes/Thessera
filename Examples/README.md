# Examples

These examples form a six-step adoption ladder for Thessera.

- [DomainOnly](DomainOnly/README.md) is a plain console CRUD app with a hand-written domain object and an in-memory dictionary.
- [DomainApplication](DomainApplication/README.md) keeps the in-memory approach but adopts Thessera's application and domain contracts.
- [StateStored](StateStored/README.md) moves persistence to PostgreSQL through `GaWeCodes.Thessera.Persistence.EfCore.Postgres`.
- [EventSourced](EventSourced/README.md) swaps the state store for Marten-backed event sourcing.
- [StateStoredWithMessaging](StateStoredWithMessaging/README.md) adds RabbitMQ publishing plus a background listener that writes `received-events.log`.
- [EventSourcedWithMessaging](EventSourcedWithMessaging/README.md) combines Marten event sourcing with the same RabbitMQ round-trip logging.

Build tiers 2-6 only after refreshing the local package feed:

```powershell
dotnet pack .\Thessera.slnx -c Release -o C:\temp\thessera-local-feed
```

Docker is required for tiers 3-6 because those examples run against real PostgreSQL containers in tests, and tiers 5-6 also need RabbitMQ.
