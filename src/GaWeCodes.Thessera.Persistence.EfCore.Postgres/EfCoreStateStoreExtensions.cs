using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.EfCore.Postgres;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using Microsoft.EntityFrameworkCore;

// Deliberate exception to the package/namespace rule. The composition entry points stay in the
// shared root namespace so a consumer's Program.cs reaches AddThessera and every Use*
// call with one using -- the same reason AddConsole() lives in Microsoft.Extensions.Logging
// and not in Microsoft.Extensions.Logging.Console. Every other type in this package matches
// its package name, so IDE0130 is suppressed here and nowhere else.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130
/// <summary>
/// The entry point that selects the state store: aggregates kept as current state in PostgreSQL,
/// through EF Core.
/// </summary>
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
    /// <returns>The same <paramref name="options"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="connectionString"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// One call registers the context with Wolverine's EF Core integration, the repository, the
    /// aggregate tracker, the unit of work, the PostgreSQL fault translators, the outbox durability,
    /// the read-model rebuild runner and a dead-letter health check.
    /// <para>
    /// A host selects exactly one store. Combining this with <c>UseMartenEventStore</c> is an error:
    /// a bounded context has one write database, and a commit cannot span two. An aggregate derived
    /// from <c>EventSourcedAggregateRoot</c> is refused here unless the host says
    /// <c>WithoutEventHistory()</c> — the state and version would be written correctly while the
    /// stream is silently and permanently lost.
    /// </para>
    /// </remarks>
    public static ThesseraOptions UseEfCoreStateStore<TContext>(
        this ThesseraOptions options,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureContext = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionString);

        return options.UsePersistence(
            new EfCorePersistenceAdapter<TContext>(
                PostgresDatabaseDriver.Instance,
                connectionString,
                configureContext));
    }
}
