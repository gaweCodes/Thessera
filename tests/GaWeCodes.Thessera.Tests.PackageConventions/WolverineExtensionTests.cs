using System.Reflection;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Dispatching;
using GaWeCodes.Thessera.Messaging.RabbitMq;
using GaWeCodes.Thessera.Persistence.Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

namespace GaWeCodes.Thessera.Tests;

public sealed class WolverineExtensionTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test";

    private const string DomainEventsQueue = "thessera-domain-events";

    private const string ProjectionsQueue = "thessera-projections";

    private static readonly Uri RabbitMqUri = new("amqp://localhost:5672");

    private static readonly Assembly TestAssembly = typeof(WolverineExtensionTests).Assembly;

    [Fact]
    public void AddThessera_WithAPersistenceStrategy_RegistersOneWolverineExtension()
    {
        using var provider = BuildProvider(options =>
            options.UseEfCoreStateStore<TestDbContext>(ConnectionString));

        Assert.Contains(
            provider.GetServices<IWolverineExtension>(),
            extension => extension.GetType().Name == "ThesseraWolverineExtension");
    }

    [Fact]
    public void AddThessera_WithoutAnyCapability_RegistersNoWolverineExtension()
    {
        using var provider = BuildProvider(_ => { });

        Assert.Empty(provider.GetServices<IWolverineExtension>());
    }

    [Fact]
    public void MessagingWithoutAPersistenceStrategy_FailsAtCompositionTime()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider(options => options.UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName)));

        Assert.Contains("durable", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("UseMartenEventStore", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MessagingAfterAPersistenceStrategy_IsAcceptedRegardlessOfCallOrder()
    {
        using var provider = BuildProvider(options => options
            .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName)
            .UseMartenEventStore(ConnectionString));

        Assert.Contains(
            provider.GetServices<IWolverineExtension>(),
            extension => extension.GetType().Name == "ThesseraWolverineExtension");
    }

    [Fact]
    public void EfCoreSelection_RegistersTheDbContext()
    {
        using var provider = BuildProvider(options =>
            options.UseEfCoreStateStore<TestDbContext>(ConnectionString));

        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<TestDbContext>());
    }

    [Fact]
    public void WithPersistenceAndUseWolverine_RoutesDomainAndProjectionEnvelopesToLocalQueues()
    {
        using var host = BuildHost(options => options.UseMartenEventStore(ConnectionString));
        var wolverine = host.Services.GetRequiredService<WolverineOptions>();
        var endpoints = wolverine.Transports.SelectMany(transport => transport.Endpoints()).ToArray();

        Assert.Contains(endpoints, endpoint => endpoint.Uri.ToString().Contains(DomainEventsQueue, StringComparison.Ordinal));
        Assert.Contains(endpoints, endpoint => endpoint.Uri.ToString().Contains(ProjectionsQueue, StringComparison.Ordinal));
    }

    [Fact]
    public void WithMessaging_AddsRabbitMqTransportAndDeclaresTheExchangeAsDurable()
    {
        using var host = BuildHost(options => options
            .UseMartenEventStore(ConnectionString)
            .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName));
        var rabbitMq = RabbitMqTransportOf(host.Services.GetRequiredService<WolverineOptions>());

        Assert.True(rabbitMq.Exchanges[TestMessaging.ExchangeName].IsDurable);
    }

    [Fact]
    public void WithMessaging_EnablesPublisherConfirmationsAndTheirTracking()
    {
        using var host = BuildHost(options => options
            .UseMartenEventStore(ConnectionString)
            .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName));
        var rabbitMq = RabbitMqTransportOf(host.Services.GetRequiredService<WolverineOptions>());
        var channel = new WolverineRabbitMqChannelOptions();

        Assert.False(channel.PublisherConfirmationsEnabled);
        Assert.False(channel.PublisherConfirmationTrackingEnabled);

        rabbitMq.ChannelCreationOptions!(channel);

        Assert.True(channel.PublisherConfirmationsEnabled);
        Assert.True(channel.PublisherConfirmationTrackingEnabled);
    }

    [Fact]
    public void WithSubscription_ListensOnTheQueueAndDeclaresItAsDurable()
    {
        using var host = BuildHost(options => options
            .UseMartenEventStore(ConnectionString)
            .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName)
            .SubscribeToIntegrationEvents("billing.integration-events", TestAssembly, "orders.*"));
        var rabbitMq = RabbitMqTransportOf(host.Services.GetRequiredService<WolverineOptions>());

        Assert.True(rabbitMq.Queues["billing.integration-events"].IsDurable);
        Assert.Contains(
            host.Services.GetRequiredService<WolverineOptions>().Transports.SelectMany(transport => transport.Endpoints()),
            endpoint => endpoint.Uri.ToString().Contains("billing.integration-events", StringComparison.Ordinal));
    }

    [Fact]
    public void Subscription_WithoutMessaging_FailsAtCompositionTime()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider(options =>
                options.SubscribeToIntegrationEvents("billing.integration-events", TestAssembly, "orders.*")));

        Assert.Contains("without a messaging transport", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subscription_CalledTwice_Throws()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider(options => options
                .UseMartenEventStore(ConnectionString)
                .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName)
                .SubscribeToIntegrationEvents("first", TestAssembly, "orders.*")
                .SubscribeToIntegrationEvents("second", TestAssembly, "billing.*")));

        Assert.Contains("one queue", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subscription_WithNoTopicPattern_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            BuildProvider(options => options
                .UseMartenEventStore(ConnectionString)
                .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName)
                .SubscribeToIntegrationEvents("billing.integration-events", TestAssembly)));
    }

    [Fact]
    public void Subscription_WithABlankTopicPattern_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            BuildProvider(options => options
                .UseMartenEventStore(ConnectionString)
                .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName)
                .SubscribeToIntegrationEvents("billing.integration-events", TestAssembly, "  ")));
    }

    [Fact]
    public void WithPersistence_WidensTheInboxIdempotencyWindow()
    {
        using var host = BuildHost(options => options.UseMartenEventStore(ConnectionString));
        var wolverine = host.Services.GetRequiredService<WolverineOptions>();

        Assert.Equal(TimeSpan.FromDays(7), wolverine.Durability.KeepAfterMessageHandling);
    }

    [Fact]
    public void WithoutPersistence_LeavesTheInboxIdempotencyWindowAtDefault()
    {
        using var host = BuildHost(_ => { });
        var wolverine = host.Services.GetRequiredService<WolverineOptions>();

        Assert.Equal(new DurabilitySettings().KeepAfterMessageHandling, wolverine.Durability.KeepAfterMessageHandling);
    }

    [Fact]
    public async Task WithPersistenceAndRouting_AHandlerCanDependOnISender()
    {
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                    options.UseEfCoreStateStore<TestDbContext>(ConnectionString);
                }))
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.Discovery.IncludeAssembly(typeof(WolverineExtensionTests).Assembly);
            })
            .StartAsync(TestContext.Current.CancellationToken);

        await host.Services.GetRequiredService<IMessageBus>()
            .InvokeAsync(new SenderDependentProbe(), TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static RabbitMqTransport RabbitMqTransportOf(WolverineOptions options)
        => options.Transports.OfType<RabbitMqTransport>().Single();

    private static IHost BuildHost(Action<ThesseraOptions> configure)
        => Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                    configure(options);
                }))
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .Build();

    private static ServiceProvider BuildProvider(Action<ThesseraOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            configure(options);
        });
        return services.BuildServiceProvider();
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}

public sealed record SenderDependentProbe;

public sealed class SenderDependentProbeHandler
{
    public static Task HandleAsync(SenderDependentProbe probe, ISender sender) => Task.CompletedTask;
}
