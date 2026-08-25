using DeadLetterFixture;
using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Exceptions;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class BrokerTopologyCheckTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    [Fact]
    public async Task AMissingExchange_FailsTheStartInsteadOfSwallowingEveryPublish()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var exchangeName = TestMessaging.UniqueExchangeName("absent-exchange");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartAsync(exchangeName, queueName: null, InfrastructureProvisioning.Never));

        Assert.Contains(exchangeName, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("InfrastructureProvisioning.AtStartup", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingQueue_FailsTheStartWithAMessageNamingIt()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var exchangeName = TestMessaging.UniqueExchangeName("queue-probe");
        var queueName = TestMessaging.UniqueQueueName("queue-probe");

        using (var provisioner = await StartAsync(exchangeName, queueName, InfrastructureProvisioning.AtStartup))
        {
            await provisioner.StopAsync(TestContext.Current.CancellationToken);
        }

        var absentQueue = TestMessaging.UniqueQueueName("absent-queue");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartAsync(exchangeName, absentQueue, InfrastructureProvisioning.Never));

        Assert.Contains(absentQueue, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("InfrastructureProvisioning.AtStartup", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProvisionedTopology_LetsAConsumingHostStart()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var exchangeName = TestMessaging.UniqueExchangeName("provisioned");
        var queueName = TestMessaging.UniqueQueueName("provisioned");

        using (var provisioner = await StartAsync(exchangeName, queueName, InfrastructureProvisioning.AtStartup))
        {
            await provisioner.StopAsync(TestContext.Current.CancellationToken);
        }

        using var consumer = await StartAsync(exchangeName, queueName, InfrastructureProvisioning.Never);
        await consumer.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheProvisioner_BindsTheSubscriptionQueue_InsteadOfOnlyDeclaringIt()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var cancellationToken = TestContext.Current.CancellationToken;
        var exchangeName = TestMessaging.UniqueExchangeName("binding");
        var queueName = TestMessaging.UniqueQueueName("binding");

        using (var provisioner = await StartAsync(exchangeName, queueName, InfrastructureProvisioning.AtStartup))
        {
            await provisioner.StopAsync(cancellationToken);
        }

        await using var broker = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, cancellationToken);

        await broker.PublishAsync(
            exchangeName,
            $"{TestMessaging.UpstreamContextName}.binding-probe",
            cancellationToken);
        Assert.Equal(1u, await broker.MessageCountAsync(queueName, cancellationToken));

        await broker.PublishAsync(exchangeName, "elsewhere.binding-probe", cancellationToken);
        Assert.Equal(1u, await broker.MessageCountAsync(queueName, cancellationToken));
    }

    [Fact]
    public async Task TheProvisioner_DeclaresATopicExchange_EvenWhenItOnlyPublishes()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var cancellationToken = TestContext.Current.CancellationToken;
        var exchangeName = TestMessaging.UniqueExchangeName("exchange-type");

        using (var provisioner = await StartAsync(exchangeName, queueName: null, InfrastructureProvisioning.AtStartup))
        {
            await provisioner.StopAsync(cancellationToken);
        }

        await using (var matching = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, cancellationToken))
        {
            await matching.RedeclareExchangeAsync(exchangeName, "topic", cancellationToken);
        }

        await using var mismatched = await BrokerProbe.ConnectAsync(rabbit.ConnectionUri, cancellationToken);

        await Assert.ThrowsAsync<OperationInterruptedException>(() =>
            mismatched.RedeclareExchangeAsync(exchangeName, "fanout", cancellationToken));
    }

    private async Task<IHost> StartAsync(
        string exchangeName,
        string? queueName,
        InfrastructureProvisioning provisioning) =>
        await Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddThessera(options =>
            {
                options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                options.UseMartenEventStore(postgres.ConnectionString)
                    .ProvisionInfrastructure(provisioning);
                options.UseWolverineMessaging(
                    rabbit.ConnectionUri,
                    exchangeName,
                    TestMessaging.ContextName);

                if (queueName is not null)
                {
                    options.SubscribeToIntegrationEvents(
                        queueName,
                        typeof(AlwaysFailsConsumer).Assembly,
                        "upstream.*");
                }
            }))
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .StartAsync(TestContext.Current.CancellationToken);
}
