namespace GaWeCodes.Thessera.Tests;

public sealed class ConsumerNeutralityTests
{
    private static readonly string[] OriginatingProjectTerms =
    [
        "VitalSync",
        "nutrition",
        "fitness",
        "analytics",
        "migration worker",
    ];

    private const string DecisionRecordPrefix = "ADR-";

    [Fact]
    public void NoPackageSourceNamesTheOriginatingProject()
    {
        var offenders = SourceFiles()
            .SelectMany(file => File
                .ReadLines(file.FullName)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(entry => NamesTheOriginatingProject(entry.Line))
                .Select(entry => $"{file.Relative}({entry.Number}): {entry.Line.Trim()}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "These packages are published to consumers who have never heard of the project they grew up in, so "
            + "their source must not name it — not in an exception message, not in an example value, and not in "
            + $"a role such as \"migration worker\" that only exists there.{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoPackageSourceCitesADecisionRecord()
    {
        var offenders = SourceFiles()
            .SelectMany(file => File
                .ReadLines(file.FullName)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(entry => entry.Line.Contains(DecisionRecordPrefix, StringComparison.Ordinal))
                .Select(entry => $"{file.Relative}({entry.Number}): {entry.Line.Trim()}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "A decision record number is meaningless to a consumer, who cannot look it up. State the reason in "
            + $"the message itself instead of pointing at a document only this repository has.{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void TheDetectorRecognisesAnOriginatingProjectTermWhereOneIsExpected()
    {
        Assert.True(NamesTheOriginatingProject("for example \"nutrition\""));
        Assert.False(NamesTheOriginatingProject("for example \"orders\""));
    }

    [Fact]
    public void ThereIsPackageSourceToInspect() => Assert.NotEmpty(SourceFiles());

    private static bool NamesTheOriginatingProject(string line) =>
        Array.Exists(
            OriginatingProjectTerms,
            term => line.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static (string FullName, string Relative)[] SourceFiles()
    {
        var source = Path.Combine(ThesseraLayout.Root, "src");

        Assert.True(
            Directory.Exists(source),
            $"'{source}' does not exist; the package source directory cannot be located.");

        return
        [
            .. ThesseraLayout
                .SourceFiles(source)
                .Select(file => (file, Path.GetRelativePath(source, file)))
                .OrderBy(file => file.Item2, StringComparer.Ordinal),
        ];
    }
}
