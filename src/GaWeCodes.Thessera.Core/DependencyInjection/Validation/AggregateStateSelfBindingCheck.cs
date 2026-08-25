using System.Reflection;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Domain.Aggregates;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

internal sealed class AggregateStateSelfBindingCheck(IReadOnlyCollection<Assembly> assemblies)
    : SynchronousStartupCheck
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        var misbound = assemblies
            .Distinct()
            .SelectMany(TypesOf)
            .Where(static type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Select(static type => (Type: type, Declared: DeclaredSelf(type)))
            .Where(static candidate => candidate.Declared is not null && candidate.Declared != candidate.Type)
            .Select(static candidate => $"'{candidate.Type}' declares '{candidate.Declared}' as its own type")
            .ToList();

        if (misbound.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "An aggregate state must name itself as the first type argument of AggregateState, because that is " +
            "what lets it return a copy of itself. These states name a different type, which the compiler accepts " +
            "and which then fails as an unexplained InvalidCastException the first time the aggregate applies an " +
            $"event: {string.Join(", ", misbound.Take(5))}" +
            (misbound.Count > 5 ? $" and {misbound.Count - 5} more" : string.Empty) +
            ". Change each of them to name itself.");
    }

    private static Type? DeclaredSelf(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(AggregateState<,>))
            {
                return current.GenericTypeArguments[0];
            }
        }

        return null;
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException loadFailure)
        {
            return loadFailure.Types.OfType<Type>();
        }
    }
}
