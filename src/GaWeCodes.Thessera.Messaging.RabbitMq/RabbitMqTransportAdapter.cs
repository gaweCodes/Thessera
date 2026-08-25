using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Wolverine.Messaging.Transport;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.RabbitMQ;

namespace GaWeCodes.Thessera.Messaging.RabbitMq;

internal sealed class RabbitMqTransportAdapter(Uri rabbitMqUri, string exchangeName, string contextName)
    : IWolverineMessagingTransport
{
    public string Description => "UseWolverineMessaging";

    public string ContextName => contextName;

    public Uri RabbitMqUri => rabbitMqUri;

    public string ExchangeName => exchangeName;

    public void Register(MessagingTransportRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.UseWolverineRuntime();
        context.Services.AddSingleton<IStartupCheck>(new BrokerTopologyCheck(this, context));
    }

    public void Configure(WolverineOptions options, bool provisionInfrastructure)
    {
        ArgumentNullException.ThrowIfNull(options);

        var transport = options.UseRabbitMq(rabbitMqUri)
            .UseQuorumQueues()
            .ConfigureChannelCreation(channel =>
            {
                channel.PublisherConfirmationsEnabled = true;
                channel.PublisherConfirmationTrackingEnabled = true;
            });

        if (provisionInfrastructure)
        {
            transport.AutoProvision();
        }

        transport.DeclareExchange(exchangeName, exchange =>
        {
            exchange.IsDurable = true;
            exchange.ExchangeType = ExchangeType.Topic;
        });

        options.Publish(rule => rule.MessagesImplementing<IIntegrationEvent>()
            .ToRabbitTopics(exchangeName)
            .UseDurableOutbox());
    }

    public void ConfigureSubscription(WolverineOptions options, IntegrationEventSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(subscription);

        options.ListenToRabbitQueue(subscription.EndpointName, queue => queue.IsDurable = true).UseDurableInbox();

        var exchange = options.UseRabbitMq().BindExchange(exchangeName);
        foreach (var topicPattern in subscription.TopicPatterns)
        {
            exchange.ToQueue(subscription.EndpointName, bindingKey: topicPattern);
        }
    }
}
