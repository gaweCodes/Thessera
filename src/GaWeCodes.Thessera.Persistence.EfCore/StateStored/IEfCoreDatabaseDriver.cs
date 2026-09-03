using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Persistence.Durability;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

/// <summary>
/// Everything this package does not know about your database: how to configure the context, where
/// the outbox lives, which faults are worth retrying, and how to read the ones that are not.
/// </summary>
/// <remarks>
/// Four members are the whole contract. Implement them and you have a state store — the repository,
/// the aggregate tracker, the unit of work, the state reconciliation, the model check and the
/// read-model rebuild runner are already written and database-neutral.
/// <para>
/// Give consumers an entry point named after the vendor, in the shape of
/// <c>UseEfCoreStateStore&lt;TContext&gt;</c>. Two drivers offering the same method name and
/// signature would hand a host that references both an ambiguity it cannot resolve.
/// </para>
/// </remarks>
/// <seealso cref="EfCorePersistenceAdapter{TContext}"/>
public interface IEfCoreDatabaseDriver
{
    /// <summary>
    /// Points the context at your database provider.
    /// </summary>
    /// <param name="builder">The context options being built.</param>
    /// <param name="connectionString">The write database's connection string.</param>
    /// <remarks>
    /// Only the provider registration belongs here; the caller's own configuration is applied on top
    /// afterwards, so it can still override what this sets.
    /// </remarks>
    void ConfigureContext(DbContextOptionsBuilder builder, string connectionString);

    /// <summary>
    /// Binds the transactional outbox to your database.
    /// </summary>
    /// <param name="options">The message engine's options.</param>
    /// <param name="connectionString">The write database's connection string.</param>
    /// <param name="role">
    /// <see cref="MessageStoreRole.Main"/> if this is the host's sole (or first) message store;
    /// <see cref="MessageStoreRole.Ancillary"/> if another store already claimed Main and this one
    /// must be enrolled against <paramref name="enrollContextType"/> instead.
    /// </param>
    /// <param name="enrollContextType">
    /// The write context to enroll this store's messages against. Required when
    /// <paramref name="role"/> is <see cref="MessageStoreRole.Ancillary"/>; ignored for
    /// <see cref="MessageStoreRole.Main"/>, where every unenrolled message lands here by default.
    /// </param>
    /// <remarks>
    /// This is where the outbox and your transaction become one commit. It is also why a driver ends
    /// up referencing the message engine: an outbox has to know it, which is a fact about outboxes
    /// rather than a leak in this seam.
    /// </remarks>
    void PersistMessages(WolverineOptions options, string connectionString, MessageStoreRole role, Type? enrollContextType);

    /// <summary>
    /// Decides whether a fault is worth retrying.
    /// </summary>
    /// <param name="exception">The exception to judge.</param>
    /// <returns>
    /// <see langword="true"/> for a dropped connection, a lock timeout or anything else that may
    /// well succeed on the next attempt. The runtime retries those with a cooldown and sends
    /// everything else to the error queue.
    /// </returns>
    bool IsTransientFault(Exception exception);

    /// <summary>
    /// Gets the translators that turn your driver's exceptions into failures.
    /// </summary>
    /// <value>
    /// The translators, tried in order. Returning none is allowed and means every persistence
    /// exception keeps propagating instead of reaching the caller as a failed result — for
    /// PostgreSQL, <c>GaWeCodes.Thessera.Npgsql</c> already provides one.
    /// </value>
    IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators { get; }
}
