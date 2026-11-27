# Thessera Development Instructions

## What Thessera is

Tactical DDD, CQRS and selective event sourcing building blocks for .NET. The same domain model
runs either state-stored (EF Core) or event-stored (Marten) — switching is a wiring decision, not
a rewrite. [`README.md`](../README.md) lists our packages;
[`docs/architecture/README.md`](../docs/architecture/README.md) describes the shape they form and
how a command travels through it.

## Build, test

```bash
dotnet build Thessera.slnx
dotnet test --solution Thessera.slnx
```

Name the solution in both commands. Two `.slnx` files sit in the repository root — `Thessera.slnx`
and `Examples.slnx` — so a bare `dotnet build` or `dotnet test` fails rather than picking one. The
solution is a *named* argument to `dotnet test` because tests run on the Microsoft.Testing.Platform
mode of `dotnet test`, opted into from `global.json`
([ADR 0013](../docs/architecture/0013-tests-run-on-mtp-mode.md)). Set
`THESSERA_REQUIRE_CONTAINERS=1` to make the Testcontainers-backed tests fail rather than skip when
no Docker daemon is reachable; the workflows always set it.

The SDK pin in `global.json` is the only version number kept outside `Directory.Packages.props`,
which carries every dependency version — a `.csproj` therefore writes
`<PackageReference Include="..." />` with no `Version`. This holds for `Thessera.slnx`. `Examples/`
deliberately opts out of central package management: its projects consume Thessera like any
outside consumer would, pinning the Thessera family to `ThesseraVersion` in
`Examples/Directory.Build.props` and writing every third-party version literally in the project.

`Directory.Build.props` is authoritative for the solution-wide compiler and analyzer settings; read
it rather than trusting a summary. What matters when working here is that the build is strict:
warnings are errors, analysis runs at the highest level, and a rule is relaxed for a whole scope
only in a scoped `.editorconfig` — one under `tests/`, and one in the `Domain` package. No other
project under `src` overrides anything.

Beyond that, code under `src` carries a few suppressions, each scoped to a single type or member
and each with a written justification: `CA1812` in a package's `GlobalSuppressions.cs` for an
internal type only ever instantiated through DI or reflection, `IDE0130` on the entry-point
extension files that deliberately live in the root `GaWeCodes.Thessera` namespace, and `CA2225`
on the `Result<TResult>` conversion operators. These are **absolute exceptions**, not a pattern to
follow: each stays only because removing it would break the public surface or trade one
suppression for another. Before adding a new one, try to fix the code instead — and see rule 11
under "When contributing".

`CS1591` (missing XML documentation) is **not** relaxed anywhere. Every package under `src`
generates a documentation file, and warnings-as-errors turns an undocumented public member into a
build error — so documentation cannot fall behind the public surface without the build saying so.

## Repository map

```text
Thessera/
├── src/                                                 our packages, one directory each
│   └── GaWeCodes.Thessera.Analyzers/                    targets netstandard2.0, not net10.0 - a Roslyn analyzer
├── tests/
│   ├── GaWeCodes.Thessera.<Package>.Tests/              one test project per package
│   ├── GaWeCodes.Thessera.Tests.PackageConventions/     package/project naming and packaging conventions
│   ├── GaWeCodes.Thessera.Tests.Containers/             Testcontainers-backed tests
│   ├── GaWeCodes.Thessera.Tests.EfCore/                 shared EF Core test infrastructure
│   ├── GaWeCodes.Thessera.Tests.Support/                shared test support
│   ├── MatrixHosts/                                     minimal hosts used by the persistence/broker test matrix
│   ├── ExternalAssemblies/                              fixtures referenced from outside the assembly under test
│   └── Shared/
├── Examples/                                            standalone consumer examples and their tests
├── Examples.slnx                                        separate solution for the examples only
├── docs/                                                documentation for maintainers, not shipped in any package
│   ├── architecture/                                    architecture decision records (ADRs)
│   └── glossary.md                                      shared vocabulary for READMEs, XML docs and ADRs
├── CHANGELOG.md
└── README.md
```

The packages under `src` are listed in [`README.md`](../README.md), each with its own
`README.md` describing what it is, when to use it (and when not to), and a runnable example.

`Examples/` is a consumer-facing adoption ladder, described in
[`Examples/README.md`](../Examples/README.md). Two things about it are not visible from there: it
deliberately does not participate in the main solution, and it resets MSBuild strictness with its
own `Examples/Directory.Build.props`. The examples consume Thessera through `PackageReference`s
from a local folder feed, never through project references, and each one owns its domain model and
tests only its own code.

## Comment style

- **No inline (`//`) comments**, anywhere in `src` or `tests`. 
- **No comments in `.csproj` files.**
- **XML documentation comments are added only where the build requires them** — a public member
  under `src`, where `CS1591` turns a missing one into a build error.
  - **As short as possible, as long as necessary.** A required XML comment states what a reader
  cannot already get from the member's name, its signature, or a glance at the code — not what a
  quick read already tells them. Prefer one clear sentence over a paragraph.

## Conventions in force in this repository

Each of these is decided in an ADR; [`docs/architecture/README.md`](../docs/architecture/README.md)
indexes them under "How the repository is kept honest" and says why each one was chosen. What
follows is only what you have to *do*.

