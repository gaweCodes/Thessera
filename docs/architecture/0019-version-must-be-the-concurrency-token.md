# 0019 — An EF Core aggregate state's `Version` must be the concurrency token

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

Both stores promise the same thing: of two commands that loaded the same aggregate at the same
version, the second to commit fails as a conflict and becomes `persistence.concurrency_conflict`
([0006](0006-persistence-failures-as-result.md)). On Marten the promise is kept by the adapter
itself — `MartenUnitOfWork` appends at the aggregate's expected stream version, and a stale writer
cannot succeed.

On EF Core the promise depended on the consumer. `EfCoreUnitOfWork` writes the new `Version` into
the tracked state, and EF Core only turns that into `UPDATE … WHERE version = <loaded version>` if
the property is mapped as a concurrency token. Every example, test fixture and README in this
repository did map it, so the conflict tests passed — but nothing required it. A consumer who left
out `IsConcurrencyToken()` got an `UPDATE … WHERE id = …` instead: the later writer silently
overwrote the earlier one, `EfCoreFaultTranslator` never saw a `DbUpdateConcurrencyException`, and
nothing at build, startup or commit time said so. A lost update is the one failure in this family
that loses data without a trace.

`AggregateStateModelCheck` already refused the other mappings that fail silently on commit — an
independent entity where an owned type belongs, an owned collection without a key, a stored name
derived from a CLR name.

## Decision

`AggregateStateModelCheck` refuses to start the host when the `Version` property of any aggregate
state in the write `DbContext` is unmapped or not a concurrency token. The message names each
offending `State.Version` and says what goes wrong without it. It is reported after the check's
existing mapping failures, so a state broken in several ways still names the structural fault
first. The adapter does not configure the
token itself; the mapping stays the consumer's, as it already is for keys, owned types and column
names.

## Consequences

- Optimistic concurrency on the state store no longer depends on a line the consumer has to
  remember; a missing token fails the host at startup, not a race in production.
- A host that already maps the token — every example in this repository, every fixture that starts
  a host successfully, and the VitalSync samples — is unaffected.
- A host that does not map it stops starting after the upgrade. That is the point, but it is a
  breaking change for that host and is called out in the changelog. Adding `IsConcurrencyToken()`
  changes no column, so no schema migration follows from it.
- The check only asks for a concurrency token; it does not prescribe how the value is produced.
  `Version` is owned by the domain and incremented per applied event, which is what the token has
  to compare.

## Alternatives considered

**Let the adapter configure the token itself**, through an `IModelCustomizer` or a convention
plugin added to the `DbContextOptions`. Rejected: it only takes effect where the context is built
with the adapter's options. A context built any other way — a design-time factory, a test, a
consumer's own `AddDbContext` — would quietly miss the token again, which is exactly the silent
failure this record exists to remove. It would also compete with a consumer's own model
customizer. A check fails loudly wherever it runs; a convention fails silently wherever it does not.

**Do both** — configure the token and check it. Rejected for now: the check alone closes the gap,
and the consumer already writes the mapping for every other column of the state. Adding a
convention later does not contradict this record.

**Leave it to documentation.** Rejected: the documentation already said to map the token, and the
gap was that nothing noticed when it was not done.
