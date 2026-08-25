using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Telemetry;

internal static class TelemetryTags
{
    public const string RequestName = "thessera.request.name";

    public const string RequestKind = "thessera.request.kind";

    public const string RequestKindCommand = "command";

    public const string RequestKindQuery = "query";

    public const string Outcome = "thessera.outcome";

    public const string OutcomeSuccess = "success";

    public const string OutcomeFailure = "failure";

    public const string OutcomeFaulted = "faulted";

    public const string FailureCategories = "thessera.failure.categories";

    public const string ExceptionType = "thessera.exception.type";

    public const string DomainEventName = "thessera.domain_event.name";

    public const string AggregateName = "thessera.aggregate.name";

    public const string AggregateId = "thessera.aggregate.id";

    public const string AggregateVersion = "thessera.aggregate.version";

    public const string ProjectionHandler = "thessera.projection.handler";

    public const string IntegrationEventsPublished = "thessera.integration_events.published";
}
