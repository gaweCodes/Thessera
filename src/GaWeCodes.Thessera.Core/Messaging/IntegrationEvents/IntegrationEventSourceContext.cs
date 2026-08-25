namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

public sealed class IntegrationEventSourceContext(string name)
{
    public const string HeaderName = "thessera.source-context";

    public string Name { get; } = name;
}
