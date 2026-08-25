using System.Reflection;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Tests;

public sealed class ApplicationEntityKeyConstraintTests
{
    [Fact]
    public void EveryEntityKeyTypeParameter_AlsoRequiresValueEquality()
    {
        Assert.Empty(KeyParametersWithoutValueEquality(typeof(IUnitOfWork).Assembly));
    }

    [Fact]
    public void TheDetector_FindsAParameterThatOnlyRequiresIEntityKey()
    {
        Assert.Equal(
            [$"{typeof(ApplicationWithoutValueEqualityOnTheType<>).FullName}.TKey"],
            KeyParametersWithoutValueEquality(typeof(ApplicationEntityKeyConstraintTests).Assembly)
                .Where(offender => offender.Contains("OnTheType", StringComparison.Ordinal))
                .ToArray());
    }

    [Fact]
    public void TheDetector_AlsoLooksAtGenericMethods()
    {
        Assert.Contains(
            $"{typeof(ApplicationWithoutValueEqualityOnAMethod).FullName}.{nameof(ApplicationWithoutValueEqualityOnAMethod.Take)}.TKey",
            KeyParametersWithoutValueEquality(typeof(ApplicationEntityKeyConstraintTests).Assembly));
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

public sealed class ApplicationWithoutValueEqualityOnTheType<TKey>
    where TKey : struct, IEntityKey
{
    public TKey Key { get; init; }
}

public sealed class ApplicationWithoutValueEqualityOnAMethod
{
    public static TKey Take<TKey>(TKey key)
        where TKey : struct, IEntityKey => key;
}
