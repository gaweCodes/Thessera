namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// What a store package hands to <c>UsePersistence</c> to announce itself.
/// </summary>
/// <remarks>
/// Exactly one may be selected per host: a bounded context has one write database, and a commit
/// cannot span two. A store package normally wraps its adapter in a named entry point —
/// <c>UseEfCoreStateStore</c>, <c>UseMartenEventStore</c> — rather than exposing the adapter itself.
/// </remarks>
/// <seealso cref="PersistenceRegistrationContext"/>
public interface IPersistenceAdapter
{
    /// <summary>
    /// Gets the name this store is called by in diagnostics and startup messages.
    /// </summary>
    /// <value>
    /// Usually the entry point a consumer typed, so that an error message names something they can
    /// find in their own composition.
    /// </value>
    string Description { get; }

    /// <summary>
    /// Gets the connection string of the write database.
    /// </summary>
    /// <remarks>
    /// Whatever runtime pairs with this store binds its outbox to this connection so that state
    /// and events end up in one transaction; that binding is runtime-dependent, see "What this
    /// package promises" in the package README.
    /// </remarks>
    string WriteConnectionString { get; }

    /// <summary>
    /// Gets the aggregate style this store supports.
    /// </summary>
    /// <remarks>
    /// Compared at startup against the style of every aggregate the host asks a repository for. A
    /// mismatch is refused rather than tolerated, because one direction cannot work at all and the
    /// other silently discards the history.
    /// </remarks>
    AggregateStyle AggregateStyle { get; }

    /// <summary>
    /// Decides whether a fault is worth retrying.
    /// </summary>
    /// <param name="exception">The exception to judge.</param>
    /// <returns>
    /// <see langword="true"/> for a fault that may well not recur — the runtime then retries with a
    /// cooldown instead of moving the message to the error queue.
    /// </returns>
    bool IsTransientFault(Exception exception);

    /// <summary>
    /// Registers everything this store needs.
    /// </summary>
    /// <param name="context">
    /// The service collection, whether the host may provision infrastructure, and the way to
    /// announce the runtime the store needs.
    /// </param>
    /// <remarks>
    /// Typically the repository, an aggregate tracker, the unit of work, the fault translators, the
    /// outbox durability and a read-model rebuild runner.
    /// </remarks>
    void Register(PersistenceRegistrationContext context);
}
