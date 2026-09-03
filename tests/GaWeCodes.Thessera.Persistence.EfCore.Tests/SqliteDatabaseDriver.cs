using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Sqlite;

namespace GaWeCodes.Thessera.Tests;

public sealed class SqliteDatabaseDriver : IEfCoreDatabaseDriver
{
    public IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators => [];

    public void ConfigureContext(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSqlite(connectionString);
    }

    public void PersistMessages(WolverineOptions options, string connectionString, MessageStoreRole role, Type? enrollContextType)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Every test host wiring this driver up uses exactly one store, so it is always Main; the
        // ancillary path is exercised by the PostgreSQL driver's own tests instead.
        options.PersistMessagesWithSqlite(connectionString);
    }

    public bool IsTransientFault(Exception exception) => false;
}
