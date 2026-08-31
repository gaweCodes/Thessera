# 0012 — Built-in asserts, no assertion library

- **Status:** Accepted
- **Date:** 2026-08-31

## Context

An assertion library reads better than `Assert.Equal`. That is a real benefit, and for most
repositories it settles the question.

Two things weigh the other way here. A published package family is a dependency graph somebody else
inherits, and every test-side library is one more thing to keep current, to resolve version
conflicts around, and to explain to a contributor who has never used it. And the licensing of
assertion libraries in this ecosystem has changed under its users before, which is an unusual amount
of risk to accept in exchange for nicer-reading assertions.

## Decision

Test projects use the assertions that come with xUnit v3, and reference no assertion library.

Where a bare assert would be unreadable, the fix is a well-named helper in the test project or a
message argument on the assert — the convention checks under
`GaWeCodes.Thessera.Tests.PackageConventions` do exactly that, and their messages explain what went
wrong and what it will cost rather than only that something differed.

## Consequences

- One fewer dependency, and one fewer thing a contributor has to know.
- Some assertions are wordier than their fluent equivalent. This is the cost, and it is real.
- A failure message is only as good as the person who wrote it, so the convention tests carry
  explicit messages instead of relying on a library to produce one.
- **Nothing enforces this.** No check fails when someone adds an assertion library; it holds by
  review, like [0007](0007-no-internals-visible-to.md).

## Alternatives considered

**FluentAssertions.** The most common choice, and the most readable. Rejected on the licensing
change and the dependency, not on the ergonomics — those are genuinely better.

**Shouldly, or another assertion library.** Same trade with a different name. The licensing concern
is narrower, but the dependency argument is unchanged, and picking a less common library trades one
kind of unfamiliarity for another.

**A thin assertion helper of our own.** Rejected as the worst option available: it is an assertion
library with no documentation, no community and exactly one maintainer.
