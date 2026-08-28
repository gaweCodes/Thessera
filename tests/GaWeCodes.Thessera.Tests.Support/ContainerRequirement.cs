namespace GaWeCodes.Thessera.Tests;

public static class ContainerRequirement
{
    public const string EnvironmentVariable = "THESSERA_REQUIRE_CONTAINERS";

    public static bool ContainersRequired => IsEnabled(Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static void ThrowIfRequired(string containerName, Exception failure)
    {
        if (ContainersRequired)
        {
            throw new InvalidOperationException(
                $"The {containerName} Testcontainer could not be started and {EnvironmentVariable} is set, " +
                "so this run must not silently skip the tests that depend on it.",
                failure);
        }
    }

    private static bool IsEnabled(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value, "0", StringComparison.Ordinal)
        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
}
