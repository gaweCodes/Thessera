# Glossary

The vocabulary Thessera uses in its READMEs, its XML documentation, its error messages and its
architecture decision records. It exists so that a term means the same thing in all four, and so
that the pairs of terms which are easy to confuse are told apart in one place instead of scattered
across many.

Where the prose in this repository and the name in the code differ, both are given, and the entry
says which one is the type you will actually see in an IDE.

## The domain model

### Aggregate, aggregate root

The unit that is loaded, changed and saved as a whole, and the only thing a repository hands out.
`AggregateRoot<TKey, TState>` is the base type; `IAggregateRoot<TKey>` is what the rest of the
family binds against. A root owns its state, raises domain events through `RaiseEvent`, and exposes
them as `DomainEvents` until they are cleared at save time.

### Aggregate state

The immutable record that holds an aggregate's data, derived from `AggregateState<TSelf, TKey>`.
`TSelf` must be the state's own type, because `Apply` returns a copy of itself; naming a different
type compiles and then fails as an `InvalidCastException` on the first applied event. A startup
check verifies it, and so does the `THSS0005` analyzer rule in `GaWeCodes.Thessera.Analyzers` — see
[ADR 0015](architecture/0015-two-more-analyzer-rules-catch-self-binding.md), which corrects an
earlier claim in [ADR 0014](architecture/0014-a-compile-time-analyzer-catches-four-startup-checks.md#alternatives-considered)
that the compiler already prevents this.
The distinction that matters at save time: the root is what you track,
`IStateOwner.State` is the object to store, and it is a *different* object after events were
applied.

### Aggregate style

Whether an aggregate is stored as its current state or as the stream of events that produced it.
`AggregateStyle` has exactly two members, `StateStored` and `EventSourced`. A single aggregate's
style is not configured, it is read from its base type: deriving from `EventSourcedAggregateRoot`
makes it event-sourced, deriving from `AggregateRoot` makes it state-stored. A store declares the
style it supports through `IPersistenceAdapter.AggregateStyle`, and a mismatch between the two is a
startup error rather than a run-time surprise.

### Entity

An object with an identity that lives inside an aggregate and is reached through it, never through a
repository of its own. `Entity<TKey, TState>` and `EntityBase<TKey>` compare by identity, not by
value; an aggregate root is itself an entity in that sense.

### Entity key, typed key

The identity of an aggregate or entity, wrapped in its own type rather than a bare `Guid` or `int`,
so that two identities of different kinds cannot be passed to one another. The prose in this
repository says **typed key**; the code calls it `IEntityKey`, which knows whether it `IsEmpty`, and
`IEntityKey<TValue>`, which exposes the `Value`. They are the same thing.

Only four value types are supported, because the value is persisted as text and that text is
forever: `Guid`, `string`, `int` and `long`. Anything else is rejected outright — see *stream key*
for why.

### Domain event

A fact that has already happened inside the domain, raised by an aggregate and delivered in-process.
`IDomainEvent` is the contract; `DomainEvent` is a convenient record base. A domain event never
leaves the service. What leaves is an *integration event*, mapped from it.

### Business rule

A domain invariant that is either intact or broken: `IBusinessRule` has a `Code`, a `Message` and
`IsBroken()`. `RuleChecker` turns a broken one into a `BusinessRuleViolationException`, which the
pipeline converts into a `Failure` in the `BusinessRule` category. Its fallback code is
`domain.business_rule`.

### Domain validation rule

The narrower sibling of a business rule: `IDomainValidationRule` additionally names a `Target`, the
field the complaint is about, and answers `IsInvalid()`. It ends as a `DomainValidationException`
and then as a `Failure` in the `Validation` category, fallback code `domain.validation`. Use a
validation rule when the answer points at one input, a business rule when it is about the state of
the aggregate as a whole.

## Names that are persisted

### Contract name, name segment

A name that is written to a database or onto a wire and therefore may never follow the CLR type it
happens to sit on. `NameSegment.IsValid` defines the shape: lower-case ASCII letters and digits,
single hyphens, none at the start or the end — `widget-created-v1`. Aggregate names, event names and
both segments of a topic are all contract names.

### Aggregate name

The persisted name of an aggregate type, declared with `[AggregateName]`. It prefixes every stream
key and travels on every domain-event envelope, so it is a persistence contract; an aggregate
without the attribute is rejected rather than silently named after its CLR type.

### Event name

The persisted name of a domain event type, declared with `[EventName]`. `DomainEventTypeRegistry`
holds the catalogue built by `AddDomainEventsFrom`, maps a type to its name when writing and
resolves an incoming name back to a type when reading. Renaming a CLR type is free; changing an
`[EventName]` orphans everything already stored under the old one.

### Stream key

The text that addresses one aggregate's event stream, in the form `<aggregate-name>/<key-value>`
with `/` as the separator (`EntityKeyFormatter.StreamKeySeparator`). It appears in the event store,
in persisted rows and in domain-event envelopes, so its rendering is pinned per key type rather than
left to `ToString()`: a `Guid` uses format `D` invariant, an `int` or `long` uses invariant decimal
and rejects negatives, and a `string` is taken verbatim but rejected if it contains `/`, because
such a value would let two different aggregates address the same stream. An empty key is rejected
too — it would produce a key that looks valid and is shared by every unidentified aggregate of that
type.

### Topic

The routing key an integration event is published under, declared with `[IntegrationEventTopic]` and
always exactly two contract-name segments: `<context>.<event>`, for example `orders.order-placed`.
Consumers bind to patterns over it, where `*` matches one segment and `#` matches zero or more.
Because the topic is part of the published contract, an event without the attribute is rejected
rather than published under a key nobody has bound.

## Storing

### State store

A store that keeps an aggregate's current state and overwrites it on every change, so the past is
not retained. In this family that is `GaWeCodes.Thessera.Persistence.EfCore.Postgres`, selected with
`UseEfCoreStateStore<TContext>`. It is store choice 1 of 2 — as a host's main store or as an
ancillary store for a named subset of aggregates.

### Event store

A store that keeps the events an aggregate produced and rebuilds the current state by replaying
them. In this family that is `GaWeCodes.Thessera.Persistence.Marten`, selected with
`UseMartenEventStore`. It is store choice 2 of 2 — as a host's main store or as an ancillary store
for a named subset of aggregates. Do not confuse it with the *outbox*, which also stores messages
but exists to get them delivered, not to reconstruct an aggregate.

### Event history

The stream an event-sourced aggregate leaves behind, and the thing that makes replay, audit and
as-of inspection possible. `WithoutEventHistory()` gives it up on purpose: it allows an aggregate
derived from `EventSourcedAggregateRoot` to run on a **state** store, where the state and the
version are still written correctly and the events still reach the outbox, but no stream is kept and
that loss is silent and permanent. The call means nothing on an event store and nothing without a
store, and is rejected at startup in both cases.

### Repository

The only way to load and add an aggregate: `IRepository<TAggregate, TKey>` with `GetByIdAsync` and
`AddAsync`. It does not save — saving is the unit of work's job.

### Unit of work

The commit boundary for one request: `IUnitOfWork.CommitAsync`. The built-in unit-of-work pipeline
behaviour calls it at order 300, after logging at 0 and exception-to-result at 100. A host whose
scanned assemblies contain commands but which has no unit of work and no store fails at startup,
because every one of those commands would otherwise report success while nothing is committed.

### Outbox

The store-side queue that makes "the aggregate was saved" and "its events were published" one
decision: domain events are written in the same transaction as the aggregate, then delivered
afterwards. The Wolverine runtime owns it; a store contributes its transaction through
`IOutboxDurabilityConfigurator`. This is also why the runtime is not exchangeable once a store is
selected — an outbox has to know the message engine.

### Transient fault

A failure worth retrying because the same call may well succeed shortly after: a dropped connection,
a lock timeout. Which exceptions qualify is the store's answer, not the runtime's —
`IPersistenceAdapter.IsTransientFault`, for PostgreSQL implemented by
`PostgresTransientFaults.IsTransient`. It is what separates a retry with a cooldown from a trip to
the error queue.

### Persistence adapter

What a store package hands to `UsePersistence` to announce itself: `IPersistenceAdapter` names its
`AggregateStyle`, its `Description` and its `WriteConnectionString`, decides `IsTransientFault`, and
registers its services through `Register(PersistenceRegistrationContext)`. A host selects one as its
main store; either package may be added a second time, claiming a named set of aggregates, as an
ancillary store — see [0016](architecture/0016-one-store-per-aggregate-not-per-host.md).

## Reading

### Projection

A handler that reacts to a domain event in order to write a read model:
`IProjectionHandler<TDomainEvent>`, receiving the event and its `DomainEventMetadata`. Projections
run on their own durable queue, so that a slow one cannot block domain-event delivery.

### Read model

The shape data is read in, kept separate from the aggregate that is written. It is derived, never
authoritative, and can be thrown away and rebuilt: `IReadModelRebuilder<TAggregate, TKey>` clears
and refills it, and `ReadModelRebuildWriter` does the writing in batches of 500.

### Domain event metadata

The context that travels with a domain event when it is handled rather than raised:
`DomainEventMetadata` carries `EventId`, `AggregateName`, `AggregateId`, `Version` and `OccurredAt`.
It is what a projection handler and an integration-event mapper receive alongside the event itself.

### Domain event envelope

The serialized form a domain event travels and is stored in: `DomainEventEnvelope` adds `EventName`
and `Payload` to the same five metadata fields. `DomainEventEnvelopeSerializer` wraps and unwraps
it, resolving names against the `[EventName]` catalogue. The metadata is the in-process view of an
event; the envelope is the on-the-wire view of the same one.

## Between services

### Integration event

What one service publishes for others to consume: `IIntegrationEvent`, carrying an `EventId` and an
`OccurredAt`, produced from a domain event by an `IIntegrationEventMapper<TDomainEvent>`. The
distinction from a domain event is not shape but audience — a domain event is an internal fact and
never leaves, an integration event is a published contract you are not free to change.

### Bounded context

In this repository, deliberately the narrow reading: one host, one name, and — per aggregate — one
write database. The name is given as `contextName` when a transport is wired, it is validated as a
contract name, and it is the first segment of every topic that host may publish under. A commit
cannot span two databases, which is why claiming the same aggregate from two stores is an error; a
host may still select more than one store, as long as each aggregate is claimed by exactly one of
them (see "Main store" and "Ancillary store" below).

### Main store

The one store in a host selected *without* a `forAggregates` list — it owns every aggregate no other
selected store claims. A host has at most one; the common single-store host is a main store with no
ancillary stores next to it.

### Ancillary store

A store selected *with* a `forAggregates` list, naming exactly the aggregates it owns. A host may
have any number of ancillary stores, each keyed by its own `IUnitOfWork` registration, alongside at
most one main store. This is what lets one host keep one aggregate event-sourced and another
state-stored — see [ADR 0016](architecture/0016-one-store-per-aggregate-not-per-host.md).

### Source context

The publishing context stamped onto every outgoing integration event, in the message header
`thessera.source-context` (`IntegrationEventSourceContext.HeaderName`). It is how a service
recognises and skips the events it published itself when it also subscribes to topics it emits. Do
not confuse it with the context segment of a *topic*, which names the owner of the contract; for a
service's own events the two are the same string, but the header is a property of the message, not
of the type.

### Subscription

What a host declares in order to receive integration events:
`SubscribeToIntegrationEvents(endpointName, consumerAssembly, topicPatterns)`, recorded as an
`IntegrationEventSubscription`. The endpoint name is the queue, the patterns decide what is bound to
it, and the assembly is where the consuming handlers are found.

## Wiring and startup

### Messaging transport adapter

What a transport package hands to `UseMessagingTransport` to announce itself:
`IMessagingTransportAdapter` names its `ContextName` and `Description` and registers through
`Register(MessagingTransportRegistrationContext)`. Without a transport, no integration event leaves
the service; domain events and projections still run.

### Runtime activator

The message engine a host actually starts: `IRuntimeActivator.Activate(builder, wiring)`, one per
host, with `RuntimeActivation.GetOrAdd` making sure a store and a transport that both want the same
runtime share it. Because it needs the host builder, `AddThessera` has to be called on
`IHostApplicationBuilder` rather than on `IServiceCollection` whenever a runtime is involved.

### Pipeline behaviour

A cross-cutting step around every dispatched command and query:
`IPipelineBehavior<TRequest, TResponse>`, registered with an order. The three built in are logging
at 0, exception-to-result at 100 and unit-of-work at 300; your own goes wherever it belongs relative
to those.

### Startup check, startup phase

A check that turns silent misconfiguration into a message at boot: `IStartupCheck`, or
`SynchronousStartupCheck` when nothing needs awaiting, running in one of two phases —
`BeforeHostedServicesStart` or `AfterHostedServicesStarted`. Five ship with the core: every command
and query has exactly one handler, the aggregate style matches the store, an aggregate state names
itself, every integration-event mapper is reachable, and a unit of work exists when commands do.
Each of them is there because the failure it catches is otherwise invisible until production. Six
conventions move earlier still, into `dotnet build`, via `GaWeCodes.Thessera.Analyzers`: a missing
`[AggregateName]` or `[EventName]`, an aggregate constructor that is missing or public, a child
entity with a public constructor, and an aggregate- or entity-state that names the wrong type as
itself — the last of which is also, for the aggregate-state case, the one analyzer rule that is the
direct compile-time twin of a startup check above rather than an unrelated convention; see
[ADR 0014](architecture/0014-a-compile-time-analyzer-catches-four-startup-checks.md) and
[ADR 0015](architecture/0015-two-more-analyzer-rules-catch-self-binding.md).

### Infrastructure provisioning

Whether a host may create schema, exchanges and queues on its own:
`InfrastructureProvisioning.Never` or `AtStartup`. A service normally says `Never` and leaves it to
a migration job, so that starting a second instance cannot change the database.

## Outcomes

### Result

The return of every handler: `Result` for a command without a value, `Result<TResult>` otherwise. It
is either a success or a list of failures, never both, and it is how expected outcomes are reported.
Exceptions stay for the unexpected.

### Failure

One reason a result did not succeed: a `Code` a caller can branch on, a human `Message`, a
`Category`, and an optional `Target` naming the field it is about. Persistence contributes its own
codes — `persistence.concurrency_conflict` and `persistence.unique_violation` — translated from
driver exceptions by an `IPersistenceFaultTranslator` rather than surfacing as exceptions.

### Failure category

The coarse kind of a failure, so that a caller can map it without knowing every code:
`FailureCategory` is `Validation`, `BusinessRule`, `NotFound`, `Conflict` or `Forbidden`. It is
deliberately not an HTTP status — mapping it to one belongs to the host, not to the domain.
