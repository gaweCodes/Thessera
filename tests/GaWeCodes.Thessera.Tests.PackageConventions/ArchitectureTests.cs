using System.Reflection;
using System.Text.Json;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenInfrastructureDependencies =
    [
        "Microsoft.EntityFrameworkCore",
        "Marten",
        "Wolverine",
        "Npgsql",
        "JasperFx",
        "RabbitMQ",
    ];

    private static readonly Assembly Domain = typeof(DomainEvent).Assembly;
    private static readonly Assembly Application = typeof(Result).Assembly;

    [Fact]
    public void Domain_HasNoBuildingBlockOrInfrastructurePackageReferences()
    {
        var references = ReferencedAssemblyNames(Domain);

        Assert.DoesNotContain(references, name => name.StartsWith("GaWeCodes.Thessera", StringComparison.Ordinal));
        Assert.DoesNotContain(references, IsForbiddenInfrastructureDependency);
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        var references = ReferencedAssemblyNames(Application);

        Assert.Contains("GaWeCodes.Thessera.Domain", references);
        Assert.DoesNotContain("GaWeCodes.Thessera.Core", references);
        Assert.DoesNotContain(references, IsForbiddenInfrastructureDependency);
    }

    [Fact]
    public void Domain_DoesNotReferenceApplicationOrInfrastructure()
    {
        var references = ReferencedAssemblyNames(Domain);

        Assert.DoesNotContain("GaWeCodes.Thessera.Application", references);
        Assert.DoesNotContain("GaWeCodes.Thessera.Core", references);
    }

    [Fact]
    public void Infrastructure_ReferencesBothApplicationAndDomain()
    {
        var references = ReferencedAssemblyNames(typeof(ServiceCollectionExtensions).Assembly);

        Assert.Contains("GaWeCodes.Thessera.Application", references);
        Assert.Contains("GaWeCodes.Thessera.Domain", references);
    }

    [Fact]
    public void Domain_DeclaresNoInfrastructurePackage_NotEvenAnUnusedOne()
    {
        var packages = ResolvedPackages("src/GaWeCodes.Thessera.Domain");

        Assert.DoesNotContain(packages, IsForbiddenInfrastructureDependency);
    }

    [Fact]
    public void Application_DeclaresNoInfrastructurePackage_NotEvenAnUnusedOne()
    {
        var packages = ResolvedPackages("src/GaWeCodes.Thessera.Application");

        Assert.DoesNotContain(packages, IsForbiddenInfrastructureDependency);
    }

    private static IReadOnlyCollection<string> ResolvedPackages(string projectDirectory)
    {
        var assets = Path.Combine(
            ThesseraLayout.Root,
            projectDirectory.Replace('/', Path.DirectorySeparatorChar),
            "obj",
            "project.assets.json");

        Assert.True(File.Exists(assets), $"'{assets}' does not exist; restore the solution before running this test.");

        using var document = JsonDocument.Parse(File.ReadAllText(assets));

        return document.RootElement.TryGetProperty("targets", out var targets)
            ?
            [
                .. targets.EnumerateObject()
                    .SelectMany(target => target.Value.EnumerateObject())
                    .Select(library => library.Name.Split('/')[0]),
            ]
            : [];
    }

    private static IReadOnlyCollection<string> ReferencedAssemblyNames(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty)];

    private static bool IsForbiddenInfrastructureDependency(string name) =>
        Array.Exists(
            ForbiddenInfrastructureDependencies,
            forbidden => name.StartsWith(forbidden, StringComparison.Ordinal));
}
