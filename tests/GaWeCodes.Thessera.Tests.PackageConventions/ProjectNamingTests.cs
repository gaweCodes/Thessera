namespace GaWeCodes.Thessera.Tests;

/// <summary>
/// Pins the shape of every project name in the family.
/// </summary>
/// <remarks>
/// Three renames have passed over this repository and not one of them could have gone red: a
/// project name is a file name, so a wrong one still compiles, still runs and still ships. The rule
/// is short -- a source project is <c>GaWeCodes.Thessera.&lt;Package&gt;</c>; a test project either
/// mirrors exactly one package as <c>GaWeCodes.Thessera.&lt;Package&gt;.Tests</c> or says that it
/// mirrors none as <c>GaWeCodes.Thessera.Tests.&lt;Suite&gt;</c>; and the fixtures and matrix hosts
/// carry no prefix at all, because they stand in for a stranger's code. That last form is the one
/// worth guarding hardest: prefixing a matrix host would quietly turn the proof that a package can
/// be consumed from outside into a proof that it can be consumed from inside.
/// </remarks>
public sealed class ProjectNamingTests
{
    private const string Prefix = "GaWeCodes.Thessera.";
    private const string SuiteProjectPrefix = "GaWeCodes.Thessera.Tests.";
    private const string MirrorSuffix = ".Tests";

    private static readonly string[] ForeignConsumerDirectories = ["ExternalAssemblies/", "MatrixHosts/"];

    [Fact]
    public void EverySourceProject_IsTheFamilyPrefixPlusAPackageName()
    {
        var projects = PackageNames();

        Assert.NotEmpty(projects);

        var offenders = projects
            .Where(name => !name.StartsWith(Prefix, StringComparison.Ordinal) || name.Length == Prefix.Length)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe($"Every project under 'src' must be named '{Prefix}<Package>'.", offenders));
    }

    [Fact]
    public void EveryTestProject_EitherMirrorsOnePackageOrSaysThatItMirrorsNone()
    {
        var packages = PackageNames();
        var projects = TestProjectNames(foreignConsumers: false);

        Assert.NotEmpty(packages);
        Assert.NotEmpty(projects);

        var offenders = projects.Where(name => !IsPackageMirror(name, packages) && !IsSuite(name)).ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                $"A test project is either '{Prefix}<Package>{MirrorSuffix}' naming an existing package "
                + $"under 'src', or '{SuiteProjectPrefix}<Suite>' when it mirrors no single package. A "
                + $"project that stands in for a stranger's code belongs under one of "
                + $"{string.Join(" or ", ForeignConsumerDirectories)} instead, where no prefix is expected.",
                offenders));
    }

    [Fact]
    public void TheFixturesAndMatrixHosts_CarryNoFamilyPrefix()
    {
        var projects = TestProjectNames(foreignConsumers: true);

        Assert.NotEmpty(projects);

        var offenders = projects.Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                "Fixtures and matrix hosts stand in for a stranger's code and must not carry the "
                + "family prefix; prefixing one turns an outside-in proof into an inside-in one.",
                offenders));
    }

    [Fact]
    public void EveryProjectFile_IsNamedAfterItsDirectory()
    {
        var projects = ThesseraLayout.ProjectFiles(ThesseraLayout.Root);

        Assert.NotEmpty(projects);

        var offenders = projects
            .Where(path => Path.GetFileNameWithoutExtension(path)
                != Path.GetFileName(Path.GetDirectoryName(path)!))
            .Select(path => ThesseraLayout.Relative(ThesseraLayout.Root, path))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            Describe(
                "A project file carries its directory's name. A directory renamed without its "
                + "'.csproj' builds exactly as before and is invisible until someone reads the path.",
                offenders));
    }

    private static bool IsPackageMirror(string name, string[] packages) =>
        name.EndsWith(MirrorSuffix, StringComparison.Ordinal)
        && packages.Contains(name[..^MirrorSuffix.Length], StringComparer.Ordinal);

    private static bool IsSuite(string name) =>
        name.StartsWith(SuiteProjectPrefix, StringComparison.Ordinal) && name.Length > SuiteProjectPrefix.Length;

    private static string[] PackageNames() => ProjectNames(Path.Combine(ThesseraLayout.Root, "src"));

    private static string[] TestProjectNames(bool foreignConsumers)
    {
        var tests = Path.Combine(ThesseraLayout.Root, "tests");

        return ProjectNames(tests, path => StandsInForAStranger(tests, path) == foreignConsumers);
    }

    private static bool StandsInForAStranger(string tests, string path)
    {
        var relative = ThesseraLayout.Relative(tests, path);

        return ForeignConsumerDirectories.Any(directory => relative.StartsWith(directory, StringComparison.Ordinal));
    }

    private static string[] ProjectNames(string directory, Func<string, bool>? include = null) =>
    [
        .. ThesseraLayout.ProjectFiles(directory)
            .Where(path => include is null || include(path))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal),
    ];

    private static string Describe(string rule, string[] offenders) =>
        $"{rule}{Environment.NewLine}Offending: {string.Join(", ", offenders)}";
}
