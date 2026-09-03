using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace EventSourcedWithMessaging;

public sealed class EventSourcedWithMessagingApplication : IAsyncDisposable
{
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=thessera_event_sourced_messaging_example;Username=postgres;Password=postgres";
    public const string DefaultRabbitMqUri = "amqp://guest:guest@localhost:5672/";
    public const string ExchangeName = "thessera-examples";
    public const string ContextName = "event-readings";
    public const string StreamKeyPrefix = "reading/";

    private readonly IHost _host;
    private readonly ReceivedEventsLogWriter _logWriter;

    private EventSourcedWithMessagingApplication(IHost host)
    {
        _host = host;
        _logWriter = host.Services.GetRequiredService<ReceivedEventsLogWriter>();
    }

    public static async Task<EventSourcedWithMessagingApplication> StartAsync(
        string? connectionString = null,
        Uri? rabbitMqUri = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var selectedConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? DefaultConnectionString
            : connectionString;
        var selectedRabbitMqUri = rabbitMqUri ?? new Uri(DefaultRabbitMqUri);
        var selectedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;
        var queueName = $"{ContextName}.received-events.{Guid.NewGuid():N}";

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IReadingIdSequence, ReadingIdSequence>();
        builder.Services.AddSingleton<IReadingStreamCatalog>(_ => new ReadingStreamCatalog(selectedConnectionString));
        builder.Services.AddSingleton<IReadingReadModelStore, ReadingReadModelStore>();
        builder.AddThessera(options => options
            .AddHandlersFrom(typeof(EventSourcedWithMessagingApplication).Assembly)
            .AddDomainEventsFrom(typeof(Reading).Assembly)
            .UseMartenEventStore(selectedConnectionString)
            .UseWolverineMessaging(selectedRabbitMqUri, ExchangeName, ContextName)
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.ApplicationAssembly = typeof(EventSourcedWithMessagingApplication).Assembly;
            }));

        builder.Services.AddSingleton(new SentIntegrationEventReporter());
        builder.Services.AddSingleton(new ReceivedEventsLogWriter(Path.Combine(selectedWorkingDirectory, "received-events.log")));
        builder.Services.Replace(ServiceDescriptor.Singleton<IIntegrationEventSinkFactory>(sp =>
            new ConsoleReportingIntegrationEventSinkFactory(
                ContextName,
                sp.GetRequiredService<SentIntegrationEventReporter>())));
        builder.Services.AddSingleton<IHostedService>(sp =>
            new ReceivedEventsPollingService(
                selectedRabbitMqUri,
                ExchangeName,
                queueName,
                $"{ContextName}.*",
                sp.GetRequiredService<ReceivedEventsLogWriter>()));

        var host = builder.Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        using var scope = host.Services.CreateScope();
        var maxId = await scope.ServiceProvider
            .GetRequiredService<IReadingStreamCatalog>()
            .GetMaxIdAsync(cancellationToken)
            .ConfigureAwait(false);
        scope.ServiceProvider.GetRequiredService<IReadingIdSequence>().Initialize(maxId);

        var application = new EventSourcedWithMessagingApplication(host);

        // The read model lives only in memory, so it starts empty on every process start
        // regardless of what is already in the event store - catch it up before anyone lists.
        await application.RebuildReadModelAsync(cancellationToken).ConfigureAwait(false);

        return application;
    }

    public async Task<Result<ReadingOperationResponse>> CreateAsync(int value, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new CreateReading(value), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public Task<Result<ReadingListResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListReadings(), cancellationToken);

    public async Task<Result<ReadingOperationResponse>> UpdateAsync(int id, int value, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new UpdateReading(id, value), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<ReadingOperationResponse>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new DeleteReading(id), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Clears and replays every <see cref="Reading"/> stream into the read model. Called once at
    /// startup and after every successful mutation; a real system with a larger read model would
    /// instead catch up incrementally or on a schedule, but a full rebuild is cheap enough here to
    /// double as the demonstration of <c>EventSourcedReadModelRebuildRunner</c>.
    /// </summary>
    public Task RebuildReadModelAsync(CancellationToken cancellationToken = default) =>
        _host.Services
            .GetRequiredService<EventSourcedReadModelRebuildRunner>()
            .RebuildAsync<Reading, ReadingId>(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }

    private async Task WaitForRoundTripAsync(Result<ReadingOperationResponse> result, int before, CancellationToken cancellationToken)
    {
        if (result.IsSuccess && result.Value.DomainEvents.Count > 0)
        {
            await _logWriter.WaitForCountAsync(before + result.Value.DomainEvents.Count, TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.SendAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TResult>> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.SendAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
