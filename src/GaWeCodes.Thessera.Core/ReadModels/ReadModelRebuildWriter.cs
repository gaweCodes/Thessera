using GaWeCodes.Thessera.Application.ReadModels;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.ReadModels;

public sealed class ReadModelRebuildWriter(IServiceScopeFactory scopeFactory)
{
    public const int BatchSize = 500;

    public async Task ClearAsync<TAggregate, TKey>(CancellationToken cancellationToken)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        using var scope = scopeFactory.CreateScope();

        foreach (var rebuilder in RebuildersOf<TAggregate, TKey>(scope.ServiceProvider))
        {
            await rebuilder.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task WriteAsync<TAggregate, TKey>(
        IReadOnlyList<TAggregate> batch,
        CancellationToken cancellationToken)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(batch);

        using var scope = scopeFactory.CreateScope();
        var rebuilders = RebuildersOf<TAggregate, TKey>(scope.ServiceProvider);

        foreach (var aggregate in batch)
        {
            foreach (var rebuilder in rebuilders)
            {
                await rebuilder.RebuildAsync(aggregate, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IReadModelRebuilder<TAggregate, TKey>[] RebuildersOf<TAggregate, TKey>(
        IServiceProvider services)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        var rebuilders = services.GetServices<IReadModelRebuilder<TAggregate, TKey>>().ToArray();

        return rebuilders.Length > 0
            ? rebuilders
            : throw new InvalidOperationException(
                $"No {typeof(IReadModelRebuilder<,>).Name} was registered for aggregate '{typeof(TAggregate)}'. " +
                "A rebuild that projects nothing reports success while the read model stays empty; " +
                "register one through AddHandlersFrom, or do not run the rebuild.");
    }
}
