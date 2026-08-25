using Npgsql;

namespace GaWeCodes.Thessera.Npgsql;

public static class PostgresTransientFaults
{
    public static bool IsTransient(Exception exception) =>
        exception is NpgsqlException { IsTransient: true };
}
