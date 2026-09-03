using System.Xml.Linq;

namespace GaWeCodes.Thessera.Tests;

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
