using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Persistence;

/// <summary>
/// How a store binds the transactional outbox to its own database, so that the aggregate and the
/// events it raised are written in one transaction.
/// </summary>
/// <remarks>
/// This is the seam that makes "the aggregate was saved" and "its events will be published" one
/// decision instead of two. It is also the reason a store adapter ends up referencing the message
/// engine: an outbox has to know the engine to enlist in its transaction, which is a fact about
/// outboxes rather than a leak in this design.
/// <para>
/// Hand an implementation to <c>WolverineRuntimeActivator.AddOutboxDurability</c> from your
/// adapter's <c>Register</c> method.
/// </para>
/// </remarks>
public interface IOutboxDurabilityConfigurator
{
    /// <summary>
    /// Points the message store at your database.
    /// </summary>
    /// <param name="options">The message engine's options.</param>
    /// <remarks>
    /// Typically one call — the engine's <c>PersistMessagesWith…</c> for your provider, given the
    /// same connection string the store writes through.
    /// </remarks>
    void Configure(WolverineOptions options);
}
