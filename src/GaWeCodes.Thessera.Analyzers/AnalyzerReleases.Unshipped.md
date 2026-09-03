### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
THSS0001 | Thessera.Design | Error | AggregateNameAnalyzer, aggregate is missing [AggregateName]
THSS0002 | Thessera.Design | Error | DomainEventNameAnalyzer, domain event is missing [EventName]
THSS0003 | Thessera.Design | Error | AggregateConstructorAnalyzer, aggregate constructor is missing or public
THSS0004 | Thessera.Design | Error | ChildEntityConstructorAnalyzer, child entity exposes a public constructor
THSS0005 | Thessera.Design | Error | AggregateStateSelfBindingAnalyzer, aggregate state does not name itself as TSelf
THSS0006 | Thessera.Design | Error | ChildEntityStateSelfBindingAnalyzer, child entity state does not name itself as TSelf
THSS0007 | Thessera.Design | Error | CommandHandlerSingleStoreAnalyzer, command handler injects repositories for more than one aggregate
THSS0008 | Thessera.Design | Error | CommandHandlerRawSessionAnalyzer, command handler injects a store's raw DbContext or IDocumentSession directly
