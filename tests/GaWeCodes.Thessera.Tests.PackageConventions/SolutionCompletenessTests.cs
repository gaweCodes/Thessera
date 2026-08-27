using System.Xml.Linq;

namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// Keeps the repository solution files in step with the projects that actually exist.
/// </summary>
/// <remarks>
/// A project missing from the solution still builds, because whatever references it drags it in.
/// That is exactly why the gap survives: nothing goes red. It shows up later as a package that no
/// one opened in the IDE, that no solution-wide analysis covered, and that a solution-scoped
/// `dotnet build` or `dotnet pack` quietly skipped.
/// </remarks>
public sealed class SolutionCompletenessTests
{
    [Fact]
    public void EveryProjectUnderTheRepository_IsListedInOneOfTheSolutions()
    {
        var root = ThesseraLayout.Root;

        var onDisk = ThesseraLayout
            .ProjectFiles(root)
            .Select(path => ThesseraLayout.Relative(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var listed = ListedProjects(root).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal<IEnumerable<string>>(onDisk, listed);
    }

    [Fact]
    public void NoSolutionNamesAProjectThatIsGone()
    {
        var root = ThesseraLayout.Root;

        var missing = ListedProjects(root)
            .Where(project => !File.Exists(Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        Assert.Empty(missing);
    }

    private static string[] ListedProjects(string root) =>
    [
        .. Directory.EnumerateFiles(root, "*.slnx", SearchOption.TopDirectoryOnly)
            .SelectMany(path => XDocument.Load(path)
                .Descendants("Project")
                .Select(project => project.Attribute("Path")!.Value)),
    ];
}
