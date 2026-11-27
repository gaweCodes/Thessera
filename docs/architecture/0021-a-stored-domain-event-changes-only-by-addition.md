# 0021 — A stored domain event changes only by addition; anything else is a new event

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

A domain event is written once and read again later, and the code that reads it is whatever is
deployed at that moment, not the code that wrote it. Two paths read stored events back:

- **The envelope path.** On both stores a `DomainEventEnvelope` waits in the outbox, then on the
  durable domain-event and projection queues, and possibly in dead-letter storage until someone
  replays it. `DomainEventEnvelopeSerializer.Unwrap` reads it.
- **The stream path.** On the event store, every load of an aggregate replays its stream —
  `MartenEventSourcedRepository` and `EventSourcedReadModelRebuildRunner` both go through Marten's
  `FetchStreamAsync`. A stream is never rewritten, so its oldest event is read for as long as the
  aggregate exists.

Both paths find the CLR type by the persisted name from `[EventName]` and deserialize the stored
JSON straight into that type's *current* shape, with the general `System.Text.Json` defaults plus
the typed-key converter (`EntityKeyJsonOptions.Create()`). Thessera has no upcasting, no payload
version and no hook to add either. Under those defaults an old payload meets a changed type like
this:

- A property the JSON carries but the type no longer has is skipped.
- A property the type has but the JSON lacks gets its default — `null` for a reference type even
  where it is declared non-nullable, `0`, `default` for a struct — without an error, a positional
  record's constructor parameter included.
- A renamed property is both of the above at once: the stored value is skipped and the new one is
  default. Data is lost and nothing reports it.
- A property whose type changed either throws on read or yields a value that means something else.

The only written rule so far was the exception note on `Unwrap` — keep a retired type and its name
alongside the successor — and the README remark that changing an `[EventName]` orphans stored data.
Nothing said which changes to an event are safe in place. `PersistedSchema.Verify` in
`GaWeCodes.Thessera.Testing` shows every change to a persisted shape, but leaves the reviewer to
decide which diff may be accepted.

## Decision

An event name, once something has been stored under it, fixes its shape. The one change allowed in
place is **adding a property whose absence old events can carry** — nullable, or with a default that
the domain reads as "not recorded" rather than as a real value.

Every other change makes a **new event**: removing, renaming or retyping a property, or changing
what a property means. The new event is a new type with the next version in its name
(`reading-recorded-v2`). The retired type stays in the assembly with its `[EventName]` unchanged, so
`DomainEventTypeRegistry` still resolves the old name, and it is no longer raised. Every reader that
handled the retired type keeps handling it next to the successor:

- the aggregate state's `Apply`, for as long as a stream can hold the retired event — on the event
  store, for good;
- projection handlers and integration-event mappers, for as long as an envelope of the retired event
  can still be waiting in a queue or in dead-letter storage.

Removing a property would be read without an error, but it still counts as a new event. That keeps
the review rule for a `PersistedSchema` diff down to one sentence: inside an existing
`domain-event` block only added lines may be accepted; a changed or removed line means a new event
name instead.

This record covers domain events. An integration event is a contract with other services and is
versioned together with its consumers; `PersistedSchema` pins its shape too, but this record sets no
rule for it.

## Consequences

- What is stored is exactly what the code reads. There is no transformation to know about when a
  stream is inspected or an envelope is debugged, and a retired event stays readable for as long as
  its type exists.
- Nothing in the public surface changes. The rule is enforced by review: `PersistedSchema` makes
  every change visible, it does not refuse one.
- Retired types accumulate in the domain assembly, and `Apply` grows one branch per version. On the
  event store neither ever shrinks. This is the cost that
  [0022](0022-retired-domain-events-are-upcast-when-read.md) proposes to lift.
- An added property arrives as its default on every event written before it existed. The domain has
  to treat that default as "unknown", which rules out a non-nullable property whose default is a
  plausible real value.
- On the state store the window is short — only envelopes not yet delivered, or dead-lettered, carry
  an old shape — but the rule is the same, because a dead-lettered envelope can be replayed long
  after the deploy that retired its type.

## Alternatives considered

**Upcasting through Marten's own support, behind a `StoreOptions` hook.** Rejected: it covers the
stream path only, not envelopes read by `Unwrap`, and it would put store-specific code into the
consumer's domain, which is exactly what [0001](0001-one-domain-model-two-stores.md) rules out.

**Upcasting in Thessera, for both paths.** Not rejected — it is the better end state, but it is new
public API in several packages and deserves a design of its own rather than a side note here. It is
proposed separately in [0022](0022-retired-domain-events-are-upcast-when-read.md); this rule stays
the baseline either way.

**A version number on the envelope or in the payload.** Rejected: the event name already carries
the version (`-v1`, `-v2`), and a second number would be a second source of truth that can disagree
with the first.

**Rewriting stored events into the new shape.** Rejected: an event stream is append-only history.
Rewriting it gives up the record of what was actually decided at the time, and needs a migration
coordinated with every running instance.

**Allowing a property to be removed in place.** Rejected although it reads without an error: old
events would silently lose information on replay, and the review rule for a schema diff would need
an exception that is easy to misapply to a rename.
