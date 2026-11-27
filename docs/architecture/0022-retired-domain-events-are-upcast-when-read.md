# 0022 — Retired domain events are upcast when read, in one place for both stores

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

[0021](0021-a-stored-domain-event-changes-only-by-addition.md) settles how a stored domain event may
change: in place only by adding a property, otherwise as a new event with the next version in its
name, the retired type kept beside it. It also leaves every reader to handle the retired type
itself. The aggregate state's `Apply` grows one branch per version, projection handlers and
integration-event mappers keep a handler for each retired type, and on the event store none of it
can ever be removed. After a few years of a model's life that is the part of the domain nobody
wants to read.

The established answer is upcasting: turning a retired event into its successor while it is read,
so that the domain only ever sees the current shape. Marten has its own upcasting support, but it
works on the stream path only. The envelope path — `DomainEventEnvelopeSerializer.Unwrap`, reading
the outbox, the queues and dead-letter storage on both stores — would not see it, and a consumer
using it would write store-specific code into the domain, which
[0001](0001-one-domain-model-two-stores.md) rules out. Thessera owns both read paths, so it is the
one place an upcast can be declared once and applied everywhere.

## Decision

A retired event declares its successor, and how it becomes one, by implementing
`IRetiredDomainEvent<TSuccessor>` from `GaWeCodes.Thessera.Domain`:

```csharp
[EventName("reading-updated-v1")]
public sealed record ReadingUpdated(ReadingId ReadingId, int Value, DateTimeOffset OccurredAt)
    : DomainEvent, IRetiredDomainEvent<ReadingUpdatedV2>
{
    public ReadingUpdatedV2 Upcast() => new(ReadingId, NewValue: Value, OccurredAt);
}
```

Thessera applies that conversion on both read paths, so `Apply`, projection handlers and
integration-event mappers only ever receive the current type.

- **On the retired type, typed and one-to-one.** The conversion is a method of the retired event, so
  it sits next to the shape it reads and the compiler checks both sides. One stored event always
  becomes exactly one successor. The retired type stays, because it is what the stored JSON
  deserializes into, and keeps its `[EventName]` and its place in `DomainEventTypeRegistry`. It
  leaves the domain's working set: nothing raises it and nothing applies it.
- **The event count never changes.** A stream's version and the `Version` on `DomainEventMetadata`
  stay exactly as stored. Splitting one event into several, or merging several into one, is out of
  scope.
- **Conversions chain.** `v1 → v2` and `v2 → v3` together make a stored `v1` arrive as `v3`. A chain
  ends at the first type that is not retired.
- **Applied by Thessera after deserialization, on both paths.** `DomainEventTypeRegistry.Upcast`
  runs the chain. `DomainEventEnvelopeSerializer.Unwrap` calls it after deserializing into the
  retired type, which covers the outbox, the queues and dead-letter storage on both stores. On the
  event store, `MartenEventSourcedRepository` and `EventSourcedReadModelRebuildRunner` call it on
  every event of a fetched stream before `LoadFromHistory`. Marten's own upcasting is not used: the
  stream keeps the retired name and payload, and no Marten API is added.
- **Checked at startup.** `DomainEventTypeRegistry` refuses, while the host is composed, a retired
  event whose successor is not a registered domain event, a type that names more than one
  successor, and a chain that leads back to where it started.
- **A retired event cannot be raised.** `RaiseEvent` throws when it is handed one, on an aggregate
  and on a child entity alike — a retired type is a reading shape, and writing it would add an event
  that is never read as written. The `THSS0009` analyzer rule catches the same call at compile time
  when the argument's static type is retired. Replaying history is unaffected, because it goes
  through `LoadFromHistory`, never through `RaiseEvent`.
- **Pinned by `PersistedSchema`.** The rendering adds a line `=> <successor name>` to a retired
  event's block. Declaring an upcast is therefore an added line, which the review rule of 0021
  accepts, and dropping one is a removed line, which it does not.

The naming rule of 0021 does not change: an addition stays in place, anything else is still a new
event name. Only "every reader keeps handling the retired type" is replaced by "the retired type
declares its successor". This record therefore extends 0021 and does not supersede it.

## Consequences

- The domain reads as if only the current version had ever existed. Retired types become inert data
  shapes, kept for deserialization only, and can live apart from the live model.
- A projection handler or integration-event mapper for a retired type is never called again,
  including for an envelope already waiting in a queue when the upcast is deployed — that envelope
  arrives as the successor. Such a handler can be deleted.
- A stored event is no longer what the code receives. Inspecting a stream or debugging an envelope
  means knowing that an upcast exists, and an upcast becomes part of the persisted contract: once
  data exists it may never change meaning.
- A query that reads events straight from Marten, past Thessera's repository and rebuild runner,
  still sees the retired type. That is outside what Thessera can reach, and stays so.
- The public surface grows only by addition: the contract in `Domain` and
  `DomainEventTypeRegistry.Upcast` in `Core`, both tracked in the `PublicAPI` files. Three
  behaviours change under unchanged signatures: `Unwrap` returns the current version, the registry
  constructor refuses the cases above, and `RaiseEvent` refuses a retired event.
- Reading costs one virtual call per retired event, negligible next to deserializing it.

## Alternatives considered

**Stay with 0021 alone.** The status quo, and adequate for a small history. Rejected as the end
state because its cost grows with every version and never shrinks on the event store.

**A separate upcaster type, `IDomainEventUpcaster<TRetired, TSuccessor>`.** It would keep the
retired record free of behaviour, at the price of a second type per version, a registration or scan
to find it, and a startup check that every upcaster's source is a retired event. The conversion
belongs to the retired shape and has no dependencies, so it is declared on that shape. Rejected.

**A registration call on the composition root.** Rejected: it would put the version history of the
domain into host wiring, where a second host can forget it, and away from the types it describes.

**Feed the conversions into Marten's own upcasting.** It would make even a direct Marten query
return the successor. Rejected: it would tie Thessera to one more Marten API that is not an
application-level contract, and it would leave two mechanisms where one already covers both paths.

**Expose Marten's upcasting through a `StoreOptions` hook.** Rejected for the reasons 0021 gives: it
covers the stream path only and puts store-specific code into the domain.

**Convert JSON instead of types.** A conversion from the stored JSON straight to the successor would
remove the retired type altogether. Rejected as the default: it is untyped, the compiler checks
nothing, and every conversion would have to reproduce the serializer settings — the typed-key
rendering among them — by hand. It could be added later for a case the typed form cannot express.

**Rewrite stored events into the successor's shape.** Rejected for the reasons 0021 gives: a stream
is append-only history, and rewriting it needs a migration coordinated with every running instance.

**Guard against raising a retired event in `Wrap` instead of `RaiseEvent`.** Rejected: `Wrap` runs
at commit time, far from the call that raised the event, and an aggregate would already have applied
an event it never reads.