- **Project naming.** A project under `src` carries the family prefix `GaWeCodes.Thessera.<Name>`
  and its `.csproj` name matches its directory. A test project is named either
  `GaWeCodes.Thessera.<Package>.Tests` (mirrors exactly one package) or
  `GaWeCodes.Thessera.Tests.<X>` (mirrors none, e.g. `Tests.PackageConventions`). Fixtures under
  `tests/ExternalAssemblies/`, hosts under `tests/MatrixHosts/` and the projects under `Examples/`
  intentionally do **not** carry the family prefix. A test enforces all of this
  ([ADR 0009](../docs/architecture/0009-project-names-are-tested.md)).
- **The public surface is tracked.** Every package under `src` carries `PublicAPI.Shipped.txt` and
  `PublicAPI.Unshipped.txt`; a new public member goes into `PublicAPI.Unshipped.txt` in the same
  change, and a removed or changed one becomes a `*REMOVED*` line there — never edit
  `PublicAPI.Shipped.txt` between releases. A new or changed analyzer rule goes into
  `AnalyzerReleases.Unshipped.md`. Only a stable release promotes the unshipped files into the
  shipped ones, and `release.yml` refuses a stable tag whose number does not fit what changed — a
  new or raised analyzer rule of severity `Error` counts as breaking.
  ([ADR 0008](../docs/architecture/0008-public-surface-is-a-tracked-file.md),
  [ADR 0025](../docs/architecture/0025-a-stable-release-promotes-the-public-surface.md))
- **No `InternalsVisibleTo`.** Tests assert through the public surface only.
  ([ADR 0007](../docs/architecture/0007-no-internals-visible-to.md))
- **No assertion library.** Test projects use xUnit v3 built-in asserts.
  ([ADR 0012](../docs/architecture/0012-xunit-built-in-asserts.md))
- **Tests run on Microsoft.Testing.Platform.** A test project sets
  `UseMicrosoftTestingPlatformRunner` and must **not** set `TestingPlatformDotnetTestSupport` — that
  property re-enables the removed VSTest bridge and breaks the run. The three xUnit packages share
  one major version. ([ADR 0013](../docs/architecture/0013-tests-run-on-mtp-mode.md))
- **Central package management**, transitive pinning included.
  ([ADR 0011](../docs/architecture/0011-central-package-management.md))
- **.NET-aligned dependencies follow LTS releases only.** `Microsoft.Extensions.*`, EF Core,
  Npgsql and SourceLink stay on the current LTS major; Dependabot ignores their major updates, and
  the step to the next LTS is made by hand.
  ([ADR 0023](../docs/architecture/0023-dotnet-aligned-dependencies-follow-lts-only.md))
- **Roslyn never outruns the pinned SDK.** The `Microsoft.CodeAnalysis.*` family shares one
  version, never newer than the compiler of the SDK in `global.json`; Dependabot proposes patches
  only, and a minor or major step is made by hand together with the SDK pin. A newer Roslyn does not
  fail the build — the analyzer simply stops loading (`CS9057`).
  ([ADR 0024](../docs/architecture/0024-roslyn-never-outruns-the-pinned-sdk-compiler.md))
- **One version, from Git tags.** No version number is written in the repository; MinVer derives
  it, and `release.yml` is the only path to nuget.org — see [`RELEASING.md`](../RELEASING.md).
  ([ADR 0010](../docs/architecture/0010-one-version-from-git-tags.md))

## When contributing

1. Add or update tests, and make sure they pass.
2. Check the `*.md` files your change affects and update them — including this file, whenever you
   find a gap or an ambiguity in the guidance here.
3. When a change turns on a decision rather than on a detail — a new seam, a rule the runtime
   enforces, a format that gets persisted — record it as an ADR in [`docs/architecture/`](../docs/architecture).
   Records in `docs/architecture/` are append-only: supersede, never rewrite. The vocabulary they and the READMEs
   share is defined in [`docs/glossary.md`](../docs/glossary.md).
4. Match existing style; respect `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`.
5. **Always work on `main`** — never a separate branch, and never ask which branch to use.
6. Never assume anything. If you need more information always **ask a human**!
7. Ask **always** in the chat, as plain prose. Never open a dialog, prompt, or
   multiple-choice picker; do not use a question tool. Write the question and its options as normal
   text in your answer and then stop and wait.
8. **Never answer with a table.** Not in chat, not in plan or notes documents you write. Use a
   heading with a short list underneath instead, and say what each item _means_ rather than only
   what it measures. Existing tables in the repository docs stay as they are.
9. Never commit yourself.
10. **In `*.md` files, never state the count of a growing collection** — packages, examples,
    analyzer rules, ADRs, tests and the like — as a number. Word it counter-neutral instead (e.g.
    "our packages", "the analyzer rules"), so the text does not go stale the next time the count
    changes. This is mandatory, with no exception for a count that looks stable today.
11. **Never suppress an analyzer or compiler rule** — `SuppressMessage`, `#pragma warning disable`,
    a `.editorconfig` severity of `none`/`silent`, or `NoWarn` — without either asking the user
    first, or recommending that the exception be recorded as an ADR. A suppression is a decision,
    not a workaround, and it does not get made silently.
