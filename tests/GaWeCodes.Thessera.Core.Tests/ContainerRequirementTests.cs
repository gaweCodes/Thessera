namespace GaWeCodes.Thessera.Tests;

public sealed class ContainerRequirementTests
{
    [Fact]
    public void RecognizesTheCurrentVariable()
    {
        var requiring = ContainerRequirement.RequiringVariable(
            name => name == ContainerRequirement.EnvironmentVariable ? "1" : null);

        Assert.Equal(ContainerRequirement.EnvironmentVariable, requiring);
    }

    [Fact]
    public void StillRecognizesTheLegacyVariable()
    {
        var requiring = ContainerRequirement.RequiringVariable(
            name => name == ContainerRequirement.LegacyEnvironmentVariable ? "1" : null);

        Assert.Equal(ContainerRequirement.LegacyEnvironmentVariable, requiring);
    }

    [Fact]
    public void PrefersTheCurrentVariableOverTheLegacyOne()
    {
        var requiring = ContainerRequirement.RequiringVariable(_ => "1");

        Assert.Equal(ContainerRequirement.EnvironmentVariable, requiring);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("FALSE")]
    public void TreatsDisablingValuesAsUnset(string? value)
    {
        var requiring = ContainerRequirement.RequiringVariable(_ => value);

        Assert.Null(requiring);
    }
}
