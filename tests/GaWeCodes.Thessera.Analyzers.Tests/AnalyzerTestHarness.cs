using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Tests;

internal static class AnalyzerTestHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithDomain = new(() => BuildReferences(includeDomain: true, includeApplication: false, includeStores: false));
    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithoutDomain = new(() => BuildReferences(includeDomain: false, includeApplication: false, includeStores: false));
    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithApplication = new(() => BuildReferences(includeDomain: true, includeApplication: true, includeStores: false));
    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithStores = new(() => BuildReferences(includeDomain: true, includeApplication: true, includeStores: true));

    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        bool referenceDomain = true,
        bool referenceApplication = false,
        bool referenceStores = false)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = referenceStores
            ? ReferencesWithStores.Value
            : referenceApplication
                ? ReferencesWithApplication.Value
                : referenceDomain ? ReferencesWithDomain.Value : ReferencesWithoutDomain.Value;

        var compilation = CSharpCompilation.Create(
            assemblyName: "GaWeCodes.Thessera.Analyzers.Tests.Snippet",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationDiagnostics = compilation.GetDiagnostics();
        var errors = compilationDiagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

        if (!errors.IsEmpty)
        {
            throw new InvalidOperationException(
                $"The test snippet does not compile: {string.Join(Environment.NewLine, errors)}");
        }

        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> BuildReferences(bool includeDomain, bool includeApplication, bool includeStores)
    {
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var platformReferences = trustedPlatformAssemblies
            .Where(static path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return fileName is "System.Private.CoreLib" or "System.Runtime" or "System.Collections" or "netstandard";
            })
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path));

        if (!includeDomain)
        {
            return [.. platformReferences];
        }

        var domainReference = MetadataReference.CreateFromFile(typeof(GaWeCodes.Thessera.Domain.IClock).Assembly.Location);

        if (!includeApplication)
        {
            return [.. platformReferences, domainReference];
        }

        var applicationReference = MetadataReference.CreateFromFile(
            typeof(GaWeCodes.Thessera.Application.Cqrs.ICommandHandler<>).Assembly.Location);

        if (!includeStores)
        {
            return [.. platformReferences, domainReference, applicationReference];
        }

        var efCoreReference = MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location);
        var martenReference = MetadataReference.CreateFromFile(typeof(Marten.IDocumentSession).Assembly.Location);

        var storeDependencyClosure = ResolveDependencyClosure(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly)
            .Concat(ResolveDependencyClosure(typeof(Marten.IDocumentSession).Assembly))
            .DistinctBy(static reference => reference.Display, StringComparer.OrdinalIgnoreCase);

        return
        [
            .. platformReferences,
            domainReference,
            applicationReference,
            efCoreReference,
            martenReference,
            .. storeDependencyClosure,
        ];
    }

    private static IEnumerable<MetadataReference> ResolveDependencyClosure(Assembly root)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var assembly = queue.Dequeue();

            if (!visited.Add(assembly.FullName ?? assembly.GetName().Name ?? string.Empty))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(assembly.Location))
            {
                yield return MetadataReference.CreateFromFile(assembly.Location);
            }

            foreach (var referenced in assembly.GetReferencedAssemblies())
            {
                Assembly loaded;

                try
                {
                    loaded = Assembly.Load(referenced);
                }
                catch (Exception exception) when (exception is IOException or BadImageFormatException or FileLoadException)
                {
                    continue;
                }

                queue.Enqueue(loaded);
            }
        }
    }
}
