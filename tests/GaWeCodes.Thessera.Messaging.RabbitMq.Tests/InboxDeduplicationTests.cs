using DeadLetterFixture;
using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Wolverine;
using Wolverine.RabbitMQ;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class InboxDeduplicationTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan SettlingWindow = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task TheSameEnvelopeDeliveredTwice_RunsTheHandlerOnce()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var queueName = TestMessaging.UniqueQueueName("inbox-dedup-probe");
        var exchangeName = TestMessaging.UniqueExchangeName("inbox-dedup-probe");
        var name = Guid.NewGuid().ToString();

        using (var declaring = await StartConsumerAsync(queueName, exchangeName, new AttemptRecorder()))
        {
            await declaring.StopAsync(TestContext.Current.CancellationToken);
        }

        using (var upstream = await StartUpstreamPublisherAsync(exchangeName))
        {
            await upstream.Services.GetRequiredService<IMessageBus>()
                .PublishAsync(new RecordedIntegrationEvent(name));

            await upstream.StopAsync(TestContext.Current.CancellationToken);
        }

        await RepublishTheSameEnvelopeTwiceAsync(queueName);

        var recorder = new AttemptRecorder();
        using var consumer = await StartConsumerAsync(queueName, exchangeName, recorder);

        var deadline = DateTime.UtcNow + Grace;
        while (recorder.Attempts == 0)
        {
            Assert.True(DateTime.UtcNow < deadline, $"The consumer never saw the message within {Grace}.");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        var settle = DateTime.UtcNow + SettlingWindow;
        while (DateTime.UtcNow < settle)
        {
            Assert.True(
                recorder.Attempts == 1,
                "Wolverine's durable inbox deduplicates on the envelope id, so a second delivery of the same "
                + $"envelope must be acknowledged without running the handler. It ran {recorder.Attempts} times.");

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.Equal(name, Assert.Single(recorder.Names));

        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task RepublishTheSameEnvelopeTwiceAsync(string queueName)
    {
        var factory = new ConnectionFactory { Uri = rabbit.ConnectionUri };
        await using var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        var deadline = DateTime.UtcNow + Grace;
        BasicGetResult? original = null;
        while (original is null)
        {
            original = await channel.BasicGetAsync(queueName, autoAck: true, TestContext.Current.CancellationToken);

            Assert.True(
                original is not null || DateTime.UtcNow < deadline,
                $"The published message never arrived on '{queueName}' within {Grace}.");

            if (original is null)
            {
                await Task.Delay(200, TestContext.Current.CancellationToken);
            }
        }

        var properties = new BasicProperties(original.BasicProperties);
        var body = original.Body.ToArray();

        for (var delivery = 0; delivery < 2; delivery++)
        {
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: TestContext.Current.CancellationToken);
        }
    }

    private async Task<IHost> StartConsumerAsync(string queueName, string exchangeName, AttemptRecorder recorder) =>
        await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(recorder);

                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                    options.UseMartenEventStore(postgres.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                    options.UseWolverineMessaging(
                        rabbit.ConnectionUri,
                        exchangeName,
                        TestMessaging.ContextName);
                    options.SubscribeToIntegrationEvents(
                        queueName,
                        typeof(AlwaysFailsConsumer).Assembly,
                        "upstream.*");
                });
            })
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .StartAsync(TestContext.Current.CancellationToken);

    private async Task<IHost> StartUpstreamPublisherAsync(string exchangeName) =>
        await Host.CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;

                options.UseRabbitMq(rabbit.ConnectionUri)
                    .AutoProvision()
                    .DeclareExchange(exchangeName, exchange => exchange.IsDurable = true);

                options.PublishMessagesToRabbitMqExchange<RecordedIntegrationEvent>(
                    exchangeName,
                    _ => "upstream.recorded");
            })
            .StartAsync(TestContext.Current.CancellationToken);
}

