using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class DispatchIsolationTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task AFailedCommand_DeliversNeitherAProjectionNorAnIntegrationEvent()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var exchangeName = TestMessaging.UniqueExchangeName("isolation-no-commit");
        var queueName = TestMessaging.UniqueQueueName("isolation-no-commit");
        var signal = new IsolationSignal();

        using var host = await StartHostAsync(exchangeName, signal, projectionThrows: false, mapperThrows: false);

        await using var probe = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, TestContext.Current.CancellationToken);
        await probe.BindQueueAsync(queueName, exchangeName, "probe.*", TestContext.Current.CancellationToken);

        var rejected = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(new StartIsolationProbe(rejected, Reject: true), TestContext.Current.CancellationToken);
            Assert.False(result.IsSuccess);
        }

        var accepted = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(new StartIsolationProbe(accepted, Reject: false), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        var projected = await signal.Projected.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(accepted, projected);

        Assert.Equal(1u, await probe.MessageCountAsync(queueName, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(rejected, signal.ProjectedIds);

        await probe.DeleteQueueAsync(queueName, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AFailingProjection_DoesNotStopTheIntegrationEvent()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var exchangeName = TestMessaging.UniqueExchangeName("isolation-projection-fails");
        var queueName = TestMessaging.UniqueQueueName("isolation-projection-fails");
        var signal = new IsolationSignal();

        using var host = await StartHostAsync(exchangeName, signal, projectionThrows: true, mapperThrows: false);

        await using var probe = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, TestContext.Current.CancellationToken);
        await probe.BindQueueAsync(queueName, exchangeName, "probe.*", TestContext.Current.CancellationToken);

        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(new StartIsolationProbe(Guid.NewGuid(), Reject: false), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        await signal.Attempted.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await WaitForMessageAsync(probe, queueName);

        Assert.Equal(1u, await probe.MessageCountAsync(queueName, TestContext.Current.CancellationToken));

        await probe.DeleteQueueAsync(queueName, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AFailingMapper_KeepsTheProjectionFromRunning()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var exchangeName = TestMessaging.UniqueExchangeName("isolation-mapper-fails");
        var queueName = TestMessaging.UniqueQueueName("isolation-mapper-fails");
        var signal = new IsolationSignal();

        using var host = await StartHostAsync(exchangeName, signal, projectionThrows: false, mapperThrows: true);

        await using var probe = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, TestContext.Current.CancellationToken);
        await probe.BindQueueAsync(queueName, exchangeName, "probe.*", TestContext.Current.CancellationToken);

        var failing = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(new StartIsolationProbe(failing, Reject: false), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        await signal.MapperFailed.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0u, await probe.MessageCountAsync(queueName, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(failing, signal.ProjectedIds);

        await probe.DeleteQueueAsync(queueName, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitForMessageAsync(BrokerProbe probe, string queueName)
    {
        var deadline = DateTimeOffset.UtcNow.Add(Timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await probe.MessageCountAsync(queueName, TestContext.Current.CancellationToken) > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }

        Assert.Fail($"No integration event reached '{queueName}' within {Timeout}.");
    }

    private async Task<IHost> StartHostAsync(
        string exchangeName,
        IsolationSignal signal,
        bool projectionThrows,
        bool mapperThrows)
        => await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(IsolationProbeStarted).Assembly);
                    options.UseMartenEventStore(postgres.ConnectionString)
                        .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                    options.UseWolverineMessaging(rabbit.ConnectionUri, exchangeName, TestMessaging.ContextName);
                });

                services.AddSingleton(signal);
                services.AddScoped<ICommandHandler<StartIsolationProbe>, StartIsolationProbeHandler>();
                services.AddScoped<IProjectionHandler<IsolationProbeStarted>>(
                    provider => new IsolationProjection(provider.GetRequiredService<IsolationSignal>(), projectionThrows));
                services.AddScoped<IIntegrationEventMapper<IsolationProbeStarted>>(
                    provider => new IsolationMapper(provider.GetRequiredService<IsolationSignal>(), mapperThrows));
            })
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .StartAsync(TestContext.Current.CancellationToken);
}

public sealed class IsolationSignal
{
    private readonly TaskCompletionSource<Guid> _projected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _mapperFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<Guid> _projectedIds = [];

    public Task<Guid> Projected => _projected.Task;

    public Task Attempted => _attempted.Task;

    public Task MapperFailed => _mapperFailed.Task;

    public IReadOnlyList<Guid> ProjectedIds
    {
        get
        {
            lock (_projectedIds)
            {
                return [.. _projectedIds];
            }
        }
    }

    public void MarkAttempted() => _attempted.TrySetResult();

    public void MarkMapperFailed() => _mapperFailed.TrySetResult();

    public void MarkProjected(Guid id)
    {
        lock (_projectedIds)
        {
            _projectedIds.Add(id);
        }

        _projected.TrySetResult(id);
    }
}

public sealed class IsolationProjection(IsolationSignal signal, bool throws)
    : IProjectionHandler<IsolationProbeStarted>
{
    public Task HandleAsync(IsolationProbeStarted domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        signal.MarkAttempted();

        if (throws)
        {
            throw new InvalidOperationException("The projection handler fails on purpose.");
        }

        signal.MarkProjected(domainEvent.ProbeId.Value);
        return Task.CompletedTask;
    }
}

public sealed class IsolationMapper(IsolationSignal signal, bool throws) : IIntegrationEventMapper<IsolationProbeStarted>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(IsolationProbeStarted domainEvent, DomainEventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        if (throws)
        {
            signal.MarkMapperFailed();
            throw new InvalidOperationException("The integration event mapper fails on purpose.");
        }

        return [new IsolationProbeIntegrationEvent(metadata.EventId, metadata.OccurredAt, domainEvent.ProbeId.Value)];
    }
}

[IntegrationEventTopic("probe.isolation-probe-started")]
public sealed record IsolationProbeIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid ProbeId) : IIntegrationEvent;

public sealed record StartIsolationProbe(Guid Id, bool Reject) : ICommand;

public sealed class StartIsolationProbeHandler(IRepository<IsolationProbe, IsolationProbeId> repository)
    : ICommandHandler<StartIsolationProbe>
{
    public async Task<Result> HandleAsync(StartIsolationProbe command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await repository.AddAsync(IsolationProbe.Start(new IsolationProbeId(command.Id)), cancellationToken);

        return command.Reject
            ? throw new DomainValidationException("The command is rejected on purpose, after the aggregate was added.")
            : Result.Success();
    }
}

public readonly record struct IsolationProbeId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("isolation-probe-started-v1")]
public sealed record IsolationProbeStarted(IsolationProbeId ProbeId) : DomainEvent;

public sealed record IsolationProbeState(IsolationProbeId Id) : AggregateState<IsolationProbeState, IsolationProbeId>
{
    public static IsolationProbeState Empty => new(new IsolationProbeId(Guid.Empty));

    public override IsolationProbeState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        IsolationProbeStarted started => this with { Id = started.ProbeId },
        _ => this,
    };
}

[AggregateName("isolation-probe")]
public sealed class IsolationProbe : EventSourcedAggregateRoot<IsolationProbeId, IsolationProbeState>
{
    private IsolationProbe() : base(IsolationProbeState.Empty)
    {
    }

    public static IsolationProbe Start(IsolationProbeId id)
    {
        var probe = new IsolationProbe();
        probe.RaiseEvent(new IsolationProbeStarted(id));
        return probe;
    }
}
