using System.Xml.Linq;

namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// Keeps a breaking API change visible in the pull request diff instead of only at the consumer.
/// </summary>
/// <remarks>
/// <see cref="PublicSurfaceTests"/> pins the exported <b>type</b> list; it says nothing about a
/// parameter added, a parameter renamed, a return type widened, or an enum member inserted in the
/// middle. Those are exactly the changes <c>Microsoft.CodeAnalysis.PublicApiAnalyzers</c> catches,
/// but only for a project that actually carries both tracking files and actually references the
/// analyzer -- a project missing either one builds, packs and ships exactly like one that has both,
/// and no build reports that. Both halves are asserted here, because either one alone lets a
/// project's public surface drift unnoticed.
/// </remarks>
public sealed class PublicApiFilesTests
{
    private const string ShippedFileName = "PublicAPI.Shipped.txt";
    private const string UnshippedFileName = "PublicAPI.Unshipped.txt";
    private const string AnalyzerPackageId = "Microsoft.CodeAnalysis.PublicApiAnalyzers";

    [Fact]
    public void EverySourceProject_HasAShippedAndAnUnshippedFile()
    {
        var offenders = SourceProjects()
            .Where(path => !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, ShippedFileName))
                || !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, UnshippedFileName)))
            .Select(Relative)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                $"Every project under 'src' ships as a package and needs both '{ShippedFileName}' and "
                + $"'{UnshippedFileName}' beside its project file; the analyzer treats a missing file as an "
                + "empty declared surface and flags every public member.",
                offenders));
    }

    [Fact]
    public void EveryTrackingFile_DeclaresTheNullableContext()
    {
        var offenders = SourceProjects()
            .SelectMany(path => new[]
            {
                Path.Combine(Path.GetDirectoryName(path)!, ShippedFileName),
                Path.Combine(Path.GetDirectoryName(path)!, UnshippedFileName),
            })
            .Where(File.Exists)
            .Where(path => FirstLine(path) != "#nullable enable")
            .Select(Relative)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                "Every tracking file's first line records the nullable context the API was declared in. Every "
                + "project under 'src' has nullable enabled, so every tracking file starts with '#nullable "
                + "enable'; without it, a '?' on a reference type is not part of what the file promises.",
                offenders));
    }

    [Fact]
    public void TheSharedBuildProps_ReferencesThePublicApiAnalyzer()
    {
        var props = XDocument.Load(Path.Combine(ThesseraLayout.Root, "src", "Directory.Build.props"));

        Assert.Contains(
            props.Descendants("PackageReference"),
            item => item.Attribute("Include")?.Value == AnalyzerPackageId
                && item.Attribute("PrivateAssets")?.Value is "All" or "all");
    }

    [Fact]
    public void NoUnshippedFile_StillCarriesAPendingEntry()
    {
        var offenders = SourceProjects()
            .Select(path => Path.Combine(Path.GetDirectoryName(path)!, UnshippedFileName))
            .Where(File.Exists)
            .Where(path => File.ReadLines(path).Skip(1).Any(line => !string.IsNullOrWhiteSpace(line)))
            .Select(Relative)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                "An entry left in 'PublicAPI.Unshipped.txt' after a release marks a surface that was declared "
                + "but never promoted to 'PublicAPI.Shipped.txt'; every alpha release promotes its additions "
                + "before the next one starts.",
                offenders));
    }

    private static string[] SourceProjects()
    {
        var projects = ThesseraLayout.ProjectFiles(Path.Combine(ThesseraLayout.Root, "src"));

        // Without this, a layout change that finds no project turns every test above into an
        // assertion over an empty set: green forever, guarding nothing.
        Assert.NotEmpty(projects);

        return projects;
    }

    private static string FirstLine(string path) =>
        File.ReadLines(path).FirstOrDefault()?.TrimEnd() ?? string.Empty;

    private static string Relative(string path) => ThesseraLayout.Relative(ThesseraLayout.Root, path);

    private static string Describe(string rule, string[] offenders) =>
        $"{rule}{Environment.NewLine}Offending: {string.Join(", ", offenders)}";
}
