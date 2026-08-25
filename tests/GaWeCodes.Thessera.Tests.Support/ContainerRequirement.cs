namespace GaWeCodes.Thessera.Tests;

public static class ContainerRequirement
{
    public const string EnvironmentVariable = "THESSERA_REQUIRE_CONTAINERS";

    public const string LegacyEnvironmentVariable = "VITALSYNC_REQUIRE_CONTAINERS";

    private static readonly string[] RecognizedVariables = [EnvironmentVariable, LegacyEnvironmentVariable];

    public static bool ContainersRequired => RequiringVariable() is not null;

    public static void ThrowIfRequired(string containerName, Exception failure)
    {
        var variable = RequiringVariable();

        if (variable is not null)
        {
            throw new InvalidOperationException(
                $"The {containerName} Testcontainer could not be started and {variable} is set, " +
                "so this run must not silently skip the tests that depend on it.",
                failure);
        }
    }

    private static string? RequiringVariable() => RequiringVariable(Environment.GetEnvironmentVariable);

    public static string? RequiringVariable(Func<string, string?> read) =>
        Array.Find(RecognizedVariables, name => IsEnabled(read(name)));

    private static bool IsEnabled(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value, "0", StringComparison.Ordinal)
        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
}
