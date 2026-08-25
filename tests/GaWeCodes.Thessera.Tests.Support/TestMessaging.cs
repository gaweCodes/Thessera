namespace GaWeCodes.Thessera.Tests;

public static class TestMessaging
{
    public const string ExchangeName = "test-platform.integration-events";

    public const string ContextName = "probe";

    public const string UpstreamContextName = "upstream";

    public static string UniqueQueueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static string UniqueExchangeName(string prefix) => $"test-{prefix}-{Guid.NewGuid():N}.integration-events";
}
