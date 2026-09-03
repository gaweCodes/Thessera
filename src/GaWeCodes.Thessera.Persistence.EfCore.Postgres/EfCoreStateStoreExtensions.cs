using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.EfCore.Postgres;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130

/// <summary>
/// The entry point that selects the state store: aggregates kept as current state in PostgreSQL,
/// through EF Core.
/// </summary>
/// <remarks>
/// Declared in the shared <c>GaWeCodes.Thessera</c> namespace rather than this package's own — like
/// <c>AddConsole()</c> in <c>Microsoft.Extensions.Logging</c> — so a consumer reaches every
/// <c>Use*</c>/<c>AddThessera</c> call with a single <c>using</c>.
/// </remarks>
public static class EfCoreStateStoreExtensions
{
    /// <summary>
    /// Selects PostgreSQL state storage for this host — one of the family's two store choices.
    /// </summary>
    /// <typeparam name="TContext">
    /// The write <see cref="DbContext"/>. It maps the aggregate <em>states</em>, not the aggregates,
    /// with child collections as owned types, <c>Version</c> as the concurrency token and
    /// <c>ApplyEntityKeyConversions()</c> called last in <c>OnModelCreating</c>.
    /// </typeparam>
    /// <param name="options">The options being configured inside <c>AddThessera</c>.</param>
    /// <param name="connectionString">The connection string of the write database.</param>
    /// <param name="configureContext">
    /// Optional further configuration of the context options, applied on top of the provider
    /// registration.
    /// </param>
    /// <param name="forAggregates">
    /// The aggregate types this store owns. Leave empty to make this the host's main store, owning
    /// every aggregate no other selected store claims — the common, single-store case. Name one or
    /// more aggregates to make this an additional store next to another one already selected on the
    /// same host, so an event-sourced aggregate and a state-stored aggregate can run side by side.
    /// </param>
    /// <returns>The same <paramref name="options"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="connectionString"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// One call registers the context with Wolverine's EF Core integration, the repository, the
    /// aggregate tracker, the unit of work, the PostgreSQL fault translators, the outbox durability,
    /// the read-model rebuild runner and a dead-letter health check.
    /// <para>
    /// A host selects at most one store without <paramref name="forAggregates"/>. Selecting this
    /// store a second time with a different connection string, or claiming an aggregate already
    /// claimed by another selected store, is an error: a commit cannot span two databases. An
    /// aggregate derived from <c>EventSourcedAggregateRoot</c> is refused here unless the host says
    /// <c>WithoutEventHistory()</c> — the state and version would be written correctly while the
    /// stream is silently and permanently lost.
    /// </para>
    /// </remarks>
    public static ThesseraOptions UseEfCoreStateStore<TContext>(
        this ThesseraOptions options,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureContext = null,
        params Type[] forAggregates)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionString);

        return options.UsePersistence(
            new EfCorePersistenceAdapter<TContext>(
                PostgresDatabaseDriver.Instance,
                connectionString,
                configureContext),
            forAggregates);
    }
}
