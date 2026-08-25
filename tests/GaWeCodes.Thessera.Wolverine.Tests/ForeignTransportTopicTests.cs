using Confluent.Kafka;
using ForeignBrokerHost;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(KafkaAndDatabaseCollection.Name)]
public sealed class ForeignTransportTopicTests(PostgreSqlFixture postgres, KafkaFixture kafka)
{
    private const string DeclaredTopic = "probe.foreign-transport-probe";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task TheDeclaredTopic_SurvivesATransportThatOffersNoTopicFunction()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(kafka.Available, kafka.SkipReason);

        var cancellationToken = TestContext.Current.CancellationToken;
        var name = Guid.NewGuid().ToString();

        using var host = await StartHostAsync(cancellationToken);

        using var consumer = StartConsumer();

        await host.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new ForeignTransportProbeIntegrationEvent(name));

        var payload = ReadPayload(consumer, cancellationToken);

        Assert.NotNull(payload);
        Assert.Contains(name, payload, StringComparison.Ordinal);

        await host.StopAsync(cancellationToken);
    }

    private IConsumer<string, string> StartConsumer()
    {
        var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = $"probe-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();

        consumer.Subscribe(DeclaredTopic);
        return consumer;
    }

    private static string? ReadPayload(IConsumer<string, string> consumer, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + DeliveryTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result?.Message is not null)
                {
                    return result.Message.Value;
                }
            }
            catch (ConsumeException exception) when (exception.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        return null;
    }

    private async Task<IHost> StartHostAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            options.UseMartenEventStore(postgres.ConnectionString)
                .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
            options.UseMessagingTransport(new KafkaTransportAdapter(kafka.BootstrapServers, "probe"));
        });

        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return host;
    }
}

[IntegrationEventTopic(DeclaredTopicName)]
public sealed record ForeignTransportProbeIntegrationEvent(string Name) : IIntegrationEvent
{
    public const string DeclaredTopicName = "probe.foreign-transport-probe";

    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
