using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace StateStored;

public sealed class StateStoredApplication : IAsyncDisposable
{
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=thessera_state_stored_example;Username=postgres;Password=postgres";

    private readonly IHost _host;

    private StateStoredApplication(IHost host) => _host = host;

    public static async Task<StateStoredApplication> StartAsync(string? connectionString = null, CancellationToken cancellationToken = default)
    {
        var selectedConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? DefaultConnectionString
            : connectionString;

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IReadingIdSequence, ReadingIdSequence>();
        builder.AddThessera(options => options
            .AddHandlersFrom(typeof(StateStoredApplication).Assembly)
            .AddDomainEventsFrom(typeof(Reading).Assembly)
            .UseEfCoreStateStore<ReadingDbContext>(selectedConnectionString)
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.ApplicationAssembly = typeof(StateStoredApplication).Assembly;
            }));

        var host = builder.Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReadingDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            """
            create table if not exists readings (
                id integer primary key,
                value integer not null,
                created_at timestamp with time zone not null,
                updated_at timestamp with time zone null,
                is_deleted boolean not null,
                deleted_at timestamp with time zone null,
                version bigint not null
            )
            """,
            cancellationToken).ConfigureAwait(false);

        var maxId = (await context.Readings
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .Select(state => state.Id.Value)
            .DefaultIfEmpty(0)
            .Max();
        scope.ServiceProvider.GetRequiredService<IReadingIdSequence>().Initialize(maxId);

        return new StateStoredApplication(host);
    }

    public Task<Result<ReadingOperationResponse>> CreateAsync(int value, CancellationToken cancellationToken = default) =>
        SendAsync(new CreateReading(value), cancellationToken);

    public Task<Result<ReadingListResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new ListReadings(), cancellationToken);

    public Task<Result<ReadingOperationResponse>> UpdateAsync(int id, int value, CancellationToken cancellationToken = default) =>
        SendAsync(new UpdateReading(id, value), cancellationToken);

    public Task<Result<ReadingOperationResponse>> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(new DeleteReading(id), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
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
