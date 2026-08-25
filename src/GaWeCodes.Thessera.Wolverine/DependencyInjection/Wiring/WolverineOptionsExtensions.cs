using System.Text.Json;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using GaWeCodes.Thessera.Wolverine.Messaging.IntegrationEvents;
using JasperFx;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;

namespace GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;

internal static class WolverineOptionsExtensions
{
    public const string DomainEventLocalQueueName = "thessera-domain-events";

    public const string ProjectionLocalQueueName = "thessera-projections";

    public const PartitionSlots DomainEventPartitionSlots = PartitionSlots.Five;

    public static readonly TimeSpan IdempotencyWindow = TimeSpan.FromDays(7);

    public static readonly TimeSpan[] TransientRetryCooldowns =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
    ];

    public static readonly TimeSpan[] UnknownRetryCooldowns =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(2),
    ];

    public static WolverineOptions ApplyThesseraIdempotencyWindow(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Durability.KeepAfterMessageHandling = IdempotencyWindow;

        return options;
    }

    public static WolverineOptions ApplyThesseraMessageStorageProvisioning(
        this WolverineOptions options,
        bool provisionInfrastructure)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AutoBuildMessageStorageOnStartup = provisionInfrastructure
            ? AutoCreate.CreateOrUpdate
            : AutoCreate.None;

        return options;
    }

    public static WolverineOptions ApplyThesseraDomainEventRouting(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Discovery.IncludeAssembly(typeof(DomainEventEnvelopeHandler).Assembly);

        options.CodeGeneration.AlwaysUseServiceLocationFor<IIntegrationEventPublisher>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IIntegrationEventSinkFactory>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<ProjectionRunner>();

        options.CodeGeneration.AlwaysUseServiceLocationFor<ISender>();

        options.MessagePartitioning.ByMessage<DomainEventEnvelope>(PartitionKeyFor);
        options.MessagePartitioning.ByMessage<ProjectionEnvelope>(projection => PartitionKeyFor(projection.Event));

        options.PublishMessage<DomainEventEnvelope>()
            .ToLocalQueue(DomainEventLocalQueueName)
            .PartitionProcessingByGroupId(DomainEventPartitionSlots)
            .UseDurableInbox();

        options.PublishMessage<ProjectionEnvelope>()
            .ToLocalQueue(ProjectionLocalQueueName)
            .PartitionProcessingByGroupId(DomainEventPartitionSlots)
            .UseDurableInbox();

        return options;
    }

    public static string PartitionKeyFor(DomainEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return $"{envelope.AggregateName}/{envelope.AggregateId}";
    }

    public static WolverineOptions ApplyThesseraMessagingPolicies(
        this WolverineOptions options,
        Func<Exception, bool> isTransientFault)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(isTransientFault);

        options.Policies.OnException<JsonException>().MoveToErrorQueue();
        options.Policies.OnException<DomainValidationException>().MoveToErrorQueue();
        options.Policies.OnException<BusinessRuleViolationException>().MoveToErrorQueue();

        options.Policies.OnException<Exception>(isTransientFault)
            .RetryWithCooldown(TransientRetryCooldowns);
        options.Policies.OnException<TimeoutException>()
            .RetryWithCooldown(TransientRetryCooldowns);

        options.Policies.OnException<Exception>()
            .RetryWithCooldown(UnknownRetryCooldowns)
            .Then.MoveToErrorQueue();

        return options;
    }

    public static WolverineOptions ApplyThesseraIntegrationEventTopics(
        this WolverineOptions options,
        string contextName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);

        options.Policies.AllSenders(sender => sender.CustomizeOutgoing(envelope =>
        {
            if (envelope.Message is IIntegrationEvent)
            {
                envelope.TopicName = TopicResolver.For(envelope.Message.GetType(), contextName);
            }
        }));

        return options;
    }

    public static WolverineOptions ApplyThesseraSubscriptionDiscovery(
        this WolverineOptions options,
        IntegrationEventSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(subscription);

        options.Discovery.IncludeAssembly(subscription.ConsumerAssembly);

        options.CodeGeneration.AlwaysUseServiceLocationFor<IntegrationEventSourceContext>();
        options.Policies.AddMiddleware(
            typeof(OwnContextIntegrationEventFilter),
            chain => chain.MessageType.IsAssignableTo(typeof(IIntegrationEvent)));

        return options;
    }
}
