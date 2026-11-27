# 0023 — Dependencies released in step with .NET follow LTS releases only

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

Our packages target an LTS release of .NET. A group of their dependencies ships a new major version
with every .NET release, LTS or not: `Microsoft.Extensions.*`, EF Core, Npgsql and its EF Core
provider, and SourceLink. Dependabot proposes each of those majors as soon as it appears. In doing
so it also replaces a range such as `[10.0.11, 11.0.0)` in `Directory.Packages.props` with the new
fixed version, which removes the upper bound [0011](0011-central-package-management.md) set on
purpose and raises the minimum every consumer must reference.

For a library that is a decision about who can use it, not a routine update: a consumer on the LTS
release would be forced onto a short-term support release to take the next Thessera version.

## Decision

Thessera moves to a new .NET major only when that major is an LTS release. The dependencies
released in step with .NET follow the same rule.

- **Dependabot proposes minor and patch updates only** for `Microsoft.Extensions.*`,
  `Microsoft.EntityFrameworkCore*`, `Npgsql*` and `Microsoft.SourceLink.*`, in the repository root
  and in `Examples/`. `.github/dependabot.yml` ignores their major updates.
- **The step to the next LTS is made by hand**, in one change that moves the SDK pin in
  `global.json`, the target framework and these dependencies together, and widens their ranges to
  the new major.

## Consequences

- A consumer on the current LTS release can take every Thessera release until the next LTS step.
- A Dependabot pull request no longer drops a range's upper bound for these packages.
- The step to the next LTS is not proposed by any tool. It has to be scheduled when that release
  ships.
- A package that starts releasing in step with .NET is not covered until it is added to the ignore
  list.

## Alternatives considered

**Take every .NET major as Dependabot offers it.** Rejected: it ties the family's consumers to
short-term support releases and turns a routine dependency update into a breaking change.

**Close the major-version pull requests by hand each week.** Rejected: Dependabot groups the majors
with the minor and patch updates, so closing the pull request also discards the updates that should
be taken.
