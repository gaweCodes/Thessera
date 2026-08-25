using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ConcurrencyConflictScenarioTests(PostgreSqlFixture fixture)
{
    private const int InsideTheUnitOfWork = ThesseraOptions.UnitOfWorkBehaviorOrder + 100;

    [Fact]
    public async Task EfCoreCommand_LosingTheRaceForAnAggregate_ReachesTheCallerAsAConflictFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var competingWrite = new CompetingWrite();
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
                .UseEfCoreStateStore<FlushProbeContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .AddPipelineBehavior(typeof(CompetingWriteBehavior<,>), InsideTheUnitOfWork)
                .CustomizeWolverine(ConfigureSoloDurability));

        builder.Services.AddSingleton(competingWrite);
        builder.Services.AddScoped<ICommandHandler<StartFlushProbe>, StartFlushProbeHandler>();
        builder.Services.AddScoped<ICommandHandler<RenameFlushProbe>, RenameFlushProbeHandler>();

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FlushProbeContext>().Database.ExecuteSqlRawAsync(
                "create table if not exists flush_probe_rows (id uuid primary key, name text not null, version bigint not null)",
                TestContext.Current.CancellationToken);
        }

        var id = new FlushProbeId(Guid.NewGuid());
        Assert.True((await SendAsync(host, new StartFlushProbe(id.Value))).IsSuccess);

        competingWrite.Arm(async (services, cancellationToken) =>
        {
            var repository = services.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
            var probe = await repository.GetByIdAsync(id, cancellationToken);
            probe!.Rename("winner");
            await services.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
        });

        var result = await SendAsync(host, new RenameFlushProbe(id.Value, "loser"));

        AssertConcurrencyConflict(result);

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
            var surviving = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

            Assert.Equal("winner", surviving!.Name);
            Assert.Equal(2, ((IStateOwner)surviving).Version);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MartenCommand_LosingTheRaceForAStream_ReachesTheCallerAsAConflictFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var competingWrite = new CompetingWrite();
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(TallyOpened).Assembly)
                .UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .AddPipelineBehavior(typeof(CompetingWriteBehavior<,>), InsideTheUnitOfWork)
                .CustomizeWolverine(ConfigureSoloDurability));

        builder.Services.AddSingleton(competingWrite);
        builder.Services.AddScoped<ICommandHandler<OpenTally>, OpenTallyHandler>();
        builder.Services.AddScoped<ICommandHandler<BumpTally>, BumpTallyHandler>();

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var id = new TallyId(Guid.NewGuid());
        Assert.True((await SendAsync(host, new OpenTally(id.Value))).IsSuccess);

        competingWrite.Arm(async (services, cancellationToken) =>
        {
            var repository = services.GetRequiredService<IRepository<Tally, TallyId>>();
            var tally = await repository.GetByIdAsync(id, cancellationToken);
            tally!.Bump(10);
            await services.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
        });

        var result = await SendAsync(host, new BumpTally(id.Value, 1));

        AssertConcurrencyConflict(result);

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<Tally, TallyId>>();
            var surviving = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

            Assert.Equal(10, surviving!.Count);
            Assert.Equal(2, ((IStateOwner)surviving).Version);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertConcurrencyConflict(Result result)
    {
        Assert.True(result.IsFailure);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCategory.Conflict, failure.Category);
        Assert.Equal(PersistenceFailureCodes.ConcurrencyConflict, failure.Code);
    }

    private static async Task<Result> SendAsync(IHost host, ICommand command)
    {
        using var scope = host.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.SendAsync(command, TestContext.Current.CancellationToken);
    }

    private static void ConfigureSoloDurability(WolverineOptions options)
    {
        options.Durability.Mode = DurabilityMode.Solo;
        options.Durability.ScheduledJobFirstExecution = TimeSpan.FromHours(1);
        options.Durability.ScheduledJobPollingTime = TimeSpan.FromHours(1);
        options.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
    }

    internal sealed class CompetingWrite
    {
        private Func<IServiceProvider, CancellationToken, Task>? _action;

        public void Arm(Func<IServiceProvider, CancellationToken, Task> action) => _action = action;

        public async Task RunOnceAsync(IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
        {
            var action = Interlocked.Exchange(ref _action, null);
            if (action is null)
            {
                return;
            }

            using var scope = scopeFactory.CreateScope();
            await action(scope.ServiceProvider, cancellationToken);
        }
    }

    internal sealed class CompetingWriteBehavior<TRequest, TResponse>(
        CompetingWrite competingWrite,
        IServiceScopeFactory scopeFactory) : IPipelineBehavior<TRequest, TResponse>
        where TResponse : Result
    {
        public async Task<TResponse> HandleAsync(
            TRequest request,
            RequestPipeline<TResponse> pipeline,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            var response = await pipeline.NextAsync(cancellationToken);
            await competingWrite.RunOnceAsync(scopeFactory, cancellationToken);
            return response;
        }
    }
}

public readonly record struct TallyId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("tally-opened-v1")]
public sealed record TallyOpened(TallyId TallyId) : DomainEvent;

[EventName("tally-bumped-v1")]
public sealed record TallyBumped(TallyId TallyId, int By) : DomainEvent;

public sealed record TallyState(TallyId Id, int Count) : AggregateState<TallyState, TallyId>
{
    public static TallyState Empty => new(default, 0);

    public override TallyState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        TallyOpened opened => this with { Id = opened.TallyId },
        TallyBumped bumped => this with { Count = Count + bumped.By },
        _ => this,
    };
}

[AggregateName("tally")]
public sealed class Tally : EventSourcedAggregateRoot<TallyId, TallyState>
{
    private Tally() : base(TallyState.Empty)
    {
    }

    public int Count => State.Count;

    public static Tally Open(TallyId id)
    {
        var tally = new Tally();
        tally.RaiseEvent(new TallyOpened(id));
        return tally;
    }

    public void Bump(int by) => RaiseEvent(new TallyBumped(Id, by));
}

public sealed record OpenTally(Guid Id) : ICommand;

public sealed class OpenTallyHandler(IRepository<Tally, TallyId> repository) : ICommandHandler<OpenTally>
{
    public async Task<Result> HandleAsync(OpenTally command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await repository.AddAsync(Tally.Open(new TallyId(command.Id)), cancellationToken);
        return Result.Success();
    }
}

public sealed record BumpTally(Guid Id, int By) : ICommand;

public sealed class BumpTallyHandler(IRepository<Tally, TallyId> repository) : ICommandHandler<BumpTally>
{
    public async Task<Result> HandleAsync(BumpTally command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tally = await repository.GetByIdAsync(new TallyId(command.Id), cancellationToken);
        if (tally is null)
        {
            return Failure.NotFound("tally.not_found", "No tally with that id exists.");
        }

        tally.Bump(command.By);
        return Result.Success();
    }
}
