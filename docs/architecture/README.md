# Architecture decisions

This directory holds the architecture decision records (ADRs) behind Thessera's design: the
decisions that a package README states as fact, together with what was weighed against them and
rejected. A README tells a consumer what holds. An ADR tells a maintainer why it was decided that
way, which is the part that is otherwise lost.

The vocabulary these records use is defined once in [`../glossary.md`](../glossary.md).

## How the pieces fit together

Every package README describes one package. This section is the only place that describes the shape
they form, because no single README can own it. It deliberately lists no API: for that, read the
package's own README, or the type.

### The stack

Four layers, and the rule that gives the family its shape is that dependencies only ever point
downwards.

- **`Domain`** sits at the bottom and depends on nothing but the BCL — not even on the other
  Thessera packages. That is what lets a domain project prove it has no framework in it.
- **`Application`** adds the contracts an application layer talks to and depends only on `Domain`.
  Still no runtime, no container, no broker.
- **`Core`** implements those contracts: the composition root, the dispatcher, domain-event
  delivery, projection dispatch, the startup checks. It depends on the two packages above plus the
  `Microsoft.Extensions.*.Abstractions`, and on no vendor at all — see
  [0002](0002-core-carries-no-runtime.md).
- **Everything else** sits on top of `Core` and is where the vendors appear. `Wolverine` brings the
  message engine; `Npgsql` brings PostgreSQL error translation; the two persistence packages bring
  a store; `Messaging.RabbitMq` brings a transport.

The graph above `Core` is not a tree — both store packages sit on `Wolverine` *and* on `Npgsql`,
because an outbox needs the engine and PostgreSQL errors are shared by both stores. `Testing` sits
beside all of it, on `Domain`, `Application` and `Core`, and belongs in test projects only.
`Analyzers` sits beside `Testing` rather than on top of anything: it is the one package with no
Thessera reference at all, resolving `Domain`'s types by metadata name instead — see
[0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md).

Of the eleven, exactly two are a choice: `Persistence.EfCore.Postgres` or `Persistence.Marten`. The
rest follow from that choice or from whether the service talks to a broker.

### How a command travels

```text
  ISender.SendAsync(command)
      │
      │   pipeline behaviours, lower order further out
      ├── LoggingBehavior             order 0
      ├── ExceptionToResultBehavior   order 100
      ├── UnitOfWorkBehavior          order 300
      │        │
      │        └── your handler ── IRepository ── aggregate raises domain events
      │
      └── commit — one transaction, both halves or neither:
              the state (or the appended event stream)
              + a DomainEventEnvelope per uncommitted event, into the outbox
                   │
                   ▼
          domain-event queue — durable, partitioned by <aggregate-name>/<id>
                   │
                   ├── integration-event mappers ──► sink ──► transport ──► broker
                   │
                   └── ProjectionEnvelope ──► projection queue, same partitioning
                            │
                            └── IProjectionHandler<TDomainEvent>
```

What the picture is trying to make obvious:

- **The handler never commits.** It loads an aggregate through `IRepository`, calls a method on it,
  and returns. The aggregate collects the events it raised; the unit of work is what persists them,
  once per command, driven by the behaviour at order 300.
- **State and events are written together or not at all.** On a state store the aggregate's state is
  reconciled into the change tracker and saved with the outbox rows in one `SaveChanges`; on an
  event store the events are appended to the stream at the expected version and saved with the
  outbox rows in one `SaveChanges`. This single transaction is the reason a host may only have one
  store — see [0004](0004-one-store-per-host.md).
- **A failed commit is an answer, not an exception.** The fault translators turn a unique-constraint
  violation or a concurrency conflict into a `Failure`, and the command returns a failed `Result` —
  see [0006](0006-persistence-failures-as-result.md).
- **Delivery happens after the commit, and asynchronously.** Everything below the transaction runs
  off durable queues, so it survives a crash and is retried. Delivery is therefore at-least-once and
  projections must be idempotent; the aggregate `Version` on the metadata is the intended watermark.
- **Projections run behind integration events, on their own queue.** A slow projection cannot hold
  up domain-event delivery, and a dead-lettered projection means a read model that stays wrong until
  it is rebuilt — which is why the dead-letter health check reports *degraded* rather than healthy.
