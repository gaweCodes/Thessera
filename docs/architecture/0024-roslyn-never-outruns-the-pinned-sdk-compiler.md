# 0024 — Roslyn is never newer than the compiler of the pinned SDK

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

`GaWeCodes.Thessera.Analyzers` compiles against `Microsoft.CodeAnalysis.CSharp`. The compiler that
loads an analyzer must be at least as new as the Roslyn version the analyzer was built against.
Otherwise it reports `CS9057`, skips the analyzer, and the build carries on: the analyzer rules
stop running without any error.

The same Roslyn packages are pinned centrally for a second reason. Wolverine compiles code at
runtime and needs a minimum Roslyn version, so `Directory.Packages.props` pins the whole
`Microsoft.CodeAnalysis.*` family through transitive pinning
([0011](0011-central-package-management.md)). Because it is one family, Dependabot proposed raising
part of it to a version newer than the compiler of any .NET 10 SDK available at the time. A build
against that version stays green, because the analyzer tests bring their own Roslyn. Only a
consumer would notice, and only by the analyzer rules going quiet.

## Decision

The `Microsoft.CodeAnalysis.*` family is never raised above the compiler version of the SDK pinned
in `global.json`. It is raised by hand, in the same change that moves that pin.

- **Dependabot proposes patch updates only** for `Microsoft.CodeAnalysis*`. A patch release keeps
  the assembly version the compiler compares against. `.github/dependabot.yml` ignores minor and
  major updates and groups the family, so a patch moves every package of it together.
- **The family moves as one.** Every `Microsoft.CodeAnalysis.*` pin carries the same version,
  except `Microsoft.CodeAnalysis.PublicApiAnalyzers`, which is released separately.

## Consequences

- The analyzer loads in every SDK at or above the pinned one, which is the floor
  [0013](0013-tests-run-on-mtp-mode.md) already sets for working in this repository.
- Wolverine's runtime minimum constrains how low the family can go. If Wolverine requires a Roslyn
  newer than the compiler of the pinned SDK, the SDK pin has to move first.
- Nothing in the build detects a family raised too far. The rule is held by the Dependabot
  configuration and by review.

## Alternatives considered

**Raise the family as Dependabot offers it.** Rejected: it silently disables the analyzer for every
consumer whose SDK is older than the newest Roslyn release.

**Build the analyzer against the oldest Roslyn of the LTS release, through `VersionOverride`.** It
would let the runtime family move independently and keep the analyzer loadable in every .NET 10
SDK. Rejected for now: it would be the only version written in a `.csproj`, an exception to 0011
that the gain does not yet justify.
