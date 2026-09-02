using GaWeCodes.Thessera.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// Every rule resolves the Thessera types it reasons about by metadata name and does nothing when
/// they are not found - this is what keeps the package free of a compile-time dependency on
/// <c>GaWeCodes.Thessera.Domain</c>. This test exercises that no-op path directly, compiling a
/// project that never references <c>GaWeCodes.Thessera.Domain</c> at all.
/// </summary>
public sealed class NoDomainReferenceTests
{
    [Fact]
    public async Task WithoutADomainReference_NoRuleReportsAnything()
    {
        const string source = """
            namespace Snippet;

            public sealed class Sample
            {
                public Sample() { }
            }
            """;

        var analyzers = new DiagnosticAnalyzer[]
        {
            new AggregateNameAnalyzer(),
            new DomainEventNameAnalyzer(),
            new AggregateConstructorAnalyzer(),
            new ChildEntityConstructorAnalyzer(),
            new AggregateStateSelfBindingAnalyzer(),
            new ChildEntityStateSelfBindingAnalyzer(),
        };

        foreach (var analyzer in analyzers)
        {
            var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(analyzer, source, referenceDomain: false);
            Assert.Empty(diagnostics);
        }
    }
}
