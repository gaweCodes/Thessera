using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Persistence;
using Npgsql;

namespace GaWeCodes.Thessera.Npgsql;

/// <summary>
/// Turns a PostgreSQL unique-constraint violation into a conflict failure, so the command that hit
/// it returns a failed result instead of throwing a driver exception at its caller.
/// </summary>
/// <remarks>
/// Both shipped stores register this, which is what keeps them from drifting apart on what a
/// PostgreSQL error means. Register it yourself only when you write your own PostgreSQL-backed
/// adapter.
/// </remarks>
public sealed class PostgresFaultTranslator : IPersistenceFaultTranslator
{
    /// <summary>
    /// Recognises a unique-constraint violation and describes it as a failure.
    /// </summary>
    /// <param name="exception">The exception the commit threw, or one of its inner exceptions.</param>
    /// <param name="failure">
    /// When this method returns <see langword="true"/>, a conflict failure carrying
    /// <c>persistence.unique_violation</c> and naming the violated constraint when the driver
    /// reported one; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a <c>PostgresException</c> with SQL state <c>23505</c>;
    /// <see langword="false"/> for anything else, which is then offered to the next translator and,
    /// failing that, keeps propagating.
    /// </returns>
    public bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure)
    {
        if (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation)
        {
            failure = Failure.Conflict(PersistenceFailureCodes.UniqueViolation, Describe(violation));
            return true;
        }

        failure = null;
        return false;
    }

    private static string Describe(PostgresException exception) =>
        string.IsNullOrWhiteSpace(exception.ConstraintName)
            ? exception.Message
            : $"The unique constraint '{exception.ConstraintName}' was violated.";
}
