using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;

[EventName("upcast-probe-titled-v1")]
internal sealed record UpcastProbeTitled(string Title) : DomainEvent, IRetiredDomainEvent<UpcastProbeNamed>
{
    public UpcastProbeNamed Upcast() => new(Title);
}

[EventName("upcast-probe-titled-v2")]
internal sealed record UpcastProbeNamed(string Name) : DomainEvent, IRetiredDomainEvent<UpcastProbeRenamed>
{
    public UpcastProbeRenamed Upcast() => new(Name, Language: null);
}

[EventName("upcast-probe-titled-v3")]
internal sealed record UpcastProbeRenamed(string NewName, string? Language) : DomainEvent;
