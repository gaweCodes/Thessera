using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Persistence.EfCore.ReadModels;
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace MixedPersistenceWithMessaging;

/// <summary>
/// One host, two aggregates, two stores, one broker. <see cref="Account"/> keeps only its current
/// balance and runs on <c>GaWeCodes.Thessera.Persistence.EfCore.Postgres</c> - this is the host's
/// <em>main</em> store, selected without <c>forAggregates</c>, so it owns whatever aggregate no
/// other store claims. <see cref="Reading"/> keeps its full history and runs on
/// <c>GaWeCodes.Thessera.Persistence.Marten</c> - an <em>ancillary</em> store, claimed explicitly
/// for exactly that one aggregate. Both stores share the same PostgreSQL database in this example
/// (they do not have to); each aggregate still commits through exactly one of them, and no command
/// handler here ever asks for both - <c>THSS0007</c> would refuse that at compile time. On top of
/// the two-store split, every domain event raised by either aggregate is mapped to an integration
/// event and published through RabbitMQ under the same <see cref="ContextName"/>, so a consumer
/// sees one coherent event stream regardless of which store persisted the aggregate that raised it.
/// </summary>
public sealed class MixedPersistenceWithMessagingApplication : IAsyncDisposable
{
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=thessera_mixed_persistence_messaging_example;Username=postgres;******";
    public const string DefaultRabbitMqUri = "******localhost:5672/";
    public const string ExchangeName = "thessera-examples";
    public const string ContextName = "mixed-persistence";
    public const string ReadingStreamKeyPrefix = "reading/";

    private readonly IHost _host;
    private readonly ReceivedEventsLogWriter _logWriter;

    private MixedPersistenceWithMessagingApplication(IHost host)
    {
        _host = host;
        _logWriter = host.Services.GetRequiredService<ReceivedEventsLogWriter>();
    }

    public static async Task<MixedPersistenceWithMessagingApplication> StartAsync(
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
        builder.Services.AddSingleton<IAccountIdSequence, AccountIdSequence>();
        builder.Services.AddSingleton<IReadingIdSequence, ReadingIdSequence>();
        builder.Services.AddSingleton<IReadingStreamCatalog>(_ => new ReadingStreamCatalog(selectedConnectionString));
        builder.Services.AddSingleton<IAccountReadModelStore, AccountReadModelStore>();
        builder.Services.AddSingleton<IReadingReadModelStore, ReadingReadModelStore>();
        builder.AddThessera(options => options
            .AddHandlersFrom(typeof(MixedPersistenceWithMessagingApplication).Assembly)
            .AddDomainEventsFrom(typeof(Account).Assembly)
            .UseEfCoreStateStore<AccountDbContext>(selectedConnectionString)
            .UseMartenEventStore(selectedConnectionString, typeof(Reading))
            .UseWolverineMessaging(selectedRabbitMqUri, ExchangeName, ContextName)
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.ApplicationAssembly = typeof(MixedPersistenceWithMessagingApplication).Assembly;
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

        var accountContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        await accountContext.Database.ExecuteSqlRawAsync(
            """
            create table if not exists accounts (
                id integer primary key,
                balance numeric not null,
                opened_at timestamp with time zone not null,
                is_closed boolean not null,
                closed_at timestamp with time zone null,
                version bigint not null
            )
            """,
            cancellationToken).ConfigureAwait(false);

        var maxAccountId = (await accountContext.Accounts
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .Select(state => state.Id.Value)
            .DefaultIfEmpty(0)
            .Max();
        scope.ServiceProvider.GetRequiredService<IAccountIdSequence>().Initialize(maxAccountId);

        var maxReadingId = await scope.ServiceProvider
            .GetRequiredService<IReadingStreamCatalog>()
            .GetMaxIdAsync(cancellationToken)
            .ConfigureAwait(false);
        scope.ServiceProvider.GetRequiredService<IReadingIdSequence>().Initialize(maxReadingId);

        var application = new MixedPersistenceWithMessagingApplication(host);

        // Both read models live only in memory, so they start empty on every process start
        // regardless of what is already in their respective stores - catch them up before anyone lists.
        await application.RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);

        return application;
    }

    public async Task<Result<AccountOperationResponse>> OpenAccountAsync(decimal initialBalance, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new OpenAccount(initialBalance), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public Task<Result<AccountListResponse>> ListAccountsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListAccounts(), cancellationToken);

    public async Task<Result<AccountOperationResponse>> DepositAsync(int id, decimal amount, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new DepositIntoAccount(id, amount), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<AccountOperationResponse>> WithdrawAsync(int id, decimal amount, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new WithdrawFromAccount(id, amount), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<AccountOperationResponse>> CloseAccountAsync(int id, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new CloseAccount(id), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<ReadingOperationResponse>> CreateReadingAsync(int value, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new CreateReading(value), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public Task<Result<ReadingListResponse>> ListReadingsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListReadings(), cancellationToken);

    public async Task<Result<ReadingOperationResponse>> UpdateReadingAsync(int id, int value, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new UpdateReading(id, value), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<ReadingOperationResponse>> DeleteReadingAsync(int id, CancellationToken cancellationToken = default)
    {
        var before = _logWriter.EntryCount;
        var result = await SendAsync(new DeleteReading(id), cancellationToken).ConfigureAwait(false);
        await WaitForRoundTripAsync(result, before, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Clears and rebuilds both read models: <see cref="Account"/> from the current EF Core rows,
    /// <see cref="Reading"/> by replaying its Marten streams. Called once at startup and after every
    /// successful mutation of either aggregate; a real system with larger read models would instead
    /// catch up incrementally or on a schedule, but a full rebuild is cheap enough here to double as
    /// the demonstration of both rebuild runners in the same host.
    /// </summary>
    public async Task RebuildReadModelsAsync(CancellationToken cancellationToken = default)
    {
        // Rebuilding both read models after every successful mutation is intentionally simple here;
        // production code would usually rebuild only the affected model or catch up incrementally.
        await _host.Services
            .GetRequiredService<StateStoredReadModelRebuildRunner<AccountDbContext>>()
            .RebuildAsync<Account, AccountId, AccountState>(cancellationToken)
            .ConfigureAwait(false);

        await _host.Services
            .GetRequiredService<EventSourcedReadModelRebuildRunner>()
            .RebuildAsync<Reading, ReadingId>(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }

    private async Task WaitForRoundTripAsync(Result<AccountOperationResponse> result, int before, CancellationToken cancellationToken)
    {
        if (result.IsSuccess && result.Value.DomainEvents.Count > 0)
        {
            await _logWriter.WaitForCountAsync(before + result.Value.DomainEvents.Count, TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
        }
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
