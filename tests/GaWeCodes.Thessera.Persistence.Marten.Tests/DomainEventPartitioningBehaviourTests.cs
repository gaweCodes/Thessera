using System.Collections.Concurrent;
using GaWeCodes.Thessera.Core.DependencyInjection;
using JasperFx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DomainEventPartitioningBehaviourTests(PostgreSqlFixture postgres)
{
    private const string QueueName = "partitioning-probe";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task WithinOneGroup_AMessageInCooldownIsNotOvertaken()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);

        var recorder = new PartitionRecorder(expected: 2);
        using var host = await StartHostAsync(recorder);

        var bus = host.Services.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(new PartitionProbe("widget/same", 1, FailOnce: true));
        await bus.PublishAsync(new PartitionProbe("widget/same", 2, FailOnce: false));

        await recorder.Completed.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], recorder.Handled);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AcrossGroups_MessagesAreHandledConcurrently()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);

        const int probeCount = 20;
        var recorder = new PartitionRecorder(expected: probeCount) { DwellTime = TimeSpan.FromMilliseconds(300) };
        using var host = await StartHostAsync(recorder);

        var bus = host.Services.GetRequiredService<IMessageBus>();
        for (var order = 0; order < probeCount; order++)
        {
            await bus.PublishAsync(new PartitionProbe($"widget/{order}", order, FailOnce: false));
        }

        await recorder.Completed.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(
            recorder.MaximumConcurrency > 1,
            "Messages of different aggregates must be able to run at the same time. A maximum observed "
            + $"concurrency of {recorder.MaximumConcurrency} means the queue still serialises globally, which is "
            + "exactly what the partitioning is supposed to remove.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheThesseraQueues_ArePartitionedRatherThanGloballySequential()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);

        using var host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddThessera(options =>
            {
                options.AddDomainEventsFrom(typeof(DomainEventPartitioningBehaviourTests).Assembly);
                options.UseMartenEventStore(postgres.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
            }))
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .StartAsync(TestContext.Current.CancellationToken);

        var wolverine = host.Services.GetRequiredService<WolverineOptions>();

        foreach (var queueName in new[] { "thessera-domain-events", "thessera-projections" })
        {
            var endpoint = wolverine.Transports
                .SelectMany(transport => transport.Endpoints())
                .Single(candidate => candidate.Uri.ToString().Contains(queueName, StringComparison.Ordinal));

            Assert.Equal<PartitionSlots?>(PartitionSlots.Five, endpoint.GroupShardingSlotNumber);
            Assert.NotEqual(1, endpoint.MaxDegreeOfParallelism);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync(PartitionRecorder recorder) =>
        await Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSingleton(recorder))
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.PersistMessagesWithPostgresql(postgres.ConnectionString);
                options.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;

                options.Discovery.IncludeAssembly(typeof(DomainEventPartitioningBehaviourTests).Assembly);

                options.MessagePartitioning.ByMessage<PartitionProbe>(probe => probe.GroupId);

                options.PublishMessage<PartitionProbe>()
                    .ToLocalQueue(QueueName)
                    .PartitionProcessingByGroupId(PartitionSlots.Five)
                    .UseDurableInbox();

                options.Policies.OnException<PartitionProbeException>()
                    .RetryWithCooldown(TimeSpan.FromSeconds(5));
            })
            .StartAsync(TestContext.Current.CancellationToken);
}

public sealed record PartitionProbe(string GroupId, int Order, bool FailOnce);

public sealed class PartitionProbeException : Exception
{
    public PartitionProbeException()
        : base("The probe fails once so that it enters the retry cooldown.")
    {
    }

    public PartitionProbeException(string message)
        : base(message)
    {
    }

    public PartitionProbeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class PartitionRecorder(int expected)
{
    private readonly ConcurrentQueue<int> _handled = new();

    private readonly ConcurrentDictionary<int, byte> _attempted = new();

    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _running;

    private int _maximumConcurrency;

    public TimeSpan DwellTime { get; init; }

    public Task Completed => _completed.Task;

    public IReadOnlyCollection<int> Handled => _handled;

    public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

    public async Task RecordAsync(PartitionProbe probe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (probe.FailOnce && _attempted.TryAdd(probe.Order, 0))
        {
            throw new PartitionProbeException();
        }

        ObserveConcurrency(Interlocked.Increment(ref _running));

        try
        {
            if (DwellTime > TimeSpan.Zero)
            {
                await Task.Delay(DwellTime, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }

        _handled.Enqueue(probe.Order);

        if (_handled.Count == expected)
        {
            _completed.TrySetResult();
        }
    }

    private void ObserveConcurrency(int running)
    {
        var observed = Volatile.Read(ref _maximumConcurrency);
        while (running > observed)
        {
            var previous = Interlocked.CompareExchange(ref _maximumConcurrency, running, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }
}

public sealed class PartitionProbeHandler(PartitionRecorder recorder)
{
    public Task Handle(PartitionProbe message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return recorder.RecordAsync(message, cancellationToken);
    }
}
