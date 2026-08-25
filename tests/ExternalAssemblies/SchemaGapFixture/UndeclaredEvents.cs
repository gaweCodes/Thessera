using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Events;

namespace SchemaGapFixture;

public sealed record UnnamedEvent(string Name) : DomainEvent;

public sealed record UntopicedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
