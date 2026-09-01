using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GaWeCodes.Thessera.Testing;

/// <summary>
/// Pins the persisted shape of a domain model — stream key formats, event names, and the serialized
/// name and type of every property — against an approved snapshot.
/// </summary>
/// <remarks>
/// These names leave the process and are permanent: renaming a C# member is free, but changing what
/// it is written as orphans everything already stored. Nothing in the compiler notices, which is why
/// the shape is compared against a file a human approved.
/// </remarks>
public static class PersistedSchema
{
    private const string ApprovedSuffix = ".approved.txt";
    private const string ReceivedSuffix = ".received.txt";

    /// <summary>
    /// Renders the persisted shape as text, for driving the comparison yourself.
    /// </summary>
    /// <param name="assemblies">The assemblies holding the domain model.</param>
    /// <returns>
    /// One block per aggregate stream and per domain or integration event, with each property's
    /// serialized name and type.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    [RequiresDynamicCode(TrimmingMessages.DynamicGenerics)]
    public static string Render(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return PersistedSchemaRenderer.Render(assemblies);
    }

    /// <summary>
    /// Compares the current persisted shape against an approved snapshot and fails when they differ.
    /// </summary>
    /// <param name="approvedFilePath">
    /// The baseline file. Must end in <c>.approved.txt</c>, so that a failing run can write its
    /// rendering beside it as <c>.received.txt</c>.
    /// </param>
    /// <param name="assemblies">The assemblies holding the domain model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="approvedFilePath"/> is empty, blank, or does not end in <c>.approved.txt</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The rendering differs from the baseline — or there is no baseline yet, which counts as a
    /// difference so that the first run has to be approved deliberately.
    /// </exception>
    /// <remarks>
    /// On a mismatch the current rendering is written next to the baseline, so reviewing the change
    /// is a file comparison and accepting an intended one is a file rename. The received file is
    /// deleted again as soon as a run matches.
    /// </remarks>
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
