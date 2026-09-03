using GaWeCodes.Thessera.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Tests;

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
            new CommandHandlerSingleStoreAnalyzer(),
        };

        foreach (var analyzer in analyzers)
        {
            var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(analyzer, source, referenceDomain: false);
            Assert.Empty(diagnostics);
        }
    }
}
