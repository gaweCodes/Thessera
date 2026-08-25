using RabbitMQ.Client;

namespace GaWeCodes.Thessera.Tests;

public sealed class BrokerProbe(IConnection connection, IChannel channel) : IAsyncDisposable
{
    public static async Task<BrokerProbe> ConnectAsync(Uri brokerUri, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = brokerUri };
        var connection = await factory.CreateConnectionAsync(cancellationToken);

        try
        {
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            return new BrokerProbe(connection, channel);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task BindQueueAsync(
        string queueName,
        string exchangeName,
        string topicPattern,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queueName,
            exchangeName,
            topicPattern,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);
    }

    public async Task PublishAsync(string exchangeName, string routingKey, CancellationToken cancellationToken) =>
        await channel.BasicPublishAsync(
            exchangeName,
            routingKey,
            mandatory: false,
            new BasicProperties(),
            ReadOnlyMemory<byte>.Empty,
            cancellationToken);

    public async Task<uint> MessageCountAsync(string queueName, CancellationToken cancellationToken) =>
        (await channel.QueueDeclarePassiveAsync(queueName, cancellationToken)).MessageCount;

    public async Task RedeclareExchangeAsync(
        string exchangeName,
        string exchangeType,
        CancellationToken cancellationToken) =>
        await channel.ExchangeDeclareAsync(
            exchangeName,
            exchangeType,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

    public async Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken) =>
        await channel.QueueDeleteAsync(
            queueName,
            ifUnused: false,
            ifEmpty: false,
            noWait: false,
            cancellationToken: cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await channel.DisposeAsync();
        await connection.DisposeAsync();
    }
}
