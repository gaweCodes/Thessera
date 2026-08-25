using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Testing;
using SchemaFixture;
using SchemaGapFixture;

namespace GaWeCodes.Thessera.Tests;

public sealed class PersistedSchemaTests
{
    private const string FixtureSchema = """
        domain-event sample-created-v1
          Name : string
          SampleId : guid

        domain-event sample-detailed-v1
          Due : dateonly?
          Highlight : object
            Amount : decimal
            Label : string
            LineId : int
          Lines : object[]
            Amount : decimal
            Label : string
            LineId : int
          SampleId : guid
          comment : string

        integration-event fixture.sample-created
          EventId : guid
          OccurredAt : datetimeoffset
          SampleId : guid

        """;

    [Fact]
    public void Render_PinsEveryPersistedEventOfAnAssembly()
    {
        var schema = PersistedSchema.Render([typeof(SampleCreated).Assembly]);

        Assert.Equal(FixtureSchema.ReplaceLineEndings("\n"), schema);
    }

    [Fact]
    public void Render_ReportsTheEffectiveJsonName_NotTheClrPropertyName()
    {
        var schema = PersistedSchema.Render([typeof(SampleDetailed).Assembly]);

        Assert.Contains("comment : string", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("Note", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ReportsATypedKeyAsTheBareValueItSerializesTo()
    {
        var schema = PersistedSchema.Render([typeof(SampleCreated).Assembly]);

        Assert.Contains("SampleId : guid", schema, StringComparison.Ordinal);
        Assert.Contains("LineId : int", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ThrowsForADomainEventWithoutAPersistedName()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => PersistedSchema.Render([typeof(UnnamedEvent).Assembly]));

        Assert.Contains("[EventName]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ThrowsForAnIntegrationEventWithoutATopic()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => PersistedSchema.Render([typeof(UntopicedIntegrationEventProbe).Assembly]));

        Assert.Contains("[IntegrationEventTopic]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_PassesWhenTheApprovedSnapshotMatches()
    {
        using var baseline = new Baseline();
        baseline.Approve(PersistedSchema.Render([typeof(SampleCreated).Assembly]));

        PersistedSchema.Verify(baseline.ApprovedFilePath, [typeof(SampleCreated).Assembly]);

        Assert.False(File.Exists(baseline.ReceivedFilePath));
    }

    [Fact]
    public void Verify_RemovesTheRenderingOfAnEarlierFailure()
    {
        using var baseline = new Baseline();
        baseline.Approve(PersistedSchema.Render([typeof(SampleCreated).Assembly]));
        File.WriteAllText(baseline.ReceivedFilePath, "stale");

        PersistedSchema.Verify(baseline.ApprovedFilePath, [typeof(SampleCreated).Assembly]);

        Assert.False(File.Exists(baseline.ReceivedFilePath));
    }

    [Fact]
    public void Verify_WritesTheRenderingAndThrowsWhenAFieldNameChanged()
    {
        using var baseline = new Baseline();
        baseline.Approve(FixtureSchema.Replace("comment : string", "Note : string", StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidOperationException>(
            () => PersistedSchema.Verify(baseline.ApprovedFilePath, [typeof(SampleCreated).Assembly]));

        Assert.Contains("successor", exception.Message, StringComparison.Ordinal);
        Assert.Contains("comment : string", File.ReadAllText(baseline.ReceivedFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ThrowsWhenTheBaselineIsMissing()
    {
        using var baseline = new Baseline();

        Assert.Throws<InvalidOperationException>(
            () => PersistedSchema.Verify(baseline.ApprovedFilePath, [typeof(SampleCreated).Assembly]));
    }

    [Fact]
    public void Verify_RejectsABaselineThatCannotCarryTheRenderingOfAFailure()
    {
        Assert.Throws<ArgumentException>(
            () => PersistedSchema.Verify("EventSchema.txt", [typeof(SampleCreated).Assembly]));
    }

    private sealed class Baseline : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public Baseline() => Directory.CreateDirectory(_directory);

        public string ApprovedFilePath => Path.Combine(_directory, "EventSchema.approved.txt");

        public string ReceivedFilePath => Path.Combine(_directory, "EventSchema.received.txt");

        public void Approve(string schema) => File.WriteAllText(ApprovedFilePath, schema);

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    private sealed record UntopicedIntegrationEventProbe(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
}
