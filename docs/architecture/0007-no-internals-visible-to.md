# 0007 — No `InternalsVisibleTo`

- **Status:** Accepted
- **Date:** 2026-08-31

## Context

Tests need to reach the thing they test. The usual answer is `InternalsVisibleTo`, which lets the
test assembly see internal types and members. It is cheap, it is standard, and it removes the
friction immediately.

It also changes what the tests are testing. A test that reaches an internal type verifies an
implementation; only a test that goes through the public surface verifies what a consumer can
actually do. The distinction stays invisible until the day a refactoring keeps every test green
while breaking every consumer — or the opposite, when a test goes red for a rearrangement no
consumer could have noticed.

## Decision

No package uses `InternalsVisibleTo`. Tests assert through the public surface only.

Where that is inconvenient, the inconvenience is treated as information. A behaviour that cannot be
observed from outside is either not worth guaranteeing, or the surface is missing something a
consumer would also have wanted.

## Consequences

- Every test is written the way a consumer writes code, so the tests double as a check that the
  public surface is usable at all.
- Some tests are longer. Verifying a startup check means composing a host rather than calling the
  check, which is also why `tests/MatrixHosts/` exists.
- The rule pairs with [0008](0008-public-surface-is-a-tracked-file.md): if the tests can only reach
  the public surface, that surface has to be deliberately shaped rather than whatever fell out.
- **Nothing enforces this automatically.** No analyzer or test fails when someone adds the
  attribute; it holds by review. That is worth knowing when reading the rule, and is the reason it
  is written down here rather than assumed.

## Alternatives considered

**`InternalsVisibleTo` for the test assemblies.** The default in most repositories, and by far the
cheaper option. Rejected because it silently changes what a green test run means, and because the
change is invisible in the diff that introduces it.

**A separate "test seam" of public-but-hidden types**, marked with `[EditorBrowsable(Never)]` or
similar. Rejected as the worst of both: the members are genuinely public and therefore genuinely
part of the contract, while pretending not to be.

**Testing internals through reflection.** Rejected for the same reason as the attribute, minus the
readability.
