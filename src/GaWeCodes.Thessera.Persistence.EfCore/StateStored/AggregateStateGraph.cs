using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal static class AggregateStateGraph
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> ReadOnlyProbes = new();

    public static void EnsureTrackableCollections(DbContext context, object state)
    {
        var entityType = context.Model.FindEntityType(state.GetType());

        if (entityType is not null)
        {
            EnsureTrackableCollections(entityType, state);
        }
    }

    public static void Reconcile(EntityEntry tracked, object state)
    {
        EnsureTrackableCollections(tracked.Metadata, state);
        Apply(tracked, state);
    }

    public static bool IsReconcilableByKey(IEntityType entityType) => FindKeyProperty(entityType) is not null;

    private static IProperty? FindKeyProperty(IEntityType entityType)
    {
        var key = entityType.FindPrimaryKey();

        return key is { Properties.Count: 1 } && !key.Properties[0].IsShadowProperty()
            ? key.Properties[0]
            : null;
    }

    private static void Apply(EntityEntry entry, object state)
    {
        entry.CurrentValues.SetValues(state);

        foreach (var navigation in entry.Navigations)
        {
            var value = ReadValue(navigation.Metadata, state);

            if (navigation is CollectionEntry collection)
            {
                ReconcileCollection(collection, value);
            }
            else
            {
                ReconcileReference((ReferenceEntry)navigation, value);
            }
        }
    }

    private static void ReconcileReference(ReferenceEntry reference, object? value)
    {
        if (reference.TargetEntry is { } target && value is not null)
        {
            Apply(target, value);
            return;
        }

        reference.CurrentValue = value;
    }

    private static void ReconcileCollection(CollectionEntry collection, object? value)
    {
        var children = EnsureWritable(collection.Metadata, value);

        if (FindKeyProperty(collection.Metadata.TargetEntityType) is not { } keyProperty)
        {
            ReplaceAsSingleColumn(collection, children);
            return;
        }

        ReconcileByKey(collection, keyProperty, children);
    }

    private static void ReplaceAsSingleColumn(CollectionEntry collection, IEnumerable children) =>
        collection.CurrentValue = children;

    private static void ReconcileByKey(CollectionEntry collection, IProperty keyProperty, IEnumerable children)
    {
        var owner = collection.EntityEntry.Entity;
        var context = collection.EntityEntry.Context;
        var accessor = collection.Metadata.GetCollectionAccessor()!;
        var tracked = new Dictionary<object, object>();

        foreach (var child in collection.CurrentValue ?? Array.Empty<object>())
        {
            tracked[KeyOf(keyProperty, child)] = child;
        }

        foreach (var child in children)
        {
            if (tracked.Remove(KeyOf(keyProperty, child), out var existing))
            {
                Apply(context.Entry(existing), child);
            }
            else
            {
                accessor.Add(owner, child, forMaterialization: false);
            }
        }

        foreach (var orphan in tracked.Values)
        {
            accessor.Remove(owner, orphan);
        }
    }

    private static object KeyOf(IProperty keyProperty, object child) =>
        keyProperty.GetGetter().GetClrValue(child)
        ?? throw new NotSupportedException(
            $"The child '{keyProperty.DeclaringType.ClrType.Name}' carries no value for its key property "
            + $"'{keyProperty.Name}'. A child of an aggregate has its own identity.");

    private static void EnsureTrackableCollections(IEntityType entityType, object state)
    {
        foreach (var navigation in entityType.GetNavigations())
        {
            var value = ReadValue(navigation, state);

            if (!navigation.IsCollection)
            {
                if (value is not null)
                {
                    EnsureTrackableCollections(navigation.TargetEntityType, value);
                }

                continue;
            }

            foreach (var child in EnsureWritable(navigation, value))
            {
                if (child is not null)
                {
                    EnsureTrackableCollections(navigation.TargetEntityType, child);
                }
            }
        }
    }

    private static object? ReadValue(INavigationBase navigation, object state) =>
        navigation.GetGetter().GetClrValue(state);

    private static IEnumerable EnsureWritable(INavigationBase navigation, object? value) => value switch
    {
        null => throw new NotSupportedException(
            $"The collection '{Describe(navigation)}' is null. EF Core adds and removes child entities " +
            "through the collection instance itself, so a child collection must never be null: build an " +
            "empty collection to express 'no children'."),
        _ when IsWritable(value) => (IEnumerable)value,
        _ => throw new NotSupportedException(
            $"The collection '{Describe(navigation)}' is read-only or fixed-size ('{value.GetType().Name}'). " +
            "EF Core adds and removes child entities through the collection instance itself, so it must be " +
            "writable. Beware that a collection expression assigned to an IReadOnlyCollection<T> does not " +
            "compile to a List<T>: build the value with ToList() instead."),
    };

    private static string Describe(INavigationBase navigation) =>
        $"{navigation.DeclaringEntityType.ClrType.Name}.{navigation.Name}";

    private static bool IsWritable(object value)
    {
        var probe = ReadOnlyProbes.GetOrAdd(value.GetType(), static type => Array
            .Find(
                type.GetInterfaces(),
                static contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(ICollection<>))
            ?.GetProperty(nameof(ICollection<object>.IsReadOnly)));

        return probe is not null && probe.GetValue(value) is false;
    }
}
