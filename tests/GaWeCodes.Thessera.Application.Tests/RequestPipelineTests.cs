using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Tests;

public sealed class RequestPipelineTests
{
    [Fact]
    public async Task NextAsync_InvokesTheContinuation()
    {
        var pipeline = new RequestPipeline<Result>(_ => Task.FromResult(Result.Success()), Result.Failed);

        var result = await pipeline.NextAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task NextAsync_PassesTheCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var observed = CancellationToken.None;
        var pipeline = new RequestPipeline<Result>(
            token =>
            {
                observed = token;
                return Task.FromResult(Result.Success());
            },
            Result.Failed);

        await pipeline.NextAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, observed);
    }

    [Fact]
    public async Task NextAsync_CalledTwice_ThrowsInsteadOfRunningTheHandlerAgain()
    {
        var calls = 0;
        var pipeline = new RequestPipeline<Result>(
            _ =>
            {
                calls++;
                return Task.FromResult(Result.Success());
            },
            Result.Failed);

        await pipeline.NextAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.NextAsync(CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Failed_ProducesTheResponseTypeSuppliedByTheDispatcher()
    {
        var pipeline = new RequestPipeline<Result<int>>(
            _ => Task.FromResult(Result<int>.Success(1)),
            Result<int>.Failed);

        var result = pipeline.Failed(Failure.NotFound("probe.missing", "Nothing here."));

        Assert.IsType<Result<int>>(result);
        Assert.True(result.IsFailure);
        Assert.Equal(FailureCategory.NotFound, Assert.Single(result.Failures).Category);
    }

    [Fact]
    public void Failed_WithoutAFailure_Throws()
    {
        var pipeline = new RequestPipeline<Result>(_ => Task.FromResult(Result.Success()), Result.Failed);

        Assert.Throws<ArgumentNullException>(() => pipeline.Failed((Failure)null!));
        Assert.Throws<ArgumentNullException>(() => pipeline.Failed((IReadOnlyList<Failure>)null!));
    }

    [Fact]
    public void Failed_WithSeveralFailures_CarriesThemAll()
    {
        var pipeline = new RequestPipeline<Result>(_ => Task.FromResult(Result.Success()), Result.Failed);

        var result = pipeline.Failed(
            [Failure.Validation("a", "first"), Failure.Validation("b", "second")]);

        Assert.Equal(2, result.Failures.Count);
        Assert.Equal("first", result.Failures[0].Message);
        Assert.Equal("second", result.Failures[1].Message);
    }

    [Fact]
    public void Construction_WithoutAContinuation_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new RequestPipeline<Result>(null!, Result.Failed));

    [Fact]
    public void Construction_WithoutAFailureFactory_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => new RequestPipeline<Result>(_ => Task.FromResult(Result.Success()), null!));
}
