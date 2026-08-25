using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Failure_WithDescription_CreatesFailedResult()
    {
        var failure = Failure.NotFound("recipe.not_found", "The recipe was not found.");

        var result = Result.Failed(failure);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(failure, Assert.Single(result.Failures));
    }

    [Fact]
    public void Failure_WithMultipleFailures_CarriesAllFailures()
    {
        var failures = new[]
        {
            Failure.Validation("recipe.name_required", "The recipe name is required."),
            Failure.Validation("recipe.name_too_long", "The recipe name is too long."),
        };

        var result = Result.Failed(failures);

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public void Failure_WithNullDescription_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failed((Failure)null!));
    }

    [Fact]
    public void Failure_WithEmptyFailures_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Result.Failed([]));
    }

    [Fact]
    public void ImplicitConversion_FromFailure_CreatesFailedResult()
    {
        Result result = Failure.Conflict("recipe.exists", "The recipe already exists.");

        Assert.True(result.IsFailure);
        Assert.Equal(FailureCategory.Conflict, Assert.Single(result.Failures).Category);
    }
}