- **Without a transport package nothing leaves the service.** The sink falls back to one that logs a
  warning per discarded integration event. In-process work is unaffected.
- **Both queues partition by `<aggregate-name>/<id>`**, so the events of one aggregate are handled
  in order while different aggregates proceed in parallel. That key is the stream key, and it is a
  persisted wire format — see [0003](0003-stream-key-is-a-wire-format.md).

## The records

### The design

- [0001 — One domain model, two stores](0001-one-domain-model-two-stores.md). The core bet: the
  same aggregate runs state-stored or event-stored, and the store is a wiring decision. Why the two
  directions are not symmetric.
- [0002 — The composition root carries no runtime](0002-core-carries-no-runtime.md). Why
  `GaWeCodes.Thessera.Core` is buildable without a message engine, what the `IRuntimeActivator`
  seam carries, and the honest limit that an outbox has to know the engine.
- [0003 — The stream key is a wire format](0003-stream-key-is-a-wire-format.md). Why the rendering
  of an identity is pinned per value type, and why `decimal`, `enum` and `DateTime` keys are
  refused outright.
- [0004 — One store per host](0004-one-store-per-host.md). Why a second store is an error rather
  than a supported topology, and why `WithoutEventHistory()` is spelled as a method call.
- [0005 — Reflection-based discovery](0005-reflection-discovery-no-trimming.md), and therefore no
  trimming and no AOT. Including the consequence no analyzer reports: on a trimmed build the
  handler startup check compares nothing with nothing and reports success.
- [0006 — Persistence failures as `Result`](0006-persistence-failures-as-result.md), not as
  exceptions. How a driver exception becomes a `Failure`, and what deliberately stays an exception.
- [0014 — A compile-time analyzer catches four of the startup checks](0014-a-compile-time-analyzer-catches-four-startup-checks.md).
  Which four conventions moved from a running host, or a test nobody was required to write, into
  `dotnet build` itself — and why the store/aggregate match stayed out.
- [0015 — Two more analyzer rules catch the aggregate- and entity-state self-binding mistake](0015-two-more-analyzer-rules-catch-self-binding.md).
  Why 0014's claim that the compiler already prevents a mismatched `TSelf` was wrong, and the two
  rules that catch what it does not.

### How the repository is kept honest

- [0007 — No `InternalsVisibleTo`](0007-no-internals-visible-to.md). Why tests go through the
  public surface only, and what that costs.
- [0008 — The public surface is a tracked file](0008-public-surface-is-a-tracked-file.md). Why a
  breaking change has to appear in the diff, since it never looks like one.
- [0009 — Project and package names are a tested convention](0009-project-names-are-tested.md).
  Why a file name gets a test, and why the projects *without* the prefix are guarded hardest.
- [0010 — One version for the family, derived from Git tags](0010-one-version-from-git-tags.md).
  Why ten packages share a number, and why nothing is ever published from a laptop.
- [0011 — Every dependency version lives in one file](0011-central-package-management.md). Why
  transitive pinning is part of the decision rather than an extra.
- [0012 — Built-in asserts, no assertion library](0012-xunit-built-in-asserts.md). The one record
  here whose alternative genuinely reads better.
- [0013 — Tests run on the MTP mode of `dotnet test`](0013-tests-run-on-mtp-mode.md). Why the
  VSTest bridge had to go, and why the three xUnit packages move as one.

## Format

One record per file, named `NNNN-kebab-case-title.md`, numbered in the order the records are
written rather than the order the decisions were made. Each record carries a title, a status, a
date, the context it was decided in, the decision itself, its consequences, and the alternatives
that were considered and rejected. `template.md` carries the empty form.

A status is `Proposed`, `Accepted`, or `Superseded by NNNN`. The date is the day the record was
written down. For a decision that was already in force when its record was written, that date is
later than the day the decision was taken.

Records are append-only. Once a record is accepted it is never rewritten, not even when the
decision is later reversed. A reversal is a new record that supersedes the old one, and the old one
keeps its original text so that the reasoning behind the reversal stays readable.
