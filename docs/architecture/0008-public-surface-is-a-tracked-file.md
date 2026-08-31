# 0008 — The public surface is a tracked file

- **Status:** Accepted
- **Date:** 2026-08-31

## Context

A published package's public surface is a promise. Breaking it costs every consumer a compile error
at best and a run-time surprise at worst — and the change that breaks it usually does not look like
a breaking change in review. A renamed parameter, a widened return type, an added optional argument,
an enum member inserted in the middle: each of those is one line in a diff that reviews as a
cleanup.

Nothing in the compiler notices. The package builds, packs and ships exactly as before.

## Decision

Every package under `src` carries `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` and
references `Microsoft.CodeAnalysis.PublicApiAnalyzers`. A change to the public surface is therefore
a change to a tracked file, and shows up in the pull request as added or removed lines rather than
as an absence.

Both halves are asserted by `PublicApiFilesTests`: that the files exist, and that the analyzer is
actually referenced. A project missing either one would build, pack and ship like one that has both,
and no build would report it.

## Consequences

- A breaking change is visible in the diff, next to the code that caused it, to a reviewer who was
  not looking for it.
- Adding a public member costs a second edit. That friction is the point: it is a prompt to ask
  whether the member should be public at all.
- The tracking files record nullability, so a reference type that quietly became nullable is a diff
  line too.
- Together with [0007](0007-no-internals-visible-to.md) this makes the public surface the only thing
  under test and the only thing tracked, so the two cannot drift apart.
- `PublicSurfaceTests` pins the exported **type** list as well. The analyzer covers member shape;
  the test covers what is exported at all.

## Alternatives considered

**Rely on review.** The status quo of most repositories. Rejected because the changes in question do
not look like what they are, and a reviewer who is not specifically hunting for them will not find
them.

**Compare against the previously published package**, with a tool that diffs assemblies. It catches
the same class of change and needs no files in the repository — but it reports after the fact, at
release time, when the change has already been merged and the argument about it has to be had
backwards.

**Semantic-versioning discipline alone**, deciding case by case whether something was breaking.
Rejected because it depends on noticing, which is exactly what fails here.
