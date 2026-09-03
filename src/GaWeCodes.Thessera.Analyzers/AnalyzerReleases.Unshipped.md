### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
THSS0001 | Thessera.Design | Error | Non-abstract type deriving from `AggregateRoot<TKey, TState>` that does not carry `[AggregateName]` directly.
THSS0002 | Thessera.Design | Error | Non-abstract type implementing `IDomainEvent` that does not carry `[EventName]` directly.
THSS0003 | Thessera.Design | Error | Non-abstract type deriving from `AggregateRoot<TKey, TState>` that has no parameterless constructor, or whose parameterless constructor is `public`.
THSS0004 | Thessera.Design | Error | Non-abstract type deriving from `Entity<TKey, TState>` that exposes any `public` constructor.
THSS0005 | Thessera.Design | Error | Non-abstract type deriving from `AggregateState<TSelf, TKey>` whose first type argument does not name the deriving type itself.
THSS0006 | Thessera.Design | Error | Non-abstract type deriving from `EntityState<TSelf, TKey>` whose first type argument does not name the deriving type itself.
THSS0007 | Thessera.Design | Error | Command handler whose constructor injects `IRepository<TAggregate, TKey>` for more than one distinct aggregate type.
THSS0008 | Thessera.Design | Error | Command handler whose constructor injects an EF Core `DbContext`-derived type or a Marten `IDocumentSession` directly, instead of only `IRepository<TAggregate, TKey>`.
