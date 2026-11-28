namespace GaWeCodes.Thessera.Tests;

public static class ContainerRequirement
{
    public const string EnvironmentVariable = "THESSERA_REQUIRE_CONTAINERS";

    public static bool ContainersRequired => IsEnabled(Environment.GetEnvironmentVariable(EnvironmentVariable));

    private static bool IsEnabled(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value, "0", StringComparison.Ordinal)
        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
}
