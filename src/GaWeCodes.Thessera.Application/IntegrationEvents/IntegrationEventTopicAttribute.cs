using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Application.IntegrationEvents;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventTopicAttribute : Attribute
{
    public IntegrationEventTopicAttribute(string topic)
    {
        Topic = Validate(topic);
    }

    public string Topic { get; }

    private static string Validate(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var segments = topic.Split('.');
        return segments.Length == 2 && Array.TrueForAll(segments, NameSegment.IsValid)
            ? topic
            : throw new ArgumentException(
                $"'{topic}' is not a valid integration event topic. A topic is the published routing key " +
                "in the form '<context>.<event>', both segments lower-case kebab-case " +
                "(for example 'orders.order-placed'), so that consumer bindings such as 'orders.*' " +
                "stay stable and independent of the CLR type the attribute happens to be written on.",
                nameof(topic));
    }
}
