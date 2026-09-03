using System.Collections.Concurrent;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class NoPublishOnFailedCommitTests(PostgreSqlFixture fixture)
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NonDeliveryWindow = TimeSpan.FromSeconds(2);

    private const int InsideTheUnitOfWork = ThesseraOptions.UnitOfWorkBehaviorOrder + 100;

    [Fact]
    public async Task EfCoreCommand_LosingTheRaceForAnAggregate_NeverPublishesItsDomainEvent()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var competingWrite = new ConcurrencyConflictScenarioTests.CompetingWrite();
        var signal = new DeliverySignal<string>();
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
                .UseEfCoreStateStore<FlushProbeContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .AddPipelineBehavior(typeof(ConcurrencyConflictScenarioTests.CompetingWriteBehavior<,>), InsideTheUnitOfWork)
                .CustomizeWolverine(ConfigureSoloDurability));

        builder.Services.AddSingleton(competingWrite);
        builder.Services.AddSingleton(signal);
        builder.Services.AddScoped<ICommandHandler<StartFlushProbe>, StartFlushProbeHandler>();
        builder.Services.AddScoped<ICommandHandler<RenameFlushProbe>, RenameFlushProbeHandler>();
        builder.Services.AddScoped<IProjectionHandler<FlushProbeRenamed>, FlushProbeRenamedProjection>();

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
        Assert.True(result.IsFailure);

        Assert.True(
            await signal.WasDeliveredAsync("winner", DeliveryTimeout, TestContext.Current.CancellationToken),
            "The winner's rename should have been published — otherwise this test proves nothing about the loser.");

        Assert.False(
            await signal.WasDeliveredAsync("loser", NonDeliveryWindow, TestContext.Current.CancellationToken),
            "The loser's failed commit must never publish its domain event.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MartenCommand_LosingTheRaceForAStream_NeverPublishesItsDomainEvent()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var competingWrite = new ConcurrencyConflictScenarioTests.CompetingWrite();
        var signal = new DeliverySignal<int>();
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(TallyOpened).Assembly)
                .UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .AddPipelineBehavior(typeof(ConcurrencyConflictScenarioTests.CompetingWriteBehavior<,>), InsideTheUnitOfWork)
                .CustomizeWolverine(ConfigureSoloDurability));

        builder.Services.AddSingleton(competingWrite);
        builder.Services.AddSingleton(signal);
        builder.Services.AddScoped<ICommandHandler<OpenTally>, OpenTallyHandler>();
        builder.Services.AddScoped<ICommandHandler<BumpTally>, BumpTallyHandler>();
        builder.Services.AddScoped<IProjectionHandler<TallyBumped>, TallyBumpedProjection>();

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
        Assert.True(result.IsFailure);

        Assert.True(
            await signal.WasDeliveredAsync(10, DeliveryTimeout, TestContext.Current.CancellationToken),
            "The winner's bump should have been published — otherwise this test proves nothing about the loser.");

        Assert.False(
            await signal.WasDeliveredAsync(1, NonDeliveryWindow, TestContext.Current.CancellationToken),
            "The loser's failed commit must never publish its domain event.");

        await host.StopAsync(TestContext.Current.CancellationToken);
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

    private sealed class DeliverySignal<TValue>
        where TValue : notnull
    {
        private readonly ConcurrentDictionary<TValue, TaskCompletionSource<bool>> _signals = new();

        public void MarkDelivered(TValue value) =>
            Source(value).TrySetResult(true);

        public async Task<bool> WasDeliveredAsync(TValue value, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

            try
            {
                await Source(value).Task.WaitAsync(linked.Token);
                return true;
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                return false;
            }
        }

        private TaskCompletionSource<bool> Source(TValue value) =>
            _signals.GetOrAdd(value, static _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private sealed class FlushProbeRenamedProjection(DeliverySignal<string> signal) : IProjectionHandler<FlushProbeRenamed>
    {
        public Task HandleAsync(FlushProbeRenamed domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken)
        {
            signal.MarkDelivered(domainEvent.Name);
            return Task.CompletedTask;
        }
    }

    private sealed class TallyBumpedProjection(DeliverySignal<int> signal) : IProjectionHandler<TallyBumped>
    {
        public Task HandleAsync(TallyBumped domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken)
        {
            signal.MarkDelivered(domainEvent.By);
            return Task.CompletedTask;
        }
    }
}
