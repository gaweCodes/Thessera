using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Domain.Aggregates;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

internal sealed class AggregatePersistenceMatchCheck(
    PersistenceSelection persistence,
    IServiceCollection services) : SynchronousStartupCheck
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        var selected = persistence.Choices.Where(static choice => choice.IsSelected).ToList();

        if (selected.Count == 0)
        {
            ThrowIfWaivedWithoutStore();
            return;
        }

        var requested = RequestedAggregates().ToList();

        foreach (var choice in selected)
        {
            RunForChoice(choice, requested);
        }
    }

    private void RunForChoice(PersistenceChoice choice, List<Type> requested)
    {
        var adapter = choice.Adapter!;
        var style = adapter.AggregateStyle;

        if (persistence.IsEventHistoryWaived && style == AggregateStyle.EventSourced)
        {
            throw new InvalidOperationException(
                $"WithoutEventHistory was combined with {adapter.Description}. That call waives the event " +
                "history of aggregates that declare one, so it only means anything for a store that keeps the " +
                "current state instead of the events. This store keeps the events, so the history it waives is " +
                "the one being written. Remove the call.");
        }

        var owned = choice.ClaimedAggregates.Count > 0
            ? requested.Where(choice.ClaimedAggregates.Contains)
            : requested.Where(aggregate => ReferenceEquals(persistence.ResolveChoice(aggregate), choice));

        var mismatched = owned
            .Where(aggregate => StyleOf(aggregate) != style)
            .Select(aggregate => $"'{aggregate}'")
            .ToList();

        if (mismatched.Count == 0)
        {
            return;
        }

        if (style == AggregateStyle.StateStored && persistence.IsEventHistoryWaived)
        {
            return;
        }

        throw new InvalidOperationException(
            style == AggregateStyle.EventSourced
                ? $"{adapter.Description} stores an aggregate as the stream of events that produced it, but these " +
                    "aggregates keep no history because they derive from AggregateRoot instead of " +
                    "EventSourcedAggregateRoot. Their repository cannot even be constructed, and that failure " +
                    "surfaces on the first command that asks for one rather than here: " +
                    $"{Join(mismatched)}. Derive them from EventSourcedAggregateRoot, or select " +
                    "UseEfCoreStateStore<TContext>(writeConnectionString) for this host."
                : $"{adapter.Description} keeps only the current state of an aggregate, but these aggregates " +
                    "derive from EventSourcedAggregateRoot and therefore declare their events to be the record " +
                    "of truth: " + $"{Join(mismatched)}. Their state is written and read back correctly and " +
                    "their events still reach the outbox, so nothing fails at run time — what is missing is the " +
                    "stream. No event is ever stored, so the aggregate can never be replayed, audited, or " +
                    "inspected as of an earlier point in time, and that loss is silent and permanent. Select " +
                    "UseMartenEventStore(writeConnectionString) to keep the history, derive them from " +
                    "AggregateRoot if they never needed one, or add WithoutEventHistory() to state that this " +
                    "host gives the history up on purpose.");
    }

    private void ThrowIfWaivedWithoutStore()
    {
        if (!persistence.IsEventHistoryWaived)
        {
            return;
        }

        throw new InvalidOperationException(
            "WithoutEventHistory was called, but this host selected no persistence strategy. The call waives the "
            + "event history of aggregates stored as current state, so it needs a store that keeps state. Select "
            + "UseEfCoreStateStore<TContext>(writeConnectionString), or remove the call.");
    }

    private static string Join(List<string> aggregates) =>
        string.Join(", ", aggregates.Take(5))
        + (aggregates.Count > 5 ? $" and {aggregates.Count - 5} more" : string.Empty);

    private IEnumerable<Type> RequestedAggregates() =>
        services
            .Select(static descriptor => descriptor.ImplementationType)
            .Where(static type => type is { IsAbstract: false } && !type.IsGenericTypeDefinition)
            .SelectMany(static type => type!.GetConstructors())
            .SelectMany(static constructor => constructor.GetParameters())
            .Select(static parameter => parameter.ParameterType)
            .Where(static type => type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(IRepository<,>))
            .Select(static type => type.GenericTypeArguments[0])
            .Distinct();

    private static AggregateStyle StyleOf(Type aggregate) =>
        Array.Exists(
            aggregate.GetInterfaces(),
            static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IEventSourcedAggregateRoot<>))
            ? AggregateStyle.EventSourced
            : AggregateStyle.StateStored;
}
