# 0013 — Tests run on the Microsoft.Testing.Platform mode of `dotnet test`

- **Status:** Accepted
- **Date:** 2026-09-02

## Context

Test projects here have always run on Microsoft.Testing.Platform (MTP) rather than VSTest: every
test project sets `UseMicrosoftTestingPlatformRunner`, and xUnit v3 builds each of them as an
executable that hosts the platform itself.

Getting `dotnet test` to talk to that executable was the awkward part. Before the .NET 10 SDK,
`dotnet test` only knew VSTest, so MTP shipped a bridge: setting the
`TestingPlatformDotnetTestSupport` property made `Microsoft.Testing.Platform.MSBuild` override the
`VSTest` target and redirect it to `InvokeTestingPlatform`. Platform arguments had to be pushed
through an extra `--` separator, because in that mode they travel as what VSTest considers
RunSettings. That is what both workflows did, and it worked.

Two things ended it at once. MTP 2 removes the VSTest bridge on the .NET 10 SDK — the target now
fails with a hard error instead of redirecting — and xUnit v3 4.0.0 moves from `xunit.v3.core.mtp-v1`
to `xunit.v3.mtp-v2`, so taking that version means taking MTP 2. The .NET 10 SDK offers the
replacement in the same release: a first-class MTP mode for `dotnet test`, opted into from
`global.json`.

## Decision

`global.json` carries a `test` section selecting the runner:

```json
"test": {
  "runner": "Microsoft.Testing.Platform"
}
```

`TestingPlatformDotnetTestSupport` is set nowhere. The bridge it enables is the thing being left
behind, and leaving it set is not merely redundant — it re-imports the VSTest target and reintroduces
the error.

The solution becomes a named argument rather than a positional one, so both workflows invoke
`dotnet test --solution Thessera.slnx`. The `--` separator before platform arguments is no longer
required; it is kept in front of `--report-xunit-trx` because it still marks unambiguously where
arguments stop belonging to `dotnet test` and start belonging to the test application.

The three xUnit packages — `xunit.v3`, `xunit.v3.extensibility.core` and
`xunit.runner.visualstudio` — move as one. They share a major version because
`xunit.v3.core.mtp-v1` pins `xunit.v3.extensibility.core` to an exact version, and with the
transitive pinning of [0011](0011-central-package-management.md) a split across majors is not a
warning but an unresolvable restore.

## Consequences

- The test report is now a single `TestResults/test-results.trx` at the repository root rather than
  one file per test project, so the workflows upload `**/test-results.trx`. Per-project results are
  no longer separable from the artifact alone.
- `dotnet test` with no argument does not work from the repository root, because two `.slnx` files
  live there and the SDK refuses to guess. This was already true and is not caused by the runner
  change, but the new mode makes the solution argument explicit enough that it is worth stating.
- The projects under `Examples/` are dragged along. They manage their own versions rather than using
  `Directory.Packages.props`, so their xUnit references were bumped by hand, and they inherit the
  `global.json` at the repository root even though they build from a separate solution.
- The floor under the SDK pin rises. The MTP mode of `dotnet test` exists only in the .NET 10 SDK, so
  the repository can no longer be built and tested with an older one. `global.json` already pinned
  10.0.302, so nothing changes today — but the pin is now load-bearing rather than a preference.
- Nothing enforces the absence of `TestingPlatformDotnetTestSupport`. A new test project that copies
  an old one from Git history reintroduces the failure, and the message it produces names VSTest,
  which is misleading here.

## Alternatives considered

**Keep xUnit v3 on 3.x and stay on the bridge.** The smallest change, and it would have kept the
workflows untouched. Rejected because it is a decision to stop taking xUnit updates: 4.0.0 is where
the line is drawn, and the bridge is removed on the SDK this repository pins. The wait buys nothing
and the same work is due later, with more accumulated between.

**Take xUnit 4.0.0 but keep `TestingPlatformDotnetTestSupport`.** Not an alternative once tried —
this is the exact combination that fails, and it is what a naive dependency bump produces.

**Split the xUnit packages across majors**, as the grouped dependency update proposed. Rejected
because it does not restore at all; it is the failure that prompted this record rather than an
option.

**Run the test executables directly** instead of through `dotnet test`, since each test project is
already an executable. This works and needs no opt-in. Rejected because the workflows would have to
enumerate and invoke every test binary themselves, reimplementing discovery and result aggregation
that the SDK now does.
