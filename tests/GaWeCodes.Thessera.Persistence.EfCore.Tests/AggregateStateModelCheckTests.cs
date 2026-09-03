using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateStateModelCheckTests : IDisposable
{
    private readonly string _databaseFile =
        Path.Combine(Path.GetTempPath(), $"efcore-model-check-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_databaseFile}";

    [Fact]
    public async Task StartAsync_ComplexTypePropertyWithoutColumnName_ThrowsAtStartup()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(WalletOpened).Assembly)
            .UsePersistence(new EfCorePersistenceAdapter<WalletContextWithUndeclaredColumnName>(
                new SqliteDatabaseDriver(),
                ConnectionString))
                .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(wolverine =>
            {
                wolverine.Durability.Mode = DurabilityMode.Solo;
                wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
            }));

        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("'Money.Amount'", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_databaseFile);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
