using System.Xml.Linq;

namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// Keeps every package's nuget.org page from going out blank.
/// </summary>
/// <remarks>
/// A package without a README and without a description builds, packs, restores and runs exactly
/// like one that has both -- the only thing that breaks is the first minute of the first stranger
/// who finds it, and no build reports that. The same is true one level up: the README can be
/// present and still never reach the feed, because what puts it there is a packed item and a
/// <c>PackageReadmeFile</c> in the shared props, not the file lying next to the project. Both
/// halves are asserted here, because either one alone is a page nobody reads.
/// </remarks>
public sealed class PackageReadmeTests
{
    private const string ReadmeFileName = "README.md";

    [Fact]
    public void EverySourceProject_HasAReadmeNextToIt()
    {
        var offenders = SourceProjects()
            .Where(path => !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, ReadmeFileName)))
            .Select(Relative)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                $"Every project under 'src' ships as a package and needs a '{ReadmeFileName}' beside its "
                + "project file; nuget.org renders it as the package page.",
                offenders));
    }

    [Fact]
    public void EveryReadme_OpensWithItsOwnPackageName()
    {
        var offenders = SourceProjects()
            .Select(path => (Project: path, Readme: Path.Combine(Path.GetDirectoryName(path)!, ReadmeFileName)))
            .Where(pair => File.Exists(pair.Readme))
            .Where(pair => FirstLine(pair.Readme) != $"# {Path.GetFileNameWithoutExtension(pair.Project)}")
            .Select(pair => Relative(pair.Readme))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                "A package README opens with '# <PackageId>'. A README copied from a sibling package renders "
                + "on the wrong page under the wrong name and reads as correct.",
                offenders));
    }

    [Fact]
    public void EverySourceProject_DeclaresADescription()
    {
        var offenders = SourceProjects()
            .Where(path => string.IsNullOrWhiteSpace(DescriptionOf(path)))
            .Select(Relative)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                "Every project under 'src' needs a non-empty '<Description>'. It stands next to the package "
                + "name in nuget.org search results, which is where a reader decides whether to open the page "
                + "at all -- and without one the SDK substitutes the package id, which says nothing.",
                offenders));
    }

    [Fact]
    public void TheSharedBuildProps_PacksTheReadme()
    {
        var props = XDocument.Load(Path.Combine(ThesseraLayout.Root, "src", "Directory.Build.props"));

        Assert.Equal(
            ReadmeFileName,
            props.Descendants("PackageReadmeFile").SingleOrDefault()?.Value);

        Assert.Contains(
            props.Descendants("None"),
            item => item.Attribute("Include")?.Value == ReadmeFileName
                && item.Attribute("Pack")?.Value == "true"
                && item.Attribute("PackagePath")?.Value == "\\");
    }

    private static string[] SourceProjects()
    {
        var projects = ThesseraLayout.ProjectFiles(Path.Combine(ThesseraLayout.Root, "src"));

        // Without this, a layout change that finds no project turns all three tests above into
        // assertions over an empty set: green forever, guarding nothing.
        Assert.NotEmpty(projects);

        return projects;
    }

    private static string? DescriptionOf(string projectPath) =>
        XDocument.Load(projectPath).Descendants("Description").SingleOrDefault()?.Value;

    private static string FirstLine(string path) =>
        File.ReadLines(path).FirstOrDefault()?.TrimEnd() ?? string.Empty;

    private static string Relative(string path) => ThesseraLayout.Relative(ThesseraLayout.Root, path);

    private static string Describe(string rule, string[] offenders) =>
        $"{rule}{Environment.NewLine}Offending: {string.Join(", ", offenders)}";
}
