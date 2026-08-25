namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// The one place that knows where the family lives on disk and what counts as one of its files.
/// </summary>
/// <remarks>
/// Every convention test in this project answers a question about the repository layout, so each of
/// them needs the same two facts: where the root is, and which files under it are real rather than
/// build output. Stated once, both move together. Stated per test -- which is how this started, in
/// six copies -- the next change to either finds some of them and leaves the rest answering
/// differently, and because these are all guards, the symptom is one guard passing while its
/// neighbour fails on the same tree.
/// </remarks>
internal static class ThesseraLayout
{
    private static readonly string BinSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
    private static readonly string ObjSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";

    private static readonly Lazy<string> LazyRoot = new(Locate);

    /// <summary>The directory holding the solution, resolved once per test run.</summary>
    internal static string Root => LazyRoot.Value;

    /// <summary>Every real project file under <paramref name="directory"/>, build output excluded.</summary>
    internal static string[] ProjectFiles(string directory) => Files(directory, "*.csproj");

    /// <summary>Every real C# file under <paramref name="directory"/>, build output excluded.</summary>
    internal static string[] SourceFiles(string directory) => Files(directory, "*.cs");

    /// <summary>The path of <paramref name="path"/> below <paramref name="root"/>, with forward slashes.</summary>
    internal static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string[] Files(string directory, string pattern) =>
    [
        .. Directory
            .EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains(ObjSegment, StringComparison.Ordinal)
                && !path.Contains(BinSegment, StringComparison.Ordinal)),
    ];

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(
            directory is not null,
            "No directory containing a '*.slnx' file was found above "
            + $"'{AppContext.BaseDirectory}'; the Thessera root cannot be located.");

        return directory!.FullName;
    }
}
