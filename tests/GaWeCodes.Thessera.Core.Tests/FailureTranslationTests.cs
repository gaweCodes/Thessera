using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class FailureTranslationTests
{
    [Fact]
    public async Task DomainValidationException_IsTranslatedToValidationFailure()
    {
        var result = await SendThrowing(new DomainValidationException("Name must not be empty."));

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.Validation, failure.Category);
        Assert.Equal(DomainValidationException.FallbackCode, failure.Code);
    }

    [Fact]
    public async Task BusinessRuleViolationException_IsTranslatedToBusinessRuleFailure()
    {
        var result = await SendThrowing(new BusinessRuleViolationException("Recipe already published."));

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.BusinessRule, failure.Category);
        Assert.Equal(BusinessRuleViolationException.FallbackCode, failure.Code);
    }

    [Fact]
    public async Task DomainValidationException_FromAQuery_IsTranslatedToATypedFailureResult()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddScoped<IQueryHandler<ThrowingQuery, int>>(
            _ => new ThrowingQueryHandler(new DomainValidationException("Page must be positive.")));
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ThrowingQuery(), CancellationToken.None);

        Assert.IsType<Result<int>>(result);
        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.Validation, failure.Category);
    }

    [Fact]
    public async Task DomainValidationException_WithSeveralViolations_BecomesOneFailurePerViolation()
    {
        var result = await SendThrowing(new DomainValidationException(
        [
            new RuleViolation("widget.name.required", "name", "The name must not be empty."),
            new RuleViolation("widget.quantity.positive", "quantity", "The quantity must be positive."),
        ]));

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Equal(FailureCategory.Validation, failure.Category));
        Assert.Equal("widget.name.required", result.Failures[0].Code);
        Assert.Equal("name", result.Failures[0].Target);
        Assert.Equal("quantity", result.Failures[1].Target);
        Assert.Equal("The quantity must be positive.", result.Failures[1].Message);
    }

    [Fact]
    public async Task BusinessRuleViolations_CarryTheRulesOwnCodeAndNoTarget()
    {
        var result = await SendThrowing(new BusinessRuleViolationException(
        [
            new RuleViolation("recipe.already_published", null, "Already published."),
            new RuleViolation("recipe.retired", null, "Already retired."),
        ]));

        Assert.Equal(2, result.Failures.Count);
        Assert.All(
            result.Failures,
            failure =>
            {
                Assert.Equal(FailureCategory.BusinessRule, failure.Category);
                Assert.Null(failure.Target);
            });
        Assert.Equal("recipe.already_published", result.Failures[0].Code);
        Assert.Equal("recipe.retired", result.Failures[1].Code);
    }

    [Fact]
    public async Task BusinessRuleViolation_FromAMessageOnlyException_UsesTheFallbackCode()
    {
        var result = await SendThrowing(new BusinessRuleViolationException("Recipe already published."));

        var failure = Assert.Single(result.Failures);
        Assert.Equal(BusinessRuleViolationException.FallbackCode, failure.Code);
    }

    [Fact]
    public async Task DomainValidationException_WithSeveralViolations_FromAQuery_KeepsTheTypedResult()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddScoped<IQueryHandler<ThrowingQuery, int>>(
            _ => new ThrowingQueryHandler(new DomainValidationException(
            [
                new RuleViolation("page.positive", "page", "Page must be positive."),
                new RuleViolation("size.positive", "size", "Size must be positive."),
            ])));
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ThrowingQuery(), CancellationToken.None);

        Assert.IsType<Result<int>>(result);
        Assert.Equal(2, result.Failures.Count);
    }

    private static async Task<Result> SendThrowing(Exception exception)    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddScoped<ICommandHandler<ThrowingCommand>>(_ => new ThrowingCommandHandler(exception));
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ThrowingCommand(), CancellationToken.None).ConfigureAwait(false);
        return result;
    }

    private sealed record ThrowingCommand : ICommand;

    private sealed record ThrowingQuery : IQuery<int>;

    private sealed class ThrowingQueryHandler(Exception exception) : IQueryHandler<ThrowingQuery, int>
    {
        public Task<Result<int>> HandleAsync(ThrowingQuery query, CancellationToken cancellationToken) => throw exception;
    }

    private sealed class ThrowingCommandHandler(Exception exception) : ICommandHandler<ThrowingCommand>
    {
        public Task<Result> HandleAsync(ThrowingCommand command, CancellationToken cancellationToken) => throw exception;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
