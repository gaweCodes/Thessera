# 0009 — Project and package names are a tested convention

- **Status:** Accepted
- **Date:** 2026-08-31

## Context

A project name is a file name. A wrong one compiles, runs, packs and ships; nothing goes red. Three
renames have passed over this repository and not one of them could have failed.

For a package family that matters more than usual, because the name *is* the contract with a
consumer: it is what they type into `dotnet add package`, and a package published under the wrong id
cannot be taken back.

There is a second, less obvious case. The fixtures under `tests/ExternalAssemblies/` and the hosts
under `tests/MatrixHosts/` deliberately carry no family prefix, because they stand in for a
stranger's code. Prefixing one of them would quietly turn the proof that a package can be consumed
from outside into a proof that it can be consumed from inside — and the test would still pass.

## Decision

The naming rules are short, and `ProjectNamingTests` asserts them:

- A project under `src` is `GaWeCodes.Thessera.<Package>`, and its `.csproj` name matches its
  directory.
- A test project either mirrors exactly one package as `GaWeCodes.Thessera.<Package>.Tests`, or says
  that it mirrors none as `GaWeCodes.Thessera.Tests.<Suite>`.
- Fixtures and matrix hosts carry no prefix at all, and the test guards that hardest.
- Projects under `Examples/` carry no prefix either, for the same reason: they are consumer
  examples, not family packages.

`SolutionCompletenessTests` covers the adjacent gap — a project missing from the solution file still
builds, because whatever references it drags it in, so nothing goes red there either.

## Consequences

- A rename that breaks the convention fails in the pull request rather than on nuget.org.
- The two test-project forms carry meaning: reading `GaWeCodes.Thessera.Tests.PackageConventions`
  tells you it mirrors no package, without opening it.
- Adding a package means adding a directory, a project, a README, two public-API files and a
  solution entry. Several tests check for each, which is deliberate: every one of those omissions is
  invisible at build time.

## Alternatives considered

**Leave it to review.** What most repositories do, and what let three renames through here
unremarked.

**Enforce it in the build with an MSBuild target.** Equivalent in effect, but it makes every build
slower to reason about and puts the rule somewhere nobody reads. A test states the rule in prose
next to the assertion.

**Drop the prefix on fixtures and matrix hosts as a mere convenience.** Rejected once it became
clear that the absence of the prefix is what those projects are for.
