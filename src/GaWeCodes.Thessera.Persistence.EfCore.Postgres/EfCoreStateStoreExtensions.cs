using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.EfCore.Postgres;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using Microsoft.EntityFrameworkCore;

// Deliberate exception to the package/namespace rule. The composition entry points stay in the
// shared root namespace so a consumer's Program.cs reaches AddThessera and every Use*
// call with one using -- the same reason AddConsole() lives in Microsoft.Extensions.Logging
// and not in Microsoft.Extensions.Logging.Console. Every other type in this package matches
// its package name, so IDE0130 is suppressed here and nowhere else.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130
public static class EfCoreStateStoreExtensions
{
    public static ThesseraOptions UseEfCoreStateStore<TContext>(
        this ThesseraOptions options,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureContext = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionString);

        return options.UsePersistence(
            new EfCorePersistenceAdapter<TContext>(
                PostgresDatabaseDriver.Instance,
                connectionString,
                configureContext));
    }
}
