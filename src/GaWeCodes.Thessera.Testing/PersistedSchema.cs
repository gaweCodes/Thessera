using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GaWeCodes.Thessera.Testing;

public static class PersistedSchema
{
    private const string ApprovedSuffix = ".approved.txt";
    private const string ReceivedSuffix = ".received.txt";

    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    [RequiresDynamicCode(TrimmingMessages.DynamicGenerics)]
    public static string Render(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return PersistedSchemaRenderer.Render(assemblies);
    }

    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    [RequiresDynamicCode(TrimmingMessages.DynamicGenerics)]
    public static void Verify(string approvedFilePath, IEnumerable<Assembly> assemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedFilePath);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (!approvedFilePath.EndsWith(ApprovedSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The snapshot baseline '{approvedFilePath}' must end in '{ApprovedSuffix}', so that the rendering " +
                $"of a failing run can be written next to it as '{ReceivedSuffix}'.",
                nameof(approvedFilePath));
        }

        var actual = Normalize(PersistedSchemaRenderer.Render(assemblies));
        var approved = File.Exists(approvedFilePath)
            ? Normalize(File.ReadAllText(approvedFilePath))
            : string.Empty;

        var receivedFilePath = approvedFilePath[..^ApprovedSuffix.Length] + ReceivedSuffix;

        if (string.Equals(actual, approved, StringComparison.Ordinal))
        {
            File.Delete(receivedFilePath);
            return;
        }

        File.WriteAllText(receivedFilePath, actual.ReplaceLineEndings());

        throw new InvalidOperationException(
            $"The persisted event schema no longer matches its approved snapshot '{approvedFilePath}'. Field names " +
            "are written into every event body, and a stored event whose field is no longer found deserializes to " +
            "the type's default without an error, a log entry or a failing test, so the change is caught here or " +
            "not at all. Compare the rendering in " +
            $"'{receivedFilePath}': a field that was only added stays readable, so approve the new snapshot; a field " +
            "that was renamed, removed or retyped does not, so leave the event untouched and introduce a successor " +
            "under a new [EventName] instead." + Environment.NewLine + Environment.NewLine +
            "Approved:" + Environment.NewLine + approved + Environment.NewLine +
            "Rendered:" + Environment.NewLine + actual);
    }

    private static string Normalize(string schema) =>
        schema.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
}
