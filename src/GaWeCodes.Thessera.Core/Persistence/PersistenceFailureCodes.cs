namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// The failure codes every store reports the two shared write conflicts under, so a caller can
/// branch on them without knowing which database is underneath.
/// </summary>
/// <remarks>
/// A fault translator produces these; they leave the service in a failure and are therefore part of
/// the contract with callers.
/// </remarks>
public static class PersistenceFailureCodes
{
    /// <summary>
    /// Another writer changed the same aggregate first. Reload and try again — the version the
    /// command started from is no longer current.
    /// </summary>
    public const string ConcurrencyConflict = "persistence.concurrency_conflict";

    /// <summary>
    /// A unique constraint refused the write. The failure message names the constraint when the
    /// driver reported one.
    /// </summary>
    public const string UniqueViolation = "persistence.unique_violation";
}
