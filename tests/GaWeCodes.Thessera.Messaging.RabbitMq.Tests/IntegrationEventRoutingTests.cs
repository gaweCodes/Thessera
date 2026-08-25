using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.Runtime;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class IntegrationEventRoutingTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    private readonly string _probeQueueName = TestMessaging.UniqueQueueName("integration-event-routing-probe");

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task PublishedIntegrationEvent_ReachesQueueBoundToThePlatformExchange()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        using var host = await StartHostAsync();
        var name = Guid.NewGuid().ToString();

        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new RoutingProbeIntegrationEvent(name));

        var received = await host.Services.GetRequiredService<RoutingProbeSignal>()
            .Received.WaitAsync(DeliveryTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(name, Assert.IsType<RoutingProbeIntegrationEvent>(received.Message).Name);

        Assert.Equal("rabbitmq", received.Destination?.Scheme);
        Assert.Contains(_probeQueueName, received.Destination?.ToString(), StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ABoundTopicPattern_FiltersInsteadOfForwardingEveryTopic()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await StartHostAsync();

        var matching = TestMessaging.UniqueQueueName("routing-matching");
        var unmatched = TestMessaging.UniqueQueueName("routing-unmatched");

        await using var broker = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, cancellationToken);
        await broker.BindQueueAsync(matching, TestMessaging.ExchangeName, "probe.*", cancellationToken);
        await broker.BindQueueAsync(unmatched, TestMessaging.ExchangeName, "other.*", cancellationToken);

        await host.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new RoutingProbeIntegrationEvent(Guid.NewGuid().ToString()));

        await host.Services.GetRequiredService<RoutingProbeSignal>()
            .Received.WaitAsync(DeliveryTimeout, cancellationToken);

        Assert.Equal(1u, await broker.MessageCountAsync(matching, cancellationToken));
        Assert.Equal(0u, await broker.MessageCountAsync(unmatched, cancellationToken));

        await broker.DeleteQueueAsync(matching, cancellationToken);
        await broker.DeleteQueueAsync(unmatched, cancellationToken);

        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task DomainEventEnvelope_IsNotRoutedToThePlatformExchange()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        using var host = await StartHostAsync();
        var runtime = host.Services.GetRequiredService<IWolverineRuntime>();

        var integrationEventRouting = runtime.ExplainRoutingFor(typeof(RoutingProbeIntegrationEvent)).ToText();
        var envelopeRouting = runtime.ExplainRoutingFor(typeof(DomainEventEnvelope)).ToText();

        Assert.Contains(TestMessaging.ExchangeName, integrationEventRouting, StringComparison.Ordinal);
        Assert.DoesNotContain(TestMessaging.ExchangeName, envelopeRouting, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishingAnIntegrationEventWithoutATopic_FailsFastInsteadOfSilentlyDisappearing()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        using var host = await StartHostAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Services.GetRequiredService<IMessageBus>()
                .PublishAsync(new TopiclessProbeIntegrationEvent()).AsTask());
        Assert.Contains(nameof(TopiclessProbeIntegrationEvent), exception.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync()
        => await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                    options.UseMartenEventStore(postgres.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                    options.UseWolverineMessaging(rabbit.ConnectionUri, TestMessaging.ExchangeName, TestMessaging.ContextName);
                });
                services.AddSingleton<RoutingProbeSignal>();
            })
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;

                options.Discovery.DisableConventionalDiscovery();
                options.Discovery.IncludeType(typeof(RoutingProbeHandler));

                options.ListenToRabbitQueue(_probeQueueName)
                    .ConfigureQueue(queue => queue
                        .BindTopic("probe.*")
                        .ToExchange(TestMessaging.ExchangeName));
            })
            .StartAsync(TestContext.Current.CancellationToken);
}

[IntegrationEventTopic("probe.routing-probe")]
public sealed record RoutingProbeIntegrationEvent(string Name) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record TopiclessProbeIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RoutingProbeSignal
{
    private readonly TaskCompletionSource<Envelope> _received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<Envelope> Received => _received.Task;

    public void MarkReceived(Envelope envelope) => _received.TrySetResult(envelope);
}

public static class RoutingProbeHandler
{
    public static void Handle(RoutingProbeIntegrationEvent message, Envelope envelope, RoutingProbeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        _ = message;
        signal.MarkReceived(envelope);
    }
}
