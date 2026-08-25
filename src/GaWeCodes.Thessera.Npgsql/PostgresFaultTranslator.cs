using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Persistence;
using Npgsql;

namespace GaWeCodes.Thessera.Npgsql;

public sealed class PostgresFaultTranslator : IPersistenceFaultTranslator
{
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
