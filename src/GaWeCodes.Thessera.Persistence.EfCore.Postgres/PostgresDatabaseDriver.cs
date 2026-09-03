using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
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
    private const int PostgresIdentifierMaxLength = 63;

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

        var schemaName = SchemaNameFor(enrollContextType);

        options.PersistMessagesWithPostgresql(connectionString, schemaName, MessageStoreRole.Ancillary)
            .Enroll(enrollContextType);
    }

    public bool IsTransientFault(Exception exception) => PostgresTransientFaults.IsTransient(exception);

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "PostgreSQL folds unquoted identifiers to lower case; Wolverine requires an all-lower-case " +
            "schema name here, not a security-sensitive normalization.")]
    private static string SchemaNameFor(Type enrollContextType)
    {
        const string prefix = "wolverine_";

        var qualifiedName = enrollContextType.FullName ?? enrollContextType.Name;
        var sanitized = new string(Array.ConvertAll(
            qualifiedName.ToCharArray(),
            static character => char.IsAsciiLetterOrDigit(character) ? character : '_')).ToLowerInvariant();
        var schemaName = prefix + sanitized;

        if (schemaName.Length <= PostgresIdentifierMaxLength)
        {
            return schemaName;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(qualifiedName)))[..8].ToLowerInvariant();
        var truncatedLength = PostgresIdentifierMaxLength - prefix.Length - hash.Length - 1;
        return $"{prefix}{sanitized[..truncatedLength]}_{hash}";
    }
}
