namespace GaWeCodes.Thessera.Tests;

public sealed class ContainerRequirementTests
{
    [Fact]
    public void IsRequiredWhenTheVariableIsSet()
    {
        Environment.SetEnvironmentVariable(ContainerRequirement.EnvironmentVariable, "1");

        try
        {
            Assert.True(ContainerRequirement.ContainersRequired);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContainerRequirement.EnvironmentVariable, null);
        }
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
        Environment.SetEnvironmentVariable(ContainerRequirement.EnvironmentVariable, value);

        try
        {
            Assert.False(ContainerRequirement.ContainersRequired);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContainerRequirement.EnvironmentVariable, null);
        }
    }
}
