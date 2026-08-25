using System.Text;
using DeadLetterFixture;
using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Wolverine;
using Wolverine.RabbitMQ;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class DeadLetterTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    private const string DeadLetterQueueName = "wolverine-dead-letter-queue";

    private const int ExpectedAttempts = 4;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan TransientObservationWindow = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task AConsumerThatAlwaysFails_IsRetriedAndThenDeadLettered()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var recorder = new AttemptRecorder();
        var exchangeName = TestMessaging.UniqueExchangeName("dead-letter-probe");
        using var host = await StartHostAsync(recorder, TestMessaging.UniqueQueueName("dead-letter-probe"), exchangeName);
        using var upstream = await StartUpstreamPublisherAsync(exchangeName);
        var name = Guid.NewGuid().ToString();

        await upstream.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new AlwaysFailsIntegrationEvent(name));

        var deadLettered = await WaitForDeadLetterAsync(name, recorder);
        Assert.Contains(name, deadLettered, StringComparison.Ordinal);

        Assert.True(
            recorder.Attempts >= ExpectedAttempts,
            $"An unclassified failure is retried {ExpectedAttempts - 1} times before it is dead-lettered, so the "
            + $"handler must have run at least {ExpectedAttempts} times. It ran {recorder.Attempts} times. "
            + "Redelivery may push the count higher, which is why this is a lower bound.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ADeterministicFailure_IsDeadLetteredWithoutBeingRetried()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var recorder = new AttemptRecorder();
        var exchangeName = TestMessaging.UniqueExchangeName("dead-letter-invalid-probe");
        using var host = await StartHostAsync(recorder, TestMessaging.UniqueQueueName("dead-letter-invalid-probe"), exchangeName);
        using var upstream = await StartUpstreamPublisherAsync(exchangeName);
        var name = Guid.NewGuid().ToString();

        await upstream.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new AlwaysInvalidIntegrationEvent(name));

        var deadLettered = await WaitForDeadLetterAsync(name, recorder);
        Assert.Contains(name, deadLettered, StringComparison.Ordinal);

        Assert.Equal(1, recorder.Names.Count(recorded => recorded == name));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ATransientFailure_IsRetriedAndDeliberatelyNotDeadLettered()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var recorder = new AttemptRecorder();
        var exchangeName = TestMessaging.UniqueExchangeName("transient-probe");
        using var host = await StartHostAsync(recorder, TestMessaging.UniqueQueueName("transient-probe"), exchangeName);
        using var upstream = await StartUpstreamPublisherAsync(exchangeName);
        var name = Guid.NewGuid().ToString();

        await upstream.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new AlwaysTimesOutIntegrationEvent(name));

        var deadline = DateTime.UtcNow + TransientObservationWindow;
        while (DateTime.UtcNow < deadline)
        {
            Assert.False(
                await IsInTheDeadLetterQueueAsync(name),
                "A transient failure must stay on the queue for redelivery instead of being dead-lettered: "
                + "a database failover outlasts any cooldown ladder, and the 7-day "
                + "idempotency window is what makes the redelivery safe.");

            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        Assert.True(
            recorder.Attempts > 1,
            $"The transient cooldown ladder should have retried the handler within {TransientObservationWindow}, "
            + $"but it ran {recorder.Attempts} time(s). Without a retry this test would prove nothing.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string> WaitForDeadLetterAsync(string name, AttemptRecorder recorder)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var body = await FindInTheDeadLetterQueueAsync(name);
            if (body is not null)
            {
                return body;
            }

            Assert.True(
                DateTime.UtcNow < deadline,
                $"The message was not dead-lettered within {Timeout}. Handler attempts: {recorder.Attempts}.");

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }
    }

    private async Task<bool> IsInTheDeadLetterQueueAsync(string name) =>
        await FindInTheDeadLetterQueueAsync(name) is not null;

    private async Task<string?> FindInTheDeadLetterQueueAsync(string name)
    {
        const int batchSize = 50;

        var factory = new ConnectionFactory { Uri = rabbit.ConnectionUri };
        await using var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        var inspected = new List<ulong>();
        string? found = null;

        try
        {
            for (var read = 0; read < batchSize; read++)
            {
                var message = await channel.BasicGetAsync(
                    DeadLetterQueueName,
                    autoAck: false,
                    TestContext.Current.CancellationToken);

                if (message is null)
                {
                    break;
                }

                inspected.Add(message.DeliveryTag);

                var body = Encoding.UTF8.GetString(message.Body.Span);
                if (found is null && body.Contains(name, StringComparison.Ordinal))
                {
                    found = body;
                }
            }
        }
        finally
        {
            foreach (var deliveryTag in inspected)
            {
                await channel.BasicNackAsync(
                    deliveryTag,
                    multiple: false,
                    requeue: true,
                    TestContext.Current.CancellationToken);
            }
        }

        return found;
    }

    private async Task<IHost> StartHostAsync(AttemptRecorder recorder, string queueName, string exchangeName) =>
        await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(recorder);

                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                    options.UseMartenEventStore(postgres.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                    options.UseWolverineMessaging(rabbit.ConnectionUri, exchangeName, TestMessaging.ContextName);
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

                options.PublishMessagesToRabbitMqExchange<AlwaysFailsIntegrationEvent>(
                    exchangeName,
                    _ => "upstream.always-fails");

                options.PublishMessagesToRabbitMqExchange<AlwaysInvalidIntegrationEvent>(
                    exchangeName,
                    _ => "upstream.always-invalid");

                options.PublishMessagesToRabbitMqExchange<AlwaysTimesOutIntegrationEvent>(
                    exchangeName,
                    _ => "upstream.always-times-out");
            })
            .StartAsync(TestContext.Current.CancellationToken);
}

[CollectionDefinition(Name)]
public sealed class BrokerAndDatabaseCollection
    : ICollectionFixture<PostgreSqlFixture>, ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "BrokerAndDatabase";
}

