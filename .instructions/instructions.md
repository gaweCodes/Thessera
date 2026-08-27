# Thessera Development Instructions

## What Thessera is

Tactical DDD, CQRS and selective event sourcing building blocks for .NET. The same domain model
runs either state-stored (EF Core) or event-stored (Marten) — switching is a wiring decision, not
a rewrite.

## Build, test

```bash
dotnet build
dotnet test
```

Solution file: `Thessera.slnx`. SDK pinned in `global.json` (`10.0.302`, `rollForward:
latestFeature`).

`Directory.Build.props` applies solution-wide: `net10.0`, nullable + implicit usings enabled,
`LangVersion latest`, `AnalysisLevel latest-all`, `AnalysisMode All`, `EnableNETAnalyzers`,
`TreatWarningsAsErrors` and `CodeAnalysisTreatWarningsAsErrors` both true, `WarningLevel 9999`.

Package versions are managed centrally in `Directory.Packages.props`
(`ManagePackageVersionsCentrally`, `CentralPackageTransitivePinningEnabled`): a `.csproj` carries
`<PackageReference Include="..." />` with no `Version`.

Analyzer relaxations found in the repository:

- `src/GaWeCodes.Thessera.Domain/.editorconfig` — `CA1033` off.
- `src/GaWeCodes.Thessera.Application/.editorconfig` — `CA1000` off.
- `tests/.editorconfig` — `CA1707`, `CA1515`, `CA1711`, `CA1031`, `CA1859`, `CA1812` off.
- No other project under `src` has an `.editorconfig` override.

## Repository map

```text
Thessera/
├── src/
│   ├── GaWeCodes.Thessera.Domain/                       aggregates, entities, domain events, typed keys, rules
│   ├── GaWeCodes.Thessera.Application/                  CQRS and integration-event contracts, Result/Failure
│   ├── GaWeCodes.Thessera.Core/                         composition root, dispatcher, projections, startup checks
│   ├── GaWeCodes.Thessera.Wolverine/                    runtime that owns the outbox
│   ├── GaWeCodes.Thessera.Persistence.EfCore/           database-agnostic half of the EF Core state store
│   ├── GaWeCodes.Thessera.Persistence.EfCore.Postgres/  aggregates as state in PostgreSQL
│   ├── GaWeCodes.Thessera.Persistence.Marten/           aggregates as an event stream in PostgreSQL
│   ├── GaWeCodes.Thessera.Npgsql/                       PostgreSQL error translation
│   ├── GaWeCodes.Thessera.Messaging.RabbitMq/           opt-in transport for integration events
│   └── GaWeCodes.Thessera.Testing/                      convention checks and test helpers
├── tests/
│   ├── GaWeCodes.Thessera.<Package>.Tests/              one test project per package above
│   ├── GaWeCodes.Thessera.Tests.PackageConventions/     package/project naming and packaging conventions
│   ├── GaWeCodes.Thessera.Tests.Containers/             Testcontainers-backed tests
│   ├── GaWeCodes.Thessera.Tests.EfCore/                 shared EF Core test infrastructure
│   ├── GaWeCodes.Thessera.Tests.Support/                shared test support
│   ├── MatrixHosts/                                     minimal hosts used by the persistence/broker test matrix
│   ├── ExternalAssemblies/                               fixtures referenced from outside the assembly under test
│   └── Shared/

├── CHANGELOG.md
└── README.md
```

Each package has its own `README.md` describing what it is, when to use it (and when not to), and
a runnable example.

## Conventions in force in this repository

- **Project naming.** A project under `src` carries the family prefix `GaWeCodes.Thessera.<Name>`
  and its `.csproj` name matches its directory. A test project is named either
  `GaWeCodes.Thessera.<Package>.Tests` (mirrors exactly one package) or
  `GaWeCodes.Thessera.Tests.<X>` (mirrors none, e.g. `Tests.PackageConventions`). Fixtures under
  `tests/ExternalAssemblies/` and hosts under `tests/MatrixHosts/` intentionally do **not** carry
  the family prefix.
- **No `InternalsVisibleTo`.** No package uses it; tests assert through the public surface only.
- **No FluentAssertions.** Test projects use xUnit v3 built-in asserts.
- **Central package management.** Every dependency version lives in `Directory.Packages.props`;
  the SDK pin in `global.json` is the only version kept outside of it.

## When contributing

1. Add or update tests, and make sure they pass.
2. Check the `*.md` files your change affects and update them — including this file, whenever you
   find a gap or an ambiguity in the guidance here.
3. Match existing style; respect `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`.
4. **Always work on `main`** — never a separate branch, and never ask which branch to use.
5. Never assume anything. If you need more information always **ask a human**!
6. Ask **always** in the chat, as plain prose. Never open a dialog, prompt, or
   multiple-choice picker; do not use a question tool. Write the question and its options as normal
   text in your answer and then stop and wait.
7. **Never answer with a table.** Not in chat, not in plan or notes documents you write. Use a
   heading with a short list underneath instead, and say what each item _means_ rather than only
   what it measures. Existing tables in the repository docs stay as they are.
8. Never commit yourself.
