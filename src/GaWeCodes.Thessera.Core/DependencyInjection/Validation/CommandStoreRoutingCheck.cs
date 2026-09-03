using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Dispatching;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

/// <summary>
/// Enforces that one command commits through one store, and builds the routing table
/// <see cref="UnitOfWorkBehavior{TRequest,TResponse}"/> reads from.
/// </summary>
/// <remarks>
/// A no-op on a host with at most one store — the overwhelming majority — since every command then
/// commits through that host's single unkeyed <c>IUnitOfWork</c> exactly as before this feature
/// existed. Only once a second store is selected does this walk every registered command handler.
/// </remarks>
internal sealed class CommandStoreRoutingCheck(
    PersistenceSelection persistence,
    IServiceCollection services,
    CommandStoreRouter router) : SynchronousStartupCheck
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        if (!HasMoreThanOneStore())
        {
            return;
        }

        foreach (var handlerType in HandlerTypes())
        {
            RouteHandler(handlerType);
        }
    }

    private bool HasMoreThanOneStore() =>
        persistence.Choices.Count(static choice => choice.IsSelected) > 1;

    private void RouteHandler(Type handlerType)
    {
        var choices = RepositoryAggregatesOf(handlerType)
            .Select(persistence.ResolveChoice)
            .OfType<PersistenceChoice>()
            .Distinct()
            .ToList();

        if (choices.Count > 1)
        {
            throw new InvalidOperationException(
                $"'{handlerType}' injects repositories for aggregates split across {choices.Count} different " +
                "stores. A command commits through exactly one unit of work, so every aggregate a single " +
                "command handler touches must be owned by the same store. Split the command in two, or move " +
                "the aggregates so they are claimed by the same store's 'forAggregates' list.");
        }

        if (choices is not [{ ClaimedAggregates.Count: > 0 } choice])
        {
            return;
        }

        foreach (var commandType in CommandTypesHandledBy(handlerType))
        {
            router.Route(commandType, choice.StoreId);
        }
    }

    private IEnumerable<Type> HandlerTypes() =>
        services
            .Select(static descriptor => descriptor.ImplementationType)
            .Where(static type => type is { IsAbstract: false } && !type.IsGenericTypeDefinition)
            .Select(static type => type!)
            .Where(static type => Array.Exists(type.GetInterfaces(), IsCommandHandlerContract))
            .Distinct();

    private static bool IsCommandHandlerContract(Type contract) =>
        contract.IsGenericType
        && (contract.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
            || contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

    private static IEnumerable<Type> RepositoryAggregatesOf(Type handlerType) =>
        handlerType
            .GetConstructors()
            .SelectMany(static constructor => constructor.GetParameters())
            .Select(static parameter => parameter.ParameterType)
            .Where(static type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRepository<,>))
            .Select(static type => type.GenericTypeArguments[0])
            .Distinct();

    private static IEnumerable<Type> CommandTypesHandledBy(Type handlerType) =>
        handlerType
            .GetInterfaces()
            .Where(IsCommandHandlerContract)
            .Select(static contract => contract.GenericTypeArguments[0])
            .Distinct();
}
