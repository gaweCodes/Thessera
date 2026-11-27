using System.Text.Json.Serialization;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace SchemaFixture;

public readonly record struct SampleId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public readonly record struct SampleLineId(int Value) : IEntityKey<int>
{
    public bool IsEmpty => Value == 0;
}

public sealed record SampleLine(SampleLineId LineId, string Label, decimal Amount);

[EventName("sample-created-v1")]
public sealed record SampleCreated(SampleId SampleId, string Name) : DomainEvent;

[EventName("sample-detailed-v1")]
public sealed record SampleDetailed(
    SampleId SampleId,
    [property: JsonPropertyName("comment")] string Note,
    IReadOnlyCollection<SampleLine> Lines,
    SampleLine? Highlight,
    DateOnly? Due) : DomainEvent;

[EventName("sample-renamed-v1")]
public sealed record SampleRetitled(SampleId SampleId, string Title) : DomainEvent, IRetiredDomainEvent<SampleRenamed>
{
    public SampleRenamed Upcast() => new(SampleId, Title);
}

[EventName("sample-renamed-v2")]
public sealed record SampleRenamed(SampleId SampleId, string Name) : DomainEvent;

[IntegrationEventTopic("fixture.sample-created")]
public sealed record SampleCreatedIntegrationEvent(Guid SampleId, Guid EventId, DateTimeOffset OccurredAt)
    : IIntegrationEvent;
