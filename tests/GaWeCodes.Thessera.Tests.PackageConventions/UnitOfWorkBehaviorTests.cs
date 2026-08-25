using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GaWeCodes.Thessera.Tests;

public sealed class UnitOfWorkBehaviorTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test";

    [Fact]
    public async Task SuccessfulCommand_CommitsExactlyOnce()
    {
        var unitOfWork = new RecordingUnitOfWork();
        using var provider = BuildProvider(unitOfWork, new PassingCommandHandler());
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task FailedCommand_DoesNotCommit()
    {
        var unitOfWork = new RecordingUnitOfWork();
        using var provider = BuildProvider(unitOfWork, new FailingCommandHandler());
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task Query_DoesNotCommit()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => unitOfWork);
        services.AddScoped<IQueryHandler<ProbeQuery, int>, ProbeQueryHandler>();
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task EfCoreConcurrencyConflictOnCommit_IsMappedToConflictFailure()
    {
        var unitOfWork = new ThrowingUnitOfWork(new DbUpdateConcurrencyException("row changed"));
        using var provider = BuildProvider(
            unitOfWork,
            new PassingCommandHandler(),
            options => options.UseEfCoreStateStore<TestDbContext>(ConnectionString));
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.Conflict, failure.Category);
        Assert.Equal(PersistenceFailureCodes.ConcurrencyConflict, failure.Code);
    }

    [Fact]
    public async Task EfCoreUniqueViolationOnCommit_IsMappedToConflictFailure()
    {
        var unitOfWork = new ThrowingUnitOfWork(
            new DbUpdateException("save failed", UniqueViolation("ux_widgets_name")));
        using var provider = BuildProvider(
            unitOfWork,
            new PassingCommandHandler(),
            options => options.UseEfCoreStateStore<TestDbContext>(ConnectionString));
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.Conflict, failure.Category);
        Assert.Equal(PersistenceFailureCodes.UniqueViolation, failure.Code);
        Assert.Contains("ux_widgets_name", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MartenUniqueViolationOnCommit_IsMappedToConflictFailure()
    {
        var unitOfWork = new ThrowingUnitOfWork(UniqueViolation("ux_gadgets_name"));
        using var provider = BuildProvider(
            unitOfWork,
            new PassingCommandHandler(),
            options => options.UseMartenEventStore(ConnectionString));
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.Conflict, failure.Category);
        Assert.Equal(PersistenceFailureCodes.UniqueViolation, failure.Code);
        Assert.Contains("ux_gadgets_name", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UniqueViolationWithoutConstraintName_StillProducesANonEmptyMessage()
    {
        var unitOfWork = new ThrowingUnitOfWork(
            new DbUpdateException("save failed", UniqueViolation(constraintName: null)));
        using var provider = BuildProvider(
            unitOfWork,
            new PassingCommandHandler(),
            options => options.UseEfCoreStateStore<TestDbContext>(ConnectionString));
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(PersistenceFailureCodes.UniqueViolation, failure.Code);
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
    }

    [Fact]
    public async Task ForeignKeyViolationOnCommit_IsNotSwallowed()
    {
        var violation = new PostgresException(
            "insert or update violates foreign key constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.ForeignKeyViolation);
        var unitOfWork = new ThrowingUnitOfWork(new DbUpdateException("save failed", violation));
        using var provider = BuildProvider(
            unitOfWork,
            new PassingCommandHandler(),
            options => options.UseEfCoreStateStore<TestDbContext>(ConnectionString));
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<DbUpdateException>(
            () => sender.SendAsync(new ProbeCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitFault_WithoutAnyTranslator_IsNotSwallowed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new ThrowingUnitOfWork(new DbUpdateConcurrencyException("row changed")));
        services.AddScoped<ICommandHandler<ProbeCommand>>(_ => new PassingCommandHandler());
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => sender.SendAsync(new ProbeCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task SuccessfulCommand_WithoutRegisteredUnitOfWork_PassesThrough()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICommandHandler<ProbeCommand>, PassingCommandHandler>();
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private static PostgresException UniqueViolation(string? constraintName) =>
        new(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName);

    private static ServiceProvider BuildProvider(
        IUnitOfWork unitOfWork,
        ICommandHandler<ProbeCommand> handler,
        Action<ThesseraOptions>? selectPersistence = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => unitOfWork);
        services.AddScoped<ICommandHandler<ProbeCommand>>(_ => handler);
        services.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            selectPersistence?.Invoke(options);
        });
        return services.BuildServiceProvider();
    }

    private sealed record ProbeCommand : ICommand;

    private sealed record ProbeQuery : IQuery<int>;

    private sealed class PassingCommandHandler : ICommandHandler<ProbeCommand>
    {
        public Task<Result> HandleAsync(ProbeCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class FailingCommandHandler : ICommandHandler<ProbeCommand>
    {
        public Task<Result> HandleAsync(ProbeCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failed(Failure.NotFound("probe.not_found", "Nothing here.")));
    }

    private sealed class ProbeQueryHandler : IQueryHandler<ProbeQuery, int>
    {
        public Task<Result<int>> HandleAsync(ProbeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(42));
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingUnitOfWork(Exception exception) : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => throw exception;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
