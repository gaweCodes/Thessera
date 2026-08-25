using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.ReadModels;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Persistence.EfCore.ReadModels;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReadModelRebuildRunnerTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Rebuild_ClearsOnceAndReplaysEveryAggregateFromItsCurrentState()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync(withRebuilder: true);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in ids)
        {
            await SendAsync(host, new StartFlushProbe(id));
            await SendAsync(host, new RenameFlushProbe(id, $"probe-{id:N}"));
        }

        await RunRebuildAsync(host);

        var log = host.Services.GetRequiredService<RebuildLog>();

        Assert.Equal(1, log.ClearCount);
        Assert.Equal(ids.Length, log.Rebuilt.Count);

        foreach (var id in ids)
        {
            var probe = log.Rebuilt.Single(entry => entry.Id.Value == id);

            Assert.Equal($"probe-{id:N}", probe.Name);
            Assert.Equal(2, ((IStateOwner)probe).Version);
            Assert.Empty(probe.DomainEvents);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Rebuild_RunsTwiceWithTheSameResult()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync(withRebuilder: true);
        var id = Guid.NewGuid();

        await SendAsync(host, new StartFlushProbe(id));

        await RunRebuildAsync(host);
        var afterFirst = host.Services.GetRequiredService<RebuildLog>().Rebuilt.Count;

        await RunRebuildAsync(host);
        var log = host.Services.GetRequiredService<RebuildLog>();

        Assert.Equal(2, log.ClearCount);
        Assert.Equal(afterFirst * 2, log.Rebuilt.Count);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RebuildWithoutARegisteredRebuilder_ThrowsInsteadOfSilentlyDoingNothing()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync(withRebuilder: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RunRebuildAsync(host));

        Assert.Contains(nameof(FlushProbe), exception.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Task RunRebuildAsync(IHost host) =>
        host.Services.GetRequiredService<StateStoredReadModelRebuildRunner<FlushProbeContext>>()
            .RebuildAsync<FlushProbe, FlushProbeId, FlushProbeState>(TestContext.Current.CancellationToken);

    private static async Task SendAsync(IHost host, ICommand command)
    {
        using var scope = host.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.SendAsync(command, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }

    private async Task<IHost> StartHostAsync(bool withRebuilder)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
                .UseEfCoreStateStore<FlushProbeContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        builder.Services.AddScoped<ICommandHandler<StartFlushProbe>, StartFlushProbeHandler>();
        builder.Services.AddScoped<ICommandHandler<RenameFlushProbe>, RenameFlushProbeHandler>();
        builder.Services.AddSingleton<RebuildLog>();

        if (withRebuilder)
        {
            builder.Services.AddScoped<IReadModelRebuilder<FlushProbe, FlushProbeId>, RecordingRebuilder>();
        }

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FlushProbeContext>();

        await context.Database.ExecuteSqlRawAsync(
            "create table if not exists flush_probe_rows (id uuid primary key, name text not null, version bigint not null)",
            TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "delete from flush_probe_rows",
            TestContext.Current.CancellationToken);

        return host;
    }
}

public sealed class RebuildLog
{
    private readonly List<FlushProbe> _rebuilt = [];

    public int ClearCount { get; private set; }

    public IReadOnlyList<FlushProbe> Rebuilt => _rebuilt;

    public void RecordClear() => ClearCount++;

    public void Record(FlushProbe probe) => _rebuilt.Add(probe);
}

public sealed class RecordingRebuilder(RebuildLog log) : IReadModelRebuilder<FlushProbe, FlushProbeId>
{
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        log.RecordClear();
        return Task.CompletedTask;
    }

    public Task RebuildAsync(FlushProbe aggregate, CancellationToken cancellationToken)
    {
        log.Record(aggregate);
        return Task.CompletedTask;
    }
}
