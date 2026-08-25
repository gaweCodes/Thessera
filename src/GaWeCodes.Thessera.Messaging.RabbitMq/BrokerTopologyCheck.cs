using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Core.Startup;
using RabbitMQ.Client;

namespace GaWeCodes.Thessera.Messaging.RabbitMq;

internal sealed class BrokerTopologyCheck(
    RabbitMqTransportAdapter adapter,
    MessagingTransportRegistrationContext context) : IStartupCheck
{
    public StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (context.ProvisionsInfrastructure)
        {
            return;
        }

        var factory = new ConnectionFactory { Uri = adapter.RabbitMqUri };
        var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var closingConnection = connection.ConfigureAwait(false);

        await AssertExistsAsync(
            connection,
            channel => channel.ExchangeDeclarePassiveAsync(adapter.ExchangeName, cancellationToken),
            $"the exchange '{adapter.ExchangeName}' does not exist on the broker. Wolverine would still start and " +
            "every publish would return successfully while the broker discards the message, so no consumer would " +
            "ever see it",
            cancellationToken).ConfigureAwait(false);

        if (context.Subscription is not { } subscription)
        {
            return;
        }

        await AssertExistsAsync(
            connection,
            channel => channel.QueueDeclarePassiveAsync(subscription.EndpointName, cancellationToken),
            $"the queue '{subscription.EndpointName}' does not exist on the broker. Wolverine's listener would fail " +
            "with a bare AMQP 404 that names neither this host nor the reason",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task AssertExistsAsync(
        IConnection connection,
        Func<IChannel, Task> declarePassive,
        string complaint,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;

        try
        {
            var channel = await connection
                .CreateChannelAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await using var closingChannel = channel.ConfigureAwait(false);

            await declarePassive(channel).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failure = exception;
        }

        if (failure is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"This host does not provision infrastructure, and {complaint}. Start the host that selects " +
            "ProvisionInfrastructure(InfrastructureProvisioning.AtStartup) for this context Ã¢â‚¬â€ and let it finish Ã¢â‚¬â€ " +
            "before starting this one.",
            failure);
    }
}
