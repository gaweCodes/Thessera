# 0010 — One version for the family, derived from Git tags, released only from CI

- **Status:** Accepted
- **Date:** 2026-08-31

## Context

Ten packages that are released together can be versioned in two ways. Each can carry its own number,
which is honest about what changed but forces every consumer to work out which combinations are
compatible. Or they can share one, which wastes version numbers on packages that did not change and
tells a consumer at a glance that the set belongs together.

Separately, a version written into a file drifts from the tag that was actually published — and a
package pushed from a laptop cannot be reproduced by anyone else, cannot be traced to a commit, and
is indistinguishable from one built correctly.

## Decision

The whole family shares one version, and no version number is written anywhere in the repository.
MinVer derives it from the Git tag, with `v` as the tag prefix. A release is one annotated tag on
`main`, and `release.yml` is the only path to nuget.org.

The workflow refuses to publish when the resolved package version does not match the tag itself.
That check exists because the failure it catches is quiet: an unreachable tag — a shallow clone, a
wrong prefix — makes MinVer fall back to `0.0.0-alpha.0`, which would otherwise be published under
that number without complaint.

Publishing runs under a GitHub environment with Trusted Publishing, so no long-lived credential
exists to leak or to be used from a laptop.

## Consequences

- A published version can always be traced back to exactly one commit.
- A package with no changes still gets a new version. That is the accepted cost, and it is cheaper
  than the compatibility matrix the alternative produces.
- Nothing is ever packed or pushed from a developer machine, so "it worked when I built it" cannot
  reach a consumer.
- A preview follows the same path — `v1.0.0-preview.1` is a tag like any other — so the release
  mechanism is exercised before it matters.
- A published version can be unlisted but never deleted, which is why the release checklist ends
  with reading each package page rather than with a green workflow.

## Alternatives considered

**Per-package versions.** More honest per package, and the reason it was rejected is the consumer:
ten independently moving numbers make "which versions go together" a question somebody has to
answer, repeatedly, forever.

**A version in a file, bumped by hand.** Simple until the file and the tag disagree, which they do
eventually, and the disagreement is only visible on nuget.org.

**Publishing from a laptop for the first release**, with CI added later. Rejected because the first
release is precisely the one nobody has practised, and because a credential that exists on a laptop
stays on that laptop.
