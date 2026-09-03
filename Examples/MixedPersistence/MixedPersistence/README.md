# MixedPersistence

This example is not the next rung of the adoption ladder — it is proof that the ladder's two store
choices are no longer mutually exclusive. One host, two aggregates, two stores, active at the same
time, against the same PostgreSQL database:

- `Reading` keeps its full history and is claimed explicitly for
  `GaWeCodes.Thessera.Persistence.Marten` — the **ancillary** store in this host, named through
  `UseMartenEventStore(connectionString, typeof(Reading))`.
- `Account` only needs its current balance and runs on
  `GaWeCodes.Thessera.Persistence.EfCore.Postgres` — the **main** store, selected without
  `forAggregates`, so it owns whatever aggregate no other store claims (here, only `Account`).

Which call omits `forAggregates` here is a free choice at this level — swap it and `Account` becomes
ancillary, `Reading` main, with no other change. It is *not* free one level down, at Wolverine's own
message-store layer: Wolverine allows exactly one store tagged Main per host, and Marten's plain
integration can only ever be that Main store, never Ancillary. So `Account`'s EF Core store is always
the one that ends up Ancillary there, with its own inbox/outbox schema, regardless of which store is
main in the sense above — see
[ADR 0017](../../../docs/architecture/0017-wolverine-main-ancillary-message-store.md).

Each command handler still resolves `IRepository<,>` for exactly one aggregate and commits through
that aggregate's own store — nothing here lets a single command span both. `THSS0007`, referenced as
a build-time analyzer in this project just like every other tier, would fail the build the moment a
handler's constructor asked for both `IRepository<Reading, ReadingId>` and
`IRepository<Account, AccountId>` at once. The mixing happens **across** handlers, on the same host,
never inside one.

`Account` also carries a business rule that reads the aggregate's own state, not just the command's
input — `AccountMustHaveSufficientFunds` on withdrawal and `AccountMustNotHoldABalanceToClose` on
close — to show a `BusinessRuleViolationException` turned into a `Failure.BusinessRule` result, next
to the `DomainValidationException` → `Failure.Validation` path both aggregates already share.

Both list queries read a dedicated in-memory read model instead of their write store: `ListAccounts`
reads `IAccountReadModelStore`, kept in sync by an `IReadModelRebuilder<Account, AccountId>` and
`StateStoredReadModelRebuildRunner<AccountDbContext>`; `ListReadings` reads `IReadingReadModelStore`,
kept in sync by an `IReadModelRebuilder<Reading, ReadingId>` and `EventSourcedReadModelRebuildRunner`.
`RebuildReadModelsAsync` runs both runners together — once at startup and again after every successful
mutation of either aggregate — and menu option 10 triggers it by hand. Rebuilding both on every
mutation, even one that only touched one aggregate, is a simplification for this small example; a
larger read model would instead be rebuilt only for the aggregate that changed, or caught up
incrementally.

Prerequisites: refresh `C:\temp\thessera-local-feed`, have PostgreSQL available, and optionally set
`THESSERA_EXAMPLE_POSTGRES`. Run with `dotnet run --project Examples\MixedPersistence\MixedPersistence`
and test with `dotnet test Examples\MixedPersistence\MixedPersistence.Tests`.

See [ADR 0016](../../../docs/architecture/0016-one-store-per-aggregate-not-per-host.md) for why this
is possible at all, and the "Mixing this store with the state/event store" sections in the
`GaWeCodes.Thessera.Persistence.EfCore.Postgres` and `GaWeCodes.Thessera.Persistence.Marten` READMEs
for the API this example calls.

[MixedPersistenceWithMessaging](../MixedPersistenceWithMessaging/README.md), next to this one, adds
RabbitMQ publishing on top of the same two-store host.
