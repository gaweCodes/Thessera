using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace ForeignBrokerHost;

public static class MatrixHost
{
    private const string WriteConnectionString = "DataSource=matrix-foreign-broker";

    public const string BootstrapServers = "localhost:9092";

    public const string ContextName = "matrix";

    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(MatrixHost).Assembly)
            .UsePersistence(new EfCorePersistenceAdapter<MatrixDbContext>(
                new ForeignDatabaseDriver(),
                WriteConnectionString))
            .UseMessagingTransport(new KafkaTransportAdapter(BootstrapServers, ContextName)));

        return builder.Build();
    }
}

public sealed class ForeignDatabaseDriver : IEfCoreDatabaseDriver
{
    public IReadOnlyList<IPersistenceFaultTranslator> FaultTranslators => [];

    public void ConfigureContext(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseInMemoryDatabase(connectionString);
    }

    public void PersistMessages(WolverineOptions options, string connectionString)
    {
    }

    public bool IsTransientFault(Exception exception) => false;
}

public sealed class MatrixDbContext(DbContextOptions<MatrixDbContext> options) : DbContext(options);

[EventName("matrix-foreign-broker-probe-v1")]
public sealed record MatrixProbe(string Value) : DomainEvent;
