# Architecture decisions

This directory holds the architecture decision records (ADRs) behind Thessera's design: the
decisions that a package README states as fact, together with what was weighed against them and
rejected. A README tells a consumer what holds. An ADR tells a maintainer why it was decided that
way, which is the part that is otherwise lost.

**No ADRs have been written yet.** The records listed below are the first ones planned; this file
is the index they will be added to.

## Format

One record per file, named `NNNN-kebab-case-title.md`, numbered in the order the records are
written rather than the order the decisions were made. Each record carries a title, a status, a
date, the context it was decided in, the decision itself, its consequences, and the alternatives
that were considered and rejected.

A status is `Proposed`, `Accepted`, or `Superseded by NNNN`. Records are append-only: once a record
is accepted it is never rewritten, not even when the decision is later reversed. A reversal is a new
record that supersedes the old one, and the old one keeps its original text so that the reasoning
behind the reversal stays readable.

`template.md` will carry the empty form once the first record is written.

## Planned records

- **0001 — One domain model, two stores.** The core bet: the same aggregate runs state-stored or
  event-stored, and the store is a wiring decision. Consequence: the aggregate style has to match
  the chosen store, which is why a startup check compares them.
- **0002 — `Core` without Wolverine.** Why the composition root is buildable without a runtime, what
  the `IRuntimeActivator` seam carries, and the honest limit that a transactional outbox has to know
  the message engine.
- **0003 — The stream key is a wire format.** Why `EntityKeyFormatter` pins a rendering per key type
  and rejects `decimal`, `enum` and `DateTime` outright.
- **0004 — One store per host.** Why a second store is a startup error rather than a supported
  topology.
- **0005 — Reflection-based discovery, therefore no trimming and no AOT.** Including the consequence
  no analyser reports: on a trimmed build the handler startup check compares nothing with nothing
  and reports success.
- **0006 — Persistence failures as `Result`, not exceptions.** The role of
  `IPersistenceFaultTranslator` and `PersistenceFailureCodes` at the store boundary.

Further records are planned for the decisions that currently sit in
[`.instructions/instructions.md`](../../.instructions/instructions.md) as bare rules without their
reasoning: no `InternalsVisibleTo`, no FluentAssertions, central package management, versions
derived from Git tags by MinVer with releases only from CI, `PublicApiAnalyzers` as the gate on the
public surface, and `WithoutEventHistory()` as a deliberate door with a silent and permanent loss
behind it.
