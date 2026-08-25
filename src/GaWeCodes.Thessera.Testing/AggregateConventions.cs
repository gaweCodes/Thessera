using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Testing;

/// <summary>
/// Checks the rules an aggregate has to follow for the runtime to be able to store and rebuild it.
/// </summary>
/// <remarks>
/// The host verifies most of this at startup, which is too late to be cheap: the break shows up in
/// a deployed service rather than in a pull request. Calling <see cref="Verify(IEnumerable{Assembly})"/>
/// from one test moves every check into the build.
/// </remarks>
public static class AggregateConventions
{
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public static void Verify(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var types = assemblies
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .ToList();

        var aggregates = types.Where(static type => DerivesFrom(type, typeof(AggregateRoot<,>))).ToList();
        var children = types.Where(static type => DerivesFrom(type, typeof(Entity<,>))).ToList();
        var domainEvents = types.Where(static type => typeof(IDomainEvent).IsAssignableFrom(type)).ToList();

        var violations = new List<string>();

        // A run that finds nothing passes every check below without asserting anything. That is the
        // most expensive failure mode a convention test has, because it stays green forever.
        if (aggregates.Count == 0 && domainEvents.Count == 0)
        {
            violations.Add(
                "No aggregate and no domain event was found in " +
                string.Join(", ", assemblies.Select(static assembly => $"'{assembly.GetName().Name}'")) +
                ". Either the wrong assembly was handed in, or the check is silently vacuous.");
        }

        foreach (var aggregate in aggregates)
        {
            if (aggregate.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    Type.EmptyTypes) is null)
            {
                violations.Add(
                    $"'{aggregate}' has no parameterless constructor, so no repository can reconstitute it.");
            }

            if (aggregate.GetConstructor(Type.EmptyTypes) is not null)
            {
                violations.Add(
                    $"'{aggregate}' exposes a public parameterless constructor, so it can be created without going " +
                    "through its factory. Keep the parameterless constructor private.");
            }

            if (aggregate.GetCustomAttribute<AggregateNameAttribute>(inherit: false) is null)
            {
                violations.Add(
                    $"'{aggregate}' needs an [AggregateName]. The name prefixes every event stream, so renaming the " +
                    "class would otherwise orphan every stream that already exists.");
            }
        }

        foreach (var domainEvent in domainEvents.Where(static type =>
                     type.GetCustomAttribute<EventNameAttribute>(inherit: false) is null))
        {
            violations.Add(
                $"'{domainEvent}' needs an [EventName]. The class name is not a persistence contract.");
        }

        foreach (var child in children.Where(static type => type.GetConstructors().Length != 0))
        {
            violations.Add(
                $"'{child}' exposes a public constructor, so a child hull can be built without its root and would " +
                "have no channel to raise through. Keep the constructor internal.");
        }

        if (violations.Count != 0)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{violations.Count} aggregate convention(s) are broken:") +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations.Select(static violation => "  - " + violation)));
        }
    }

    private static bool DerivesFrom(Type type, Type openGeneric)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGeneric)
            {
                return true;
            }
        }

        return false;
    }
}
