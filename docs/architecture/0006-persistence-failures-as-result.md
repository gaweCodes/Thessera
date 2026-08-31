# 0006 — Persistence failures come back as `Result`, not as exceptions

- **Status:** Accepted
- **Date:** 2026-08-29

## Context

A unique-constraint violation and a concurrency conflict are ordinary answers a write can give. The
caller usually knows what to do with them: report a conflict, ask the user to retry, pick a
different value.

If they escape as driver exceptions, every caller has to catch a vendor type. The application layer
then knows which database sits underneath it, which is exactly the coupling the layering exists to
prevent — and the knowledge spreads to wherever a command is dispatched from.

## Decision

`UnitOfWorkBehavior` commits the unit of work for commands and, when the commit throws, walks the
exception chain — including inner exceptions, because EF Core and Marten wrap driver exceptions —
through every registered `IPersistenceFaultTranslator`. The first translator that recognises the
fault turns it into a `Failure`, and the request returns a failed `Result` instead of throwing.

Shared codes live in `PersistenceFailureCodes`: `persistence.unique_violation` and
`persistence.concurrency_conflict`. An exception no translator recognises keeps propagating
untouched.

## Consequences

- The application layer maps a `Failure` and its `FailureCategory`, never a vendor exception. An
  API can turn a category into a status code without knowing the store.
- A store adapter has to supply its translators. EF Core, Marten and PostgreSQL each ship one, and
  the Marten adapter registers both its own and the PostgreSQL one. An adapter that ships none gets
  exceptions — not silence, which is the right failure.
- Translation walks inner exceptions, so a driver exception wrapped by an ORM is still recognised.
  A translator therefore has to be written against the exception it actually understands and not
  against whatever the outermost type happens to be.
- Only faults a translator recognises become results. An unexpected exception stays an exception,
  which keeps bugs visible.
- `FailureCategory` is deliberately allowed to gain members in a **minor** version, so a `switch`
  over it needs a `_` arm that maps to a generic server-side failure. Code without one compiles
  today and breaks on an upgrade that is otherwise not breaking. This trade is stated in the
  `GaWeCodes.Thessera.Application` README.

## Alternatives considered

**Let the exceptions escape.** Simplest, and it pushes `catch (PostgresException)` into the
application layer — putting the name of the database in the one project that was supposed not to
know it.

**Translate every exception into a `Result`.** Rejected. An unexpected exception is not an answer
the caller can act on; turning it into one converts bugs into ordinary-looking failures and removes
the signal that something is actually wrong.

**Keep exceptions but define Thessera-owned exception types.** Rejected: it fixes the coupling but
keeps using exceptions for control flow over outcomes the domain fully expects. A conflict is not
exceptional; it is one of the answers.

**A closed `FailureCategory` enum.** Rejected as the wrong end of the trade — it would force a
major version for every failure kind the family ever learns, and the alternative costs callers one
`_` arm.
