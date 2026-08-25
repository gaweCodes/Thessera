using System.Reflection;
using GaWeCodes.Thessera.Core.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class CoreFacadeVendorNeutralityTests
{
    private static readonly string[] VendorAssemblies =
    [
        "Marten",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "RabbitMQ.Client",
        "Wolverine",
    ];

    private static readonly string[] MessagingVendorNamespaces =
    [
        "Wolverine",
        "JasperFx",
    ];

    public static TheoryData<Type> FacadeTypes =>
    [
        typeof(ThesseraOptions),
        typeof(ServiceCollectionExtensions),
    ];

    [Theory]
    [MemberData(nameof(FacadeTypes))]
    public void NoFacadeSignatureNamesAVendorType(Type facade)
    {
        ArgumentNullException.ThrowIfNull(facade);

        Assert.Empty(FindVendorTypes(facade));
    }

    [Fact]
    public void TheHostBuilderEntryPointNoLongerNamesWolverine()
    {
        Assert.Empty(FindVendorTypes(typeof(HostApplicationBuilderExtensions)));
    }

    [Fact]
    public void TheCompositionRootAndEntryPointCarryNoVendorUsing()
    {
        string[] coreOwnedFiles =
        [
            Path.Combine("DependencyInjection", "ThesseraComposition.cs"),
            Path.Combine("DependencyInjection", "HostApplicationBuilderExtensions.cs"),
            Path.Combine("DependencyInjection", "ServiceCollectionExtensions.cs"),
            Path.Combine("DependencyInjection", "ThesseraOptions.cs"),
        ];

        var coupled = WolverineCoupledCoreFiles();

        Assert.Empty(coupled.Intersect(coreOwnedFiles, StringComparer.Ordinal));
    }

    [Fact]
    public void TheCoreAssemblyNoLongerReferencesRabbitMq()
    {
        var referenced = typeof(ThesseraOptions).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(referenced);
    }

    [Fact]
    public void TheCoreCarriesNoWolverineCouplingAtAll()
    {
        var coupled = WolverineCoupledCoreFiles();

        Assert.True(
            coupled.Length == 0,
            $"The core has {coupled.Length} Wolverine-coupled files. Phase 2.5 brought this number to zero "
            + $"and it must stay there. Put the new code behind a core-owned contract and let "
            + $"GaWeCodes.Thessera.Wolverine implement it.{Environment.NewLine}"
            + string.Join(Environment.NewLine, coupled));
    }

    [Fact]
    public void TheCoreAssemblyNoLongerReferencesWolverine()
    {
        var referenced = typeof(ThesseraOptions).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.Contains("Wolverine", StringComparison.OrdinalIgnoreCase)
                || name.Contains("JasperFx", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(referenced);
    }

    private static string[] WolverineCoupledCoreFiles()
    {
        var core = Path.Combine(
            ThesseraLayout.Root,
            "src",
            "GaWeCodes.Thessera.Core");

        Assert.True(Directory.Exists(core), $"'{core}' does not exist; the core source directory cannot be located.");

        return
        [
            .. ThesseraLayout
                .SourceFiles(core)
                .Where(file => File.ReadLines(file).Any(NamesAVendorNamespace))
                .Select(file => Path.GetRelativePath(core, file))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static bool NamesAVendorNamespace(string line) =>
        line.TrimStart().StartsWith("using", StringComparison.Ordinal)
        && Array.Exists(
            MessagingVendorNamespaces,
            vendor => line.Contains(vendor, StringComparison.Ordinal));

    [Fact]
    public void TheDetectorRecognisesAVendorTypeWhereOneIsExpected()
    {
        Assert.NotEmpty(FindVendorTypes(typeof(EfCoreStateStoreExtensions)));
    }

    private static string[] FindVendorTypes(Type declaringType) =>
        declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SelectMany(method => method
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType)
                .SelectMany(Unwrap)
                .Where(IsVendorType)
                .Select(type => $"{declaringType.Name}.{method.Name} names '{type.Name}'"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        foreach (var argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (var nested in Unwrap(argument))
            {
                yield return nested;
            }
        }
    }

    private static bool IsVendorType(Type type)
    {
        var assembly = type.Assembly.GetName().Name;

        return assembly is not null
            && Array.Exists(
                VendorAssemblies,
                vendor => assembly.Equals(vendor, StringComparison.Ordinal)
                    || assembly.StartsWith($"{vendor}.", StringComparison.Ordinal));
    }
}
