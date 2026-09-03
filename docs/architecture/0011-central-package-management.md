# 0011 — Every dependency version lives in one file

- **Status:** Accepted
- **Date:** 2026-08-31

## Context

Our packages plus their test projects reference the same third-party libraries repeatedly. With a
version on every `PackageReference`, two projects end up on two versions of the same library sooner
or later, and NuGet resolves the conflict by picking one — silently. The project that asked for the
other version compiles against what it declared and runs against something else.

Transitive dependencies make it worse: a library the repository never named can arrive at a version
nobody chose, and change when an unrelated package is updated.

## Decision

`Directory.Packages.props` holds every version. `ManagePackageVersionsCentrally` is on, so a
`PackageReference` carrying a `Version` is an error rather than an override, and
`CentralPackageTransitivePinningEnabled` extends the same control to transitive dependencies.

The SDK pin in `global.json` is the only version kept outside that file.

Ranges are used where a major upgrade should not be taken sight-unseen — for the libraries this
family configures through APIs that are not application-level contracts, notably EF Core metadata,
Marten and the message engine. The reasoning for each of those pins lives in the package README that
depends on it.

## Consequences

- Two projects cannot disagree about a version, because there is only one place to state it.
- Upgrading is one line, and the diff shows every project it affects, which is none of them
  individually and all of them at once.
- Adding a dependency is two edits rather than one. That friction is mild and, for a package family,
  useful: a new third-party dependency is a decision.
- The rule is enforced by the SDK rather than by a test, so it cannot be bypassed by accident.
- Version numbers in prose go stale. The READMEs therefore describe the pins — "pinned below its
  next major version" — rather than quoting them, so that the reason survives the next bump while
  the number lives in exactly one place.

## Alternatives considered

**A version on every `PackageReference`.** The default, and the source of the silent-resolution
problem above.

**Central versions without transitive pinning.** Half the benefit: direct references would agree
while a transitive dependency still moved on its own. Rejected because the transitive case is the
one nobody is watching.

**Floating versions** (`10.*`). Rejected outright: a build that resolves differently tomorrow than
it did today cannot be reproduced, and for a published package that irreproducibility reaches
consumers.
