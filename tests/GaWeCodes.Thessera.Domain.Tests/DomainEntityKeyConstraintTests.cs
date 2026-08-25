using System.Reflection;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainEntityKeyConstraintTests
{
    [Fact]
    public void EveryEntityKeyTypeParameter_AlsoRequiresValueEquality()
    {
        Assert.Empty(KeyParametersWithoutValueEquality(typeof(IEntityKey).Assembly));
    }

    [Fact]
    public void TheDetector_FindsAParameterThatOnlyRequiresIEntityKey()
    {
        Assert.Contains(
            $"{typeof(DomainWithoutValueEqualityOnTheType<>).FullName}.TKey",
            KeyParametersWithoutValueEquality(typeof(DomainEntityKeyConstraintTests).Assembly));
    }

    [Fact]
    public void TheDetector_AlsoLooksAtGenericMethods()
    {
        Assert.Contains(
            $"{typeof(DomainWithoutValueEqualityOnAMethod).FullName}.{nameof(DomainWithoutValueEqualityOnAMethod.Take)}.TKey",
            KeyParametersWithoutValueEquality(typeof(DomainEntityKeyConstraintTests).Assembly));
    }

    internal static string[] KeyParametersWithoutValueEquality(Assembly assembly)
    {
        var exported = assembly.GetExportedTypes();

        var fromTypes = exported
            .Where(type => type.IsGenericTypeDefinition)
            .SelectMany(type => type.GetGenericArguments())
            .Select(argument => (Argument: argument, Name: $"{argument.DeclaringType?.FullName}.{argument.Name}"));

        var fromMethods = exported
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.IsGenericMethodDefinition)
            .SelectMany(method => method.GetGenericArguments()
                .Select(argument => (
                    Argument: argument,
                    Name: $"{method.DeclaringType?.FullName}.{method.Name}.{argument.Name}")));

        return
        [
            .. fromTypes.Concat(fromMethods)
                .Where(candidate => candidate.Argument.GetGenericParameterConstraints().Any(IsEntityKey))
                .Where(candidate => !candidate.Argument.GetGenericParameterConstraints().Any(IsValueEquatable))
                .Select(candidate => candidate.Name),
        ];
    }

    private static bool IsEntityKey(Type constraint)
    {
        return constraint == typeof(IEntityKey)
            || (constraint.IsGenericType && constraint.GetGenericTypeDefinition() == typeof(IEntityKey<>));
    }

    private static bool IsValueEquatable(Type constraint)
    {
        return constraint.IsGenericType && constraint.GetGenericTypeDefinition() == typeof(IEquatable<>);
    }
}

public sealed class DomainWithoutValueEqualityOnTheType<TKey>
    where TKey : struct, IEntityKey
{
    public TKey Key { get; init; }
}

public sealed class DomainWithoutValueEqualityOnAMethod
{
    public static TKey Take<TKey>(TKey key)
        where TKey : struct, IEntityKey => key;
}
