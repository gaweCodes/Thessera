using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace EventSourced;

public sealed class EventSourcedApplication : IAsyncDisposable
{
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=thessera_event_sourced_example;Username=postgres;Password=postgres";
    public const string StreamKeyPrefix = "reading/";

    private readonly IHost _host;

    private EventSourcedApplication(IHost host) => _host = host;

    public static async Task<EventSourcedApplication> StartAsync(string? connectionString = null, CancellationToken cancellationToken = default)
    {
        var selectedConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? DefaultConnectionString
            : connectionString;

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IReadingIdSequence, ReadingIdSequence>();
        builder.Services.AddSingleton<IReadingStreamCatalog>(_ => new ReadingStreamCatalog(selectedConnectionString));
        builder.Services.AddSingleton<IReadingReadModelStore, ReadingReadModelStore>();
        builder.AddThessera(options => options
            .AddHandlersFrom(typeof(EventSourcedApplication).Assembly)
            .AddDomainEventsFrom(typeof(Reading).Assembly)
            .UseMartenEventStore(selectedConnectionString)
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.ApplicationAssembly = typeof(EventSourcedApplication).Assembly;
            }));

        var host = builder.Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        using var scope = host.Services.CreateScope();
        var maxId = await scope.ServiceProvider
            .GetRequiredService<IReadingStreamCatalog>()
            .GetMaxIdAsync(cancellationToken)
            .ConfigureAwait(false);
        scope.ServiceProvider.GetRequiredService<IReadingIdSequence>().Initialize(maxId);

        var application = new EventSourcedApplication(host);

        // The read model lives only in memory, so it starts empty on every process start
        // regardless of what is already in the event store - catch it up before anyone lists.
        await application.RebuildReadModelAsync(cancellationToken).ConfigureAwait(false);

        return application;
    }

    public Task<Result<ReadingOperationResponse>> CreateAsync(int value, CancellationToken cancellationToken = default) =>
        MutateAsync(new CreateReading(value), cancellationToken);

    public Task<Result<ReadingListResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListReadings(), cancellationToken);

    public Task<Result<ReadingOperationResponse>> UpdateAsync(int id, int value, CancellationToken cancellationToken = default) =>
        MutateAsync(new UpdateReading(id, value), cancellationToken);

    public Task<Result<ReadingOperationResponse>> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(new DeleteReading(id), cancellationToken);

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

    private async Task<Result<ReadingOperationResponse>> MutateAsync(
        ICommand<ReadingOperationResponse> command,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
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
