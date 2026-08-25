using DeadLetterFixture;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class IntegrationEventSubscriptionValidationTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    private const string UpstreamTopic = "upstream.always-fails";

    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan SettlingWindow = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task AHandlerWhoseTopicNoBoundPatternMatches_FailsTheStart()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartConsumerAsync(
                TestMessaging.UniqueQueueName("validation-no-match"),
                TestMessaging.UniqueExchangeName("validation-no-match"),
                TestMessaging.ContextName,
                "somewhere-else.*"));

        Assert.Contains(nameof(AlwaysFailsIntegrationEvent), thrown.Message, StringComparison.Ordinal);
        Assert.Contains(UpstreamTopic, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("somewhere-else.*", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHandlerForAnEventOfTheOwnContext_FailsTheStart()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartConsumerAsync(
                TestMessaging.UniqueQueueName("validation-own-context"),
                TestMessaging.UniqueExchangeName("validation-own-context"),
                TestMessaging.UpstreamContextName,
                "upstream.*"));

        Assert.Contains("this very context", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(TestMessaging.UpstreamContextName, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMatchingPatternFromAForeignContext_Starts()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        using var host = await StartConsumerAsync(
            TestMessaging.UniqueQueueName("validation-happy"),
            TestMessaging.UniqueExchangeName("validation-happy"),
            TestMessaging.ContextName,
            "upstream.*");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnEventCarryingTheOwnContextAsSource_IsDiscardedBeforeAnyHandlerRuns()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var queueName = TestMessaging.UniqueQueueName("self-consumption-probe");
        var exchangeName = TestMessaging.UniqueExchangeName("self-consumption-probe");

        var recorder = new AttemptRecorder();
        using var host = await StartConsumerAsync(queueName, exchangeName, TestMessaging.ContextName, "upstream.*", recorder);
        using var publisher = await StartPublisherAsync(exchangeName);
        var bus = publisher.Services.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(
            new AlwaysFailsIntegrationEvent("suppressed"),
            SourceContextHeader(TestMessaging.ContextName));

        await bus.PublishAsync(
            new AlwaysFailsIntegrationEvent("control"),
            SourceContextHeader(TestMessaging.UpstreamContextName));

        await WaitUntilSeenAsync(recorder, "control");

        var settle = DateTime.UtcNow + SettlingWindow;
        while (DateTime.UtcNow < settle)
        {
            Assert.DoesNotContain("suppressed", recorder.Names);
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnEventCarryingAForeignContextAsSource_ReachesTheHandler()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var queueName = TestMessaging.UniqueQueueName("foreign-source-probe");
        var exchangeName = TestMessaging.UniqueExchangeName("foreign-source-probe");

        var recorder = new AttemptRecorder();
        using var host = await StartConsumerAsync(queueName, exchangeName, TestMessaging.ContextName, "upstream.*", recorder);
        using var publisher = await StartPublisherAsync(exchangeName);
        var bus = publisher.Services.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(
            new AlwaysFailsIntegrationEvent("delivered"),
            SourceContextHeader(TestMessaging.UpstreamContextName));

        await WaitUntilSeenAsync(recorder, "delivered");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilSeenAsync(AttemptRecorder recorder, string name)
    {
        var deadline = DateTime.UtcNow + Grace;
        while (!recorder.Names.Contains(name))
        {
            Assert.True(
                DateTime.UtcNow < deadline,
                $"The consumer never saw '{name}' within {Grace}. Seen: {string.Join(", ", recorder.Names)}.");

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }

    private static DeliveryOptions SourceContextHeader(string contextName)
    {
        var delivery = new DeliveryOptions();
        delivery.Headers[IntegrationEventSourceContext.HeaderName] = contextName;
        return delivery;
    }

    private async Task<IHost> StartConsumerAsync(
        string queueName,
        string exchangeName,
        string contextName,
        string topicPattern,
        AttemptRecorder? recorder = null) =>
        await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(recorder ?? new AttemptRecorder());

                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                    options.UseMartenEventStore(postgres.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                    options.UseWolverineMessaging(rabbit.ConnectionUri, exchangeName, contextName);
                    options.SubscribeToIntegrationEvents(
                        queueName,
                        typeof(AlwaysFailsConsumer).Assembly,
                        topicPattern);
                });
            })
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .StartAsync(TestContext.Current.CancellationToken);

    private async Task<IHost> StartPublisherAsync(string exchangeName) =>
        await Host.CreateDefaultBuilder()
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;

                options.UseRabbitMq(rabbit.ConnectionUri)
                    .AutoProvision()
                    .DeclareExchange(exchangeName, exchange => exchange.IsDurable = true);

                options.PublishMessagesToRabbitMqExchange<AlwaysFailsIntegrationEvent>(
                    exchangeName,
                    _ => UpstreamTopic);
            })
            .StartAsync(TestContext.Current.CancellationToken);
}

