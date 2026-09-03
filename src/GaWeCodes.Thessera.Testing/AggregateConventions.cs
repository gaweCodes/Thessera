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
/// The runtime catches only some of these, and later than is useful: that an aggregate has a
/// parameterless constructor is checked while a host is composed, but only for a host that selected
/// a persistence strategy — a host on <c>UseNoPersistence()</c> never gets it. That a domain event
/// carries <c>[EventName]</c> is checked unconditionally when the catalogue is built. The constructor
/// visibility of aggregates and children is not checked at all. Calling
/// <see cref="Verify(IEnumerable{Assembly})"/> from one test moves the lot into the build, where the
/// break shows up in a pull request rather than in a deployed service.
/// </remarks>
public static class AggregateConventions
{
    /// <summary>
    /// Verifies every aggregate, child entity and domain event found in the given assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The assemblies holding your domain model — normally one, named through a type in it.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// At least one convention is broken. The message lists <em>every</em> violation of the run, not
    /// just the first, so one test run is enough to see all of them. It also fails when the
    /// assemblies contain neither an aggregate nor a domain event: a convention test that finds
    /// nothing passes every check without asserting anything and stays green forever.
    /// </exception>
    /// <remarks>
    /// Checked here: every aggregate has a parameterless constructor and it is not public; every
    /// aggregate carries <c>[AggregateName]</c>; every domain event carries <c>[EventName]</c>; and
    /// child entities keep their constructors internal.
    /// </remarks>
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
