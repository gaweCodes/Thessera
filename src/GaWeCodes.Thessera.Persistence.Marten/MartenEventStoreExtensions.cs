using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.Marten;

#pragma warning disable IDE0130
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130

/// <summary>
/// The entry point that selects the event store: aggregates kept as the stream of events that
/// produced them, in PostgreSQL, through Marten.
/// </summary>
/// <remarks>
/// Declared in the shared <c>GaWeCodes.Thessera</c> namespace rather than this package's own — like
/// <c>AddConsole()</c> in <c>Microsoft.Extensions.Logging</c> — so a consumer reaches every
/// <c>Use*</c>/<c>AddThessera</c> call with a single <c>using</c>.
/// </remarks>
public static class MartenEventStoreExtensions
{
    /// <summary>
    /// Selects Marten event sourcing for this host — one of the family's two store choices.
    /// </summary>
    /// <param name="options">The options being configured inside <c>AddThessera</c>.</param>
    /// <param name="connectionString">The connection string of the write database.</param>
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
    /// One call configures Marten with string stream identity and registers the repository, the
    /// aggregate tracker, the unit of work, the Marten and PostgreSQL fault translators, the outbox
    /// durability, the read-model rebuild runner and a dead-letter health check.
    /// <para>
    /// <c>AddDomainEventsFrom</c> is not optional here: every <c>[EventName]</c> in those assemblies
    /// becomes Marten's event type name, and an event whose name is unknown cannot be read back.
    /// </para>
    /// <para>
    /// Only aggregates derived from <c>EventSourcedAggregateRoot</c> can run on this store — a plain
    /// aggregate has no history to replay and its repository cannot even be built. A host selects at
    /// most one store without <paramref name="forAggregates"/>; selecting this store a second time
    /// with a different connection string, or claiming an aggregate already claimed by another
    /// selected store, is an error.
    /// </para>
    /// </remarks>
    public static ThesseraOptions UseMartenEventStore(
        this ThesseraOptions options,
        string connectionString,
        params Type[] forAggregates)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionString);

        return options.UsePersistence(new MartenPersistenceAdapter(connectionString), forAggregates);
    }
}
