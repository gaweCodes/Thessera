using System.Text;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace MixedPersistenceWithMessaging;

public sealed class ReceivedEventsPollingService(
    Uri brokerUri,
    string exchangeName,
    string queueName,
    string routingKey,
    ReceivedEventsLogWriter logWriter) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { Uri = brokerUri };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken)
            .ConfigureAwait(false);
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: true, cancellationToken: stoppingToken)
            .ConfigureAwait(false);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey, cancellationToken: stoppingToken)
            .ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await channel.BasicGetAsync(queueName, autoAck: false, stoppingToken).ConfigureAwait(false);
            if (message is null)
            {
                await Task.Delay(100, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var payload = Encoding.UTF8.GetString(message.Body.ToArray());
            await logWriter.AppendAsync(message.RoutingKey, payload, stoppingToken).ConfigureAwait(false);
            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken: stoppingToken)
                .ConfigureAwait(false);
        }
    }
}
