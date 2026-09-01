using Npgsql;

namespace GaWeCodes.Thessera.Npgsql;

/// <summary>
/// Answers whether a PostgreSQL fault is worth retrying.
/// </summary>
/// <remarks>
/// The runtime asks the store this question, and the answer decides between a retry with a cooldown
/// and a trip to the error queue. Keeping it in one place is what stops the two shipped stores
/// disagreeing about the same error.
/// </remarks>
public static class PostgresTransientFaults
{
    /// <summary>
    /// Determines whether the fault is transient — a dropped connection or a timeout rather than a
    /// wrong write.
    /// </summary>
    /// <param name="exception">The exception to judge.</param>
    /// <returns>
    /// <see langword="true"/> when Npgsql itself reports the fault as transient, so the same call
    /// may well succeed shortly after.
    /// </returns>
    public static bool IsTransient(Exception exception) =>
        exception is NpgsqlException { IsTransient: true };
}
