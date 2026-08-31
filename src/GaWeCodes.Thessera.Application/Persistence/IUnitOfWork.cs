namespace GaWeCodes.Thessera.Application.Persistence;

/// <summary>
/// The commit boundary of one command: the single transaction that writes the aggregate and the
/// domain events it raised.
/// </summary>
/// <remarks>
/// A handler never calls this. The unit-of-work pipeline behaviour does, once per command, after
/// the handler returned a success — which is what makes "the aggregate was saved" and "its events
/// will be published" one decision instead of two.
/// <para>
/// A host with commands but no store and no implementation of this interface should fail fast
/// rather than silently reporting success while nothing is committed — a runtime-dependent
/// guarantee; see "What this package promises" in the package README.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Writes everything the command changed, in one transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the transaction has committed.</returns>
    /// <remarks>
    /// Translating a unique-constraint violation or a concurrency conflict into a failed result
    /// instead of a driver exception is runtime-dependent; see "What this package promises" in the
    /// package README.
    /// </remarks>
    Task CommitAsync(CancellationToken cancellationToken);
}
