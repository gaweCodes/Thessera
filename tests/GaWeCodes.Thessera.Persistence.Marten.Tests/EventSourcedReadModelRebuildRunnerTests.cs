using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.ReadModels;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventSourcedReadModelRebuildRunnerTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Rebuild_ClearsOnceAndFoldsEveryStreamOfTheAggregate()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync(withRebuilder: true);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in ids)
        {
            await SendAsync(host, new StartRebuildProbe(id));
            await SendAsync(host, new RenameRebuildProbe(id, $"probe-{id:N}"));
        }

        await RunRebuildAsync(host);

        var log = host.Services.GetRequiredService<EventSourcedRebuildLog>();

        Assert.Equal(1, log.ClearCount);

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
    public async Task Rebuild_IgnoresTheStreamsOfEveryOtherAggregate()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync(withRebuilder: true);
        var foreignId = Guid.NewGuid();

        await SendAsync(host, new StartRebuildProbe(Guid.NewGuid()));
        await SendAsync(host, new StartRebuildNeighbour(foreignId));

        await RunRebuildAsync(host);

        var log = host.Services.GetRequiredService<EventSourcedRebuildLog>();

        Assert.DoesNotContain(log.Rebuilt, probe => probe.Id.Value == foreignId);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RebuildWithoutARegisteredRebuilder_ThrowsInsteadOfSilentlyDoingNothing()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync(withRebuilder: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RunRebuildAsync(host));

        Assert.Contains(nameof(RebuildProbe), exception.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Task RunRebuildAsync(IHost host) =>
        host.Services.GetRequiredService<EventSourcedReadModelRebuildRunner>()
            .RebuildAsync<RebuildProbe, RebuildProbeId>(TestContext.Current.CancellationToken);

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
                .AddDomainEventsFrom(typeof(RebuildProbeStarted).Assembly)
                .UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        builder.Services.AddScoped<ICommandHandler<StartRebuildProbe>, StartRebuildProbeHandler>();
        builder.Services.AddScoped<ICommandHandler<RenameRebuildProbe>, RenameRebuildProbeHandler>();
        builder.Services.AddScoped<ICommandHandler<StartRebuildNeighbour>, StartRebuildNeighbourHandler>();
        builder.Services.AddSingleton<EventSourcedRebuildLog>();

        if (withRebuilder)
        {
            builder.Services.AddScoped<IReadModelRebuilder<RebuildProbe, RebuildProbeId>, RecordingEventSourcedRebuilder>();
        }

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }
}

public sealed class EventSourcedRebuildLog
{
    private readonly List<RebuildProbe> _rebuilt = [];

    public int ClearCount { get; private set; }

    public IReadOnlyList<RebuildProbe> Rebuilt => _rebuilt;

    public void RecordClear() => ClearCount++;

    public void Record(RebuildProbe probe) => _rebuilt.Add(probe);
}

public sealed class RecordingEventSourcedRebuilder(EventSourcedRebuildLog log)
    : IReadModelRebuilder<RebuildProbe, RebuildProbeId>
{
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        log.RecordClear();
        return Task.CompletedTask;
    }

    public Task RebuildAsync(RebuildProbe aggregate, CancellationToken cancellationToken)
    {
        log.Record(aggregate);
        return Task.CompletedTask;
    }
}

public sealed record StartRebuildProbe(Guid Id) : ICommand;

public sealed record RenameRebuildProbe(Guid Id, string Name) : ICommand;

public sealed record StartRebuildNeighbour(Guid Id) : ICommand;

public sealed class StartRebuildProbeHandler(IRepository<RebuildProbe, RebuildProbeId> repository)
    : ICommandHandler<StartRebuildProbe>
{
    public async Task<Result> HandleAsync(StartRebuildProbe command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await repository.AddAsync(RebuildProbe.Start(new RebuildProbeId(command.Id)), cancellationToken);
        return Result.Success();
    }
}

public sealed class RenameRebuildProbeHandler(IRepository<RebuildProbe, RebuildProbeId> repository)
    : ICommandHandler<RenameRebuildProbe>
{
    public async Task<Result> HandleAsync(RenameRebuildProbe command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var probe = await repository.GetByIdAsync(new RebuildProbeId(command.Id), cancellationToken);
        probe!.Rename(command.Name);
        return Result.Success();
    }
}

public sealed class StartRebuildNeighbourHandler(IRepository<RebuildNeighbour, RebuildProbeId> repository)
    : ICommandHandler<StartRebuildNeighbour>
{
    public async Task<Result> HandleAsync(StartRebuildNeighbour command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await repository.AddAsync(RebuildNeighbour.Start(new RebuildProbeId(command.Id)), cancellationToken);
        return Result.Success();
    }
}

public readonly record struct RebuildProbeId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("rebuild-probe-started-v1")]
public sealed record RebuildProbeStarted(RebuildProbeId ProbeId) : DomainEvent;

[EventName("rebuild-probe-renamed-v1")]
public sealed record RebuildProbeRenamed(RebuildProbeId ProbeId, string Name) : DomainEvent;

[EventName("rebuild-neighbour-started-v1")]
public sealed record RebuildNeighbourStarted(RebuildProbeId ProbeId) : DomainEvent;

public sealed record RebuildProbeState(RebuildProbeId Id, string Name)
    : AggregateState<RebuildProbeState, RebuildProbeId>
{
    public static RebuildProbeState Empty => new(new RebuildProbeId(Guid.Empty), string.Empty);

    public override RebuildProbeState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        RebuildProbeStarted started => this with { Id = started.ProbeId },
        RebuildProbeRenamed renamed => this with { Name = renamed.Name },
        _ => this,
    };
}

public sealed record RebuildNeighbourState(RebuildProbeId Id)
    : AggregateState<RebuildNeighbourState, RebuildProbeId>
{
    public static RebuildNeighbourState Empty => new(new RebuildProbeId(Guid.Empty));

    public override RebuildNeighbourState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        RebuildNeighbourStarted started => this with { Id = started.ProbeId },
        _ => this,
    };
}

[AggregateName("rebuild-probe")]
public sealed class RebuildProbe : EventSourcedAggregateRoot<RebuildProbeId, RebuildProbeState>
{
    private RebuildProbe() : base(RebuildProbeState.Empty)
    {
    }

    public string Name => State.Name;

    public static RebuildProbe Start(RebuildProbeId id)
    {
        var probe = new RebuildProbe();
        probe.RaiseEvent(new RebuildProbeStarted(id));
        return probe;
    }

    public void Rename(string name) => RaiseEvent(new RebuildProbeRenamed(Id, name));
}

[AggregateName("rebuild-probe-neighbour")]
public sealed class RebuildNeighbour : EventSourcedAggregateRoot<RebuildProbeId, RebuildNeighbourState>
{
    private RebuildNeighbour() : base(RebuildNeighbourState.Empty)
    {
    }

    public static RebuildNeighbour Start(RebuildProbeId id)
    {
        var neighbour = new RebuildNeighbour();
        neighbour.RaiseEvent(new RebuildNeighbourStarted(id));
        return neighbour;
    }
}
