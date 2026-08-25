using System.Reflection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

public sealed record IntegrationEventSubscription(
    string EndpointName,
    IReadOnlyList<string> TopicPatterns,
    Assembly ConsumerAssembly);
