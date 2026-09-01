using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Persistence.EfCore.ReadModels;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Wolverine.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

/// <summary>
/// The state store as the composition root sees it: hand this to <c>UsePersistence</c>, together
/// with the driver for your database, and the host has an EF Core store.
/// </summary>
/// <typeparam name="TContext">
/// The write context. It maps the aggregate <em>states</em>, not the aggregates.
/// </typeparam>
/// <remarks>
/// A driver package normally wraps this in a vendor-named entry point rather than exposing it —
/// <c>UseEfCoreStateStore&lt;TContext&gt;</c> is exactly that for PostgreSQL.
/// </remarks>
/// <seealso cref="IEfCoreDatabaseDriver"/>
public sealed record EfCorePersistenceAdapter<TContext> : IPersistenceAdapter
    where TContext : DbContext
{
    private readonly IEfCoreDatabaseDriver _driver;
    private readonly Action<DbContextOptionsBuilder>? _configureContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCorePersistenceAdapter{TContext}"/> class.
    /// </summary>
    /// <param name="driver">The database-specific half: provider, outbox, faults.</param>
    /// <param name="writeConnectionString">The write database's connection string.</param>
    /// <param name="configureContext">
    /// Optional further configuration of the context options, applied after the driver's provider
    /// registration and therefore able to override it.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="driver"/> or <paramref name="writeConnectionString"/> is
    /// <see langword="null"/>.
    /// </exception>
    public EfCorePersistenceAdapter(
        IEfCoreDatabaseDriver driver,
        string writeConnectionString,
        Action<DbContextOptionsBuilder>? configureContext = null)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(writeConnectionString);

        _driver = driver;
        _configureContext = configureContext;
        WriteConnectionString = writeConnectionString;
    }

    /// <summary>
    /// Gets the name this store is called by in diagnostics and startup messages.
    /// </summary>
    public string Description => "UseEfCoreStateStore";

    /// <inheritdoc/>
    public string WriteConnectionString { get; }

    /// <summary>
    /// Gets the aggregate style this store supports.
    /// </summary>
    /// <value>
    /// Always <see cref="AggregateStyle.StateStored"/>. An aggregate derived from
    /// <c>EventSourcedAggregateRoot</c> is refused at startup unless the host waives its history
    /// with <c>WithoutEventHistory()</c>.
    /// </value>
    public AggregateStyle AggregateStyle => AggregateStyle.StateStored;

    /// <inheritdoc/>
    public bool IsTransientFault(Exception exception) => _driver.IsTransientFault(exception);

    /// <inheritdoc/>
    public void Register(PersistenceRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.Services;
        var connectionString = WriteConnectionString;
        var driver = _driver;
        var configureContext = _configureContext;

        services.AddDbContextWithWolverineIntegration<TContext>(builder =>
        {
            driver.ConfigureContext(builder, connectionString);
            configureContext?.Invoke(builder);
        });

        services.TryAddScoped(static provider =>
            new WriteDbContextAccessor(provider.GetRequiredService<TContext>()));
        services.TryAddScoped<EfCoreAggregateTracker>();
        services.TryAddSingleton<DomainEventEnvelopeFactory>();
        services.TryAddSingleton<StateStoredReadModelRebuildRunner<TContext>>();
        services.TryAddScoped<IUnitOfWork, EfCoreUnitOfWork<TContext>>();
        services.TryAddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPersistenceFaultTranslator, EfCoreFaultTranslator>());

        foreach (var translator in driver.FaultTranslators)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(translator));
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IStartupCheck, AggregateStateModelCheck<TContext>>());
        services.AddSingleton<IStartupCheck>(new WriteDbContextLifetimeCheck<TContext>(services));
        context.UseWolverineRuntime()
            .AddOutboxDurability(new EfCoreOutboxDurability(driver, connectionString));
        DeadLetterHealthCheckRegistration.Register(services);
    }
}
