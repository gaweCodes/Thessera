using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

public interface IEfCoreDatabaseDriver
{
    void ConfigureContext(DbContextOptionsBuilder builder, string connectionString);

    void PersistMessages(WolverineOptions options, string connectionString);

    bool IsTransientFault(Exception exception);

    IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators { get; }
}
