using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.Marten;

// Deliberate exception to the package/namespace rule. The composition entry points stay in the
// shared root namespace so a consumer's Program.cs reaches AddThessera and every Use*
// call with one using -- the same reason AddConsole() lives in Microsoft.Extensions.Logging
// and not in Microsoft.Extensions.Logging.Console. Every other type in this package matches
// its package name, so IDE0130 is suppressed here and nowhere else.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130
/// <summary>
/// The entry point that selects the event store: aggregates kept as the stream of events that
/// produced them, in PostgreSQL, through Marten.
/// </summary>
public static class MartenEventStoreExtensions
{
    /// <summary>
    /// Selects Marten event sourcing for this host — one of the family's two store choices.
    /// </summary>
    /// <param name="options">The options being configured inside <c>AddThessera</c>.</param>
    /// <param name="connectionString">The connection string of the write database.</param>
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
    /// aggregate has no history to replay and its repository cannot even be built. A host selects
    /// exactly one store; combining this with <c>UseEfCoreStateStore</c> is an error.
    /// </para>
    /// </remarks>
    public static ThesseraOptions UseMartenEventStore(
        this ThesseraOptions options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionString);

        return options.UsePersistence(new MartenPersistenceAdapter(connectionString));
    }
}
