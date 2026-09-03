using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.EfCore.ReadModels;
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace MixedPersistence;

/// <summary>
/// One host, two aggregates, two stores. <see cref="Account"/> keeps only its current balance and
/// runs on <c>GaWeCodes.Thessera.Persistence.EfCore.Postgres</c> - this is the host's <em>main</em>
/// store, selected without <c>forAggregates</c>, so it owns whatever aggregate no other store
/// claims. <see cref="Reading"/> keeps its full history and runs on
/// <c>GaWeCodes.Thessera.Persistence.Marten</c> - an <em>ancillary</em> store, claimed explicitly
/// for exactly that one aggregate. Both stores share the same PostgreSQL database in this example
/// (they do not have to); each aggregate still commits through exactly one of them, and no command
/// handler here ever asks for both - <c>THSS0007</c> would refuse that at compile time.
/// </summary>
public sealed class MixedPersistenceApplication : IAsyncDisposable
{
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=thessera_mixed_persistence_example;Username=postgres;******";
    public const string ReadingStreamKeyPrefix = "reading/";

    private readonly IHost _host;

    private MixedPersistenceApplication(IHost host) => _host = host;

    public static async Task<MixedPersistenceApplication> StartAsync(string? connectionString = null, CancellationToken cancellationToken = default)
    {
        var selectedConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? DefaultConnectionString
            : connectionString;

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IAccountIdSequence, AccountIdSequence>();
        builder.Services.AddSingleton<IReadingIdSequence, ReadingIdSequence>();
        builder.Services.AddSingleton<IReadingStreamCatalog>(_ => new ReadingStreamCatalog(selectedConnectionString));
        builder.Services.AddSingleton<IAccountReadModelStore, AccountReadModelStore>();
        builder.Services.AddSingleton<IReadingReadModelStore, ReadingReadModelStore>();
        builder.AddThessera(options => options
            .AddHandlersFrom(typeof(MixedPersistenceApplication).Assembly)
            .AddDomainEventsFrom(typeof(Account).Assembly)
            .UseEfCoreStateStore<AccountDbContext>(selectedConnectionString)
            .UseMartenEventStore(selectedConnectionString, typeof(Reading))
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.ApplicationAssembly = typeof(MixedPersistenceApplication).Assembly;
            }));

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

        var application = new MixedPersistenceApplication(host);

        // Both read models live only in memory, so they start empty on every process start
        // regardless of what is already in their respective stores - catch them up before anyone lists.
        await application.RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);

        return application;
    }

    public Task<Result<AccountOperationResponse>> OpenAccountAsync(decimal initialBalance, CancellationToken cancellationToken = default) =>
        MutateAsync(new OpenAccount(initialBalance), cancellationToken);

    public Task<Result<AccountListResponse>> ListAccountsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListAccounts(), cancellationToken);

    public Task<Result<AccountOperationResponse>> DepositAsync(int id, decimal amount, CancellationToken cancellationToken = default) =>
        MutateAsync(new DepositIntoAccount(id, amount), cancellationToken);

    public Task<Result<AccountOperationResponse>> WithdrawAsync(int id, decimal amount, CancellationToken cancellationToken = default) =>
        MutateAsync(new WithdrawFromAccount(id, amount), cancellationToken);

    public Task<Result<AccountOperationResponse>> CloseAccountAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(new CloseAccount(id), cancellationToken);

    public Task<Result<ReadingOperationResponse>> CreateReadingAsync(int value, CancellationToken cancellationToken = default) =>
        MutateAsync(new CreateReading(value), cancellationToken);

    public Task<Result<ReadingListResponse>> ListReadingsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListReadings(), cancellationToken);

    public Task<Result<ReadingOperationResponse>> UpdateReadingAsync(int id, int value, CancellationToken cancellationToken = default) =>
        MutateAsync(new UpdateReading(id, value), cancellationToken);

    public Task<Result<ReadingOperationResponse>> DeleteReadingAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(new DeleteReading(id), cancellationToken);

    /// <summary>
    /// Clears and rebuilds both read models: <see cref="Account"/> from the current EF Core rows,
    /// <see cref="Reading"/> by replaying its Marten streams. Called once at startup and after every
    /// successful mutation of either aggregate; a real system would rebuild only the aggregate that
    /// changed, or catch up incrementally, but a full rebuild is cheap enough here to demonstrate both runners.
    /// </summary>
    public async Task RebuildReadModelsAsync(CancellationToken cancellationToken = default)
    {
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

    private async Task<Result<TResult>> MutateAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull
    {
        var result = await SendAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RebuildReadModelsAsync(cancellationToken).ConfigureAwait(false);
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
