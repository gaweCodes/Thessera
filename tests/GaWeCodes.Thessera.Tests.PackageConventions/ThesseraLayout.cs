namespace GaWeCodes.Thessera.Tests;

internal static class ThesseraLayout
{
    private static readonly string BinSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
    private static readonly string ObjSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";

    private static readonly Lazy<string> LazyRoot = new(Locate);

    internal static string Root => LazyRoot.Value;

    internal static string[] ProjectFiles(string directory) => Files(directory, "*.csproj");

    internal static string[] SourceFiles(string directory) => Files(directory, "*.cs");

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
