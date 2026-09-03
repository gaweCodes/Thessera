using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using GaWeCodes.Thessera.Npgsql;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

namespace GaWeCodes.Thessera.Persistence.EfCore.Postgres;

internal sealed class PostgresDatabaseDriver : IEfCoreDatabaseDriver
{
    public static PostgresDatabaseDriver Instance { get; } = new();

    public IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators { get; } = [new PostgresFaultTranslator()];

    public void ConfigureContext(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString);

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "PostgreSQL folds unquoted identifiers to lower case; Wolverine requires an all-lower-case " +
            "schema name here, not a security-sensitive normalization.")]
    public void PersistMessages(WolverineOptions options, string connectionString, MessageStoreRole role, Type? enrollContextType)
    {
        if (role == MessageStoreRole.Main)
        {
            options.PersistMessagesWithPostgresql(connectionString);
            return;
        }

        ArgumentNullException.ThrowIfNull(enrollContextType);

        // Wolverine tags every message store's own tables with its schema, so this store needs one
        // distinct from whatever schema the Main store (Marten or another EfCore store) already
        // claimed - otherwise both would fight over the same wolverine_* tables on the same
        // connection. Deriving it from the enrolled context's own name keeps it deterministic and
        // collision-free across however many ancillary stores a host ends up with.
        var schemaName = "wolverine_" + enrollContextType.Name.ToLowerInvariant();

        options.PersistMessagesWithPostgresql(connectionString, schemaName, MessageStoreRole.Ancillary)
            .Enroll(enrollContextType);
    }

    public bool IsTransientFault(Exception exception) => PostgresTransientFaults.IsTransient(exception);
}
