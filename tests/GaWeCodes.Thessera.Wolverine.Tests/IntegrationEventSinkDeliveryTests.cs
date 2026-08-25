using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace GaWeCodes.Thessera.Tests;

public sealed class IntegrationEventSinkDeliveryTests
{
    private static readonly TimeSpan TrackingTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task DeliveredEnvelope_PublishesIntegrationEventWithOriginCorrelation()
    {
        using var host = await StartHostAsync();
        var recorder = host.Services.GetRequiredService<SinkProbeRecorder>();

        var session = await host.TrackActivity()
            .Timeout(TrackingTimeout)
            .WaitForMessageToBeReceivedAt<SinkProbeIntegrationEvent>(host)
            .PublishMessageAndWaitAsync(WrapProbeEvent("happy"));

        var origin = session.Sent.SingleEnvelope<DomainEventEnvelope>();
        var received = Assert.Single(recorder.Received);
        Assert.Equal(origin.CorrelationId, received.CorrelationId);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MapperFailingAfterSinkPublish_HoldsTheIntegrationEventBack()
    {
        using var host = await StartHostAsync();
        var recorder = host.Services.GetRequiredService<SinkProbeRecorder>();
        var crashSwitch = host.Services.GetRequiredService<SinkProbeCrashSwitch>();
        crashSwitch.Enabled = true;

        await host.TrackActivity()
            .Timeout(TrackingTimeout)
            .DoNotAssertOnExceptionsDetected()
            .PublishMessageAndWaitAsync(WrapProbeEvent("crash"));

        var tripped = await Task.WhenAny(
                crashSwitch.Tripped,
                Task.Delay(TrackingTimeout, TestContext.Current.CancellationToken))
            .ConfigureAwait(true) == crashSwitch.Tripped;

        Assert.True(
            tripped,
            "The crashing mapper never ran, so the envelope was not handled at all and the assertion below would hold for the wrong reason.");

        crashSwitch.Enabled = false;

        await host.TrackActivity()
            .Timeout(TrackingTimeout)
            .WaitForMessageToBeReceivedAt<SinkProbeIntegrationEvent>(host)
            .PublishMessageAndWaitAsync(WrapProbeEvent("sentinel"));

        var delivered = recorder.Received
            .Select(envelope => Assert.IsType<SinkProbeIntegrationEvent>(envelope.Message).Name)
            .ToArray();

        Assert.Equal(["sentinel"], delivered);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static DomainEventEnvelope WrapProbeEvent(string name)
        => new DomainEventEnvelopeSerializer(new DomainEventTypeRegistry([typeof(SinkProbeDomainEvent).Assembly]))
            .Wrap(new SinkProbeDomainEvent(name), Guid.NewGuid(), "sink-probe", "1", 1, DateTimeOffset.UtcNow);

    private static async Task<IHost> StartHostAsync()
        => await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddThessera(options => options.AddDomainEventsFrom(typeof(SinkProbeDomainEvent).Assembly));
                services.Replace(
                    ServiceDescriptor.Singleton<IIntegrationEventSinkFactory>(
                        new ProbeIntegrationEventSinkFactory(TestMessaging.ContextName)));
                services.AddSingleton<IIntegrationEventMapper<SinkProbeDomainEvent>, SinkProbeMapper>();
                services.AddSingleton<IIntegrationEventMapper<SinkProbeDomainEvent>, SinkProbeCrashingMapper>();
                services.AddSingleton<SinkProbeRecorder>();
                services.AddSingleton<SinkProbeCrashSwitch>();
            })
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;

                options.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;

                options.Discovery.IncludeAssembly(typeof(DomainEventEnvelopeHandler).Assembly);
                options.CodeGeneration.AlwaysUseServiceLocationFor<IIntegrationEventPublisher>();
                options.CodeGeneration.AlwaysUseServiceLocationFor<IIntegrationEventSinkFactory>();
                options.CodeGeneration.AlwaysUseServiceLocationFor<ProjectionRunner>();
                options.CodeGeneration.AlwaysUseServiceLocationFor<ISender>();
                options.PublishMessage<DomainEventEnvelope>().ToLocalQueue("sink-probe-domain-events").BufferedInMemory();
                options.PublishMessage<ProjectionEnvelope>().ToLocalQueue("sink-probe-projections").BufferedInMemory();

                options.LocalQueue("sink-probe-domain-events").BufferedInMemory();
                options.LocalQueue("sink-probe-projections").BufferedInMemory();

                options.Discovery.IncludeType(typeof(SinkProbeIntegrationEventHandler));
                options.PublishMessage<SinkProbeIntegrationEvent>().ToLocalQueue("sink-probe-integration");
            })
            .StartAsync();
}

[EventName("sink-probe-v1")]
public sealed record SinkProbeDomainEvent(string Name) : DomainEvent;

public sealed record SinkProbeIntegrationEvent(string Name, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
public sealed class SinkProbeRecorder
{
    private readonly List<Envelope> _received = [];

    public IReadOnlyList<Envelope> Received
    {
        get
        {
            lock (_received)
            {
                return [.. _received];
            }
        }
    }

    public void Record(Envelope envelope)
    {
        lock (_received)
        {
            _received.Add(envelope);
        }
    }
}

public sealed class SinkProbeCrashSwitch
{
    private readonly TaskCompletionSource _tripped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _enabled;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public Task Tripped => _tripped.Task;

    public void Trip() => _tripped.TrySetResult();
}

public sealed class SinkProbeMapper : IIntegrationEventMapper<SinkProbeDomainEvent>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(SinkProbeDomainEvent domainEvent, DomainEventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        return [new SinkProbeIntegrationEvent(domainEvent.Name, metadata.EventId, metadata.OccurredAt)];
    }
}

public sealed class SinkProbeCrashingMapper(SinkProbeCrashSwitch crashSwitch) : IIntegrationEventMapper<SinkProbeDomainEvent>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(SinkProbeDomainEvent domainEvent, DomainEventMetadata metadata)
    {
        if (!crashSwitch.Enabled)
        {
            return [];
        }

        crashSwitch.Trip();
        throw new InvalidOperationException("Simulated failure after the sink publish.");
    }
}

public sealed class ProbeIntegrationEventSinkFactory(string sourceContext) : IIntegrationEventSinkFactory
{
    public IIntegrationEventSink Create(IMessageEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        return new ProbeIntegrationEventSink(emitter, sourceContext);
    }
}

public sealed class ProbeIntegrationEventSink(IMessageEmitter emitter, string sourceContext) : IIntegrationEventSink
{
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return emitter.PublishAsync(
            integrationEvent,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [IntegrationEventSourceContext.HeaderName] = sourceContext,
            },
            cancellationToken);
    }
}

public static class SinkProbeIntegrationEventHandler
{
    public static void Handle(SinkProbeIntegrationEvent message, Envelope envelope, SinkProbeRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        _ = message;
        recorder.Record(envelope);
    }
}
