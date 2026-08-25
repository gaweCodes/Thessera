using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class AggregateStateModelCheck<TContext>(IServiceProvider serviceProvider) : SynchronousStartupCheck
    where TContext : DbContext
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var offenders = new List<string>();
        var keyless = new List<string>();
        var derivedNames = new List<string>();
        var visited = new HashSet<IEntityType>();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            if (!entityType.IsOwned() && IsAggregateState(entityType.ClrType))
            {
                Validate(entityType, offenders, keyless, derivedNames, visited);
            }
        }

        if (offenders.Count > 0)
        {
            throw new InvalidOperationException(
                "Aggregate state mapping validation failed at startup. A child of an aggregate lives and dies " +
                "with that aggregate, so it maps as an owned type (OwnsOne/OwnsMany, optionally ToJson). EF Core " +
                "loads owned children with their owner and reconciles them against their key when the state is " +
                "replaced; a navigation to an independent entity type is loaded by neither and is silently lost " +
                $"on commit: {string.Join("; ", offenders)}.");
        }

        if (keyless.Count > 0)
        {
            throw new InvalidOperationException(
                "Aggregate state mapping validation failed at startup. A child of an aggregate has its own "
                + "identity, so an owned collection declares that identity as its key (HasKey) with a single, "
                + "non-shadow property. Without it the commit cannot match a replaced child against the tracked "
                + $"one and would rewrite rows instead of updating them: {string.Join("; ", keyless)}.");
        }

        if (derivedNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Aggregate state mapping validation failed at startup. A stored field name is a persistence "
                + "contract, so it is declared and never derived from the CLR property name. "
                + "Without an explicit HasColumnName a rename of the property renames the column, which turns a "
                + "pure refactoring into a destructive migration; with one it costs nothing. Declare a name for: "
                + $"{string.Join("; ", derivedNames)}.");
        }
    }

    private static void Validate(
        IEntityType entityType,
        List<string> offenders,
        List<string> keyless,
        List<string> derivedNames,
        HashSet<IEntityType> visited)
    {
        if (!visited.Add(entityType))
        {
            return;
        }

        var storedNameAnnotation = entityType.IsMappedToJson()
            ? RelationalAnnotationNames.JsonPropertyName
            : RelationalAnnotationNames.ColumnName;

        foreach (var property in entityType.GetProperties())
        {
            if (!property.IsShadowProperty() && property.FindAnnotation(storedNameAnnotation) is null)
            {
                derivedNames.Add($"'{entityType.ClrType.Name}.{property.Name}'");
            }
        }

        foreach (var navigation in entityType.GetNavigations())
        {
            if (navigation.ForeignKey.IsOwnership)
            {
                if (!navigation.IsOnDependent)
                {
                    if (navigation.IsCollection
                        && !navigation.TargetEntityType.IsMappedToJson()
                        && !AggregateStateGraph.IsReconcilableByKey(navigation.TargetEntityType))
                    {
                        keyless.Add($"'{entityType.ClrType.Name}.{navigation.Name}'");
                    }

                    Validate(navigation.TargetEntityType, offenders, keyless, derivedNames, visited);
                }

                continue;
            }

            offenders.Add(Describe(entityType, navigation.Name, navigation.TargetEntityType));
        }

        foreach (var navigation in entityType.GetSkipNavigations())
        {
            offenders.Add(Describe(entityType, navigation.Name, navigation.TargetEntityType));
        }
    }

    private static string Describe(IEntityType declaringType, string navigationName, IEntityType targetType) =>
        $"'{declaringType.ClrType.Name}.{navigationName}' navigates to the independent entity type " +
        $"'{targetType.ClrType.Name}'";

    private static bool IsAggregateState(Type clrType)
    {
        for (var current = clrType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateState<,>))
            {
                return true;
            }
        }

        return false;
    }
}
