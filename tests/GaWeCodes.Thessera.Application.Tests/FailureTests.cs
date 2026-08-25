using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Tests;

public sealed class FailureTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var failure = new Failure("recipe.name_required", "The recipe name is required.", FailureCategory.Validation);

        Assert.Equal("recipe.name_required", failure.Code);
        Assert.Equal("The recipe name is required.", failure.Message);
        Assert.Equal(FailureCategory.Validation, failure.Category);
    }

    [Fact]
    public void Constructor_WithoutATarget_LeavesItUnset()
    {
        var failure = new Failure("code", "message", FailureCategory.Validation);

        Assert.Null(failure.Target);
    }

    [Fact]
    public void Target_DistinguishesOtherwiseIdenticalFailures()
    {
        var name = new Failure("code", "message", FailureCategory.Validation) { Target = "name" };
        var quantity = new Failure("code", "message", FailureCategory.Validation) { Target = "quantity" };

        Assert.Equal("name", name.Target);
        Assert.NotEqual(name, quantity);
    }

    [Fact]
    public void Constructor_WithNullCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Failure(null!, "message", FailureCategory.Validation));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithWhiteSpaceCode_ThrowsArgumentException(string code)
    {
        Assert.Throws<ArgumentException>(() => new Failure(code, "message", FailureCategory.Validation));
    }

    [Fact]
    public void Constructor_WithNullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Failure("code", null!, FailureCategory.Validation));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithWhiteSpaceMessage_ThrowsArgumentException(string message)
    {
        Assert.Throws<ArgumentException>(() => new Failure("code", message, FailureCategory.Validation));
    }

    [Fact]
    public void Validation_CreatesFailureWithValidationCategory()
    {
        var failure = Failure.Validation("code", "message");

        Assert.Equal(FailureCategory.Validation, failure.Category);
    }

    [Fact]
    public void BusinessRule_CreatesFailureWithBusinessRuleCategory()
    {
        var failure = Failure.BusinessRule("code", "message");

        Assert.Equal(FailureCategory.BusinessRule, failure.Category);
    }

    [Fact]
    public void NotFound_CreatesFailureWithNotFoundCategory()
    {
        var failure = Failure.NotFound("code", "message");

        Assert.Equal(FailureCategory.NotFound, failure.Category);
    }

    [Fact]
    public void Conflict_CreatesFailureWithConflictCategory()
    {
        var failure = Failure.Conflict("code", "message");

        Assert.Equal(FailureCategory.Conflict, failure.Category);
    }

    [Fact]
    public void Forbidden_CreatesFailureWithForbiddenCategory()
    {
        var failure = Failure.Forbidden("code", "message");

        Assert.Equal(FailureCategory.Forbidden, failure.Category);
    }

    [Fact]
    public void EveryDeclaredCategory_HasAFactoryOfItsOwnName()
    {
        var missing = Enum.GetValues<FailureCategory>()
            .Where(static category => typeof(Failure).GetMethod(
                category.ToString(),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                [typeof(string), typeof(string)]) is null)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These failure categories have no factory on Failure: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void Equals_SameValues_AreEqual()
    {
        var a = new Failure("code", "message", FailureCategory.Conflict);
        var b = new Failure("code", "message", FailureCategory.Conflict);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Constructor_WithUndeclaredCategory_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Failure("code", "message", (FailureCategory)99));
    }
}
