using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// Compiles a small C# snippet in memory and runs a single <see cref="DiagnosticAnalyzer"/> over it,
/// so a test can assert on exactly the diagnostics that analyzer reports - without a dependency on
/// <c>Microsoft.CodeAnalysis.Testing</c>, which the repository does not otherwise need.
/// </summary>
internal static class AnalyzerTestHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithDomain = new(() => BuildReferences(includeDomain: true));
    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithoutDomain = new(() => BuildReferences(includeDomain: false));

    /// <summary>
    /// Compiles <paramref name="source"/> and returns every diagnostic <paramref name="analyzer"/>
    /// reports for it.
    /// </summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="source">The C# snippet to compile.</param>
    /// <param name="referenceDomain">
    /// Whether the compilation references <c>GaWeCodes.Thessera.Domain</c>. Defaults to
    /// <see langword="true"/>; pass <see langword="false"/> to exercise a rule's no-op path for a
    /// project that never references it at all.
    /// </param>
    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        bool referenceDomain = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GaWeCodes.Thessera.Analyzers.Tests.Snippet",
            syntaxTrees: [syntaxTree],
            references: referenceDomain ? ReferencesWithDomain.Value : ReferencesWithoutDomain.Value,
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

    private static ImmutableArray<MetadataReference> BuildReferences(bool includeDomain)
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

        return [.. platformReferences, domainReference];
    }
}
