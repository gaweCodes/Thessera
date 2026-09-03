# Thessera

[![Build](https://github.com/GaWeCodes/Thessera/actions/workflows/build.yml/badge.svg)](https://github.com/GaWeCodes/Thessera/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Tactical DDD, CQRS and selective event sourcing building blocks for .NET. The same domain model
runs either **state-stored** (EF Core) or **event-stored** (Marten) — switching is a wiring
decision, not a rewrite. The store choice is made per *aggregate*, not per host: one host can keep
one aggregate event-sourced and another state-stored, each committing through its own store.

## Packages

Take as much as you need. Only two of our packages are a real choice; the rest follow from it. Each
package's own README has the details — when you need it, when you don't, install, a runnable
example, and its limits.

- [`GaWeCodes.Thessera.Domain`](src/GaWeCodes.Thessera.Domain) — aggregates, entities, domain
  events, typed keys, rules. BCL only.
- [`GaWeCodes.Thessera.Application`](src/GaWeCodes.Thessera.Application) — CQRS, persistence and
  integration-event contracts, `Result`/`Failure`. Contracts only, no runtime.
- [`GaWeCodes.Thessera.Core`](src/GaWeCodes.Thessera.Core) — composition root, dispatcher,
  projections, startup checks. No Wolverine.
- [`GaWeCodes.Thessera.Wolverine`](src/GaWeCodes.Thessera.Wolverine) — the runtime that owns the
  outbox. Arrives with either store.
- [`GaWeCodes.Thessera.Persistence.EfCore.Postgres`](src/GaWeCodes.Thessera.Persistence.EfCore.Postgres) —
  **store choice 1**: aggregates as state in PostgreSQL.
- [`GaWeCodes.Thessera.Persistence.Marten`](src/GaWeCodes.Thessera.Persistence.Marten) —
  **store choice 2**: aggregates as an event stream in PostgreSQL.
- [`GaWeCodes.Thessera.Persistence.EfCore`](src/GaWeCodes.Thessera.Persistence.EfCore) —
  the database-agnostic half of choice 1; write your own driver here.
- [`GaWeCodes.Thessera.Npgsql`](src/GaWeCodes.Thessera.Npgsql) — PostgreSQL error translation,
  shared by both choices.
- [`GaWeCodes.Thessera.Messaging.RabbitMq`](src/GaWeCodes.Thessera.Messaging.RabbitMq) — opt-in
  transport. Without one, no integration event leaves the service.
- [`GaWeCodes.Thessera.Testing`](src/GaWeCodes.Thessera.Testing) — convention checks and test
  helpers for all of the above.
- [`GaWeCodes.Thessera.Analyzers`](src/GaWeCodes.Thessera.Analyzers) — the compile-time twin of
  the compile-time twin of those conventions, in every host.

## Get started

```bash
dotnet add package GaWeCodes.Thessera.Persistence.EfCore.Postgres
# and/or
dotnet add package GaWeCodes.Thessera.Persistence.Marten
```

Requires .NET 10 (`net10.0`). Follow the getting-started section in the package README for your
store choice — add both to mix event-sourced and state-stored aggregates in the same host.

## Contributing

Issues and pull requests are welcome. See [`docs/architecture/`](docs/architecture) for the
architecture decisions (ADRs) behind the design, and [`docs/glossary.md`](docs/glossary.md) for the
vocabulary those decisions, the package READMEs and the XML documentation share.

## License

MIT. See [`LICENSE`](LICENSE).
