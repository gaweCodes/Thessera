using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

public sealed class ForeignDriverRoundTripTests : IDisposable
{
    private readonly string _databaseFile =
        Path.Combine(Path.GetTempPath(), $"efcore-substrate-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_databaseFile}";

    [Fact]
    public async Task AForeignDriverReconcilesNestedChildren()
    {
        using var host = await StartHostAsync();
        var id = new CrateId(Guid.NewGuid());

        await MutateAsync(host, id, crate => crate.AddItem("doomed", 9), create: true);

        var opened = await LoadAsync(host, id);
        var keptId = opened.Items.Single(item => item.Label == "kept").Id;
        var doomedId = opened.Items.Single(item => item.Label == "doomed").Id;

        await MutateAsync(host, id, crate => crate.Tag(keptId, "first"));
        await MutateAsync(host, id, crate =>
        {
            crate.ChangeQuantity(keptId, 42);
            crate.Tag(keptId, "second");
            crate.RemoveItem(doomedId);
            crate.AddItem("fresh", 7);
        });

        var reloaded = await LoadAsync(host, id);
        var kept = reloaded.Items.Single(item => item.Id == keptId);

        Assert.Equal(42, kept.Quantity);
        Assert.Equal(["first", "second"], kept.Tags.Select(tag => tag.Name).Order());
        Assert.Equal(["fresh", "kept"], reloaded.Items.Select(item => item.Label).Order());
        Assert.DoesNotContain(reloaded.Items, item => item.Id == doomedId);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyIdentity_ReturnsNull()
    {
        using var host = await StartHostAsync();
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Crate, CrateId>>();

        var reloaded = await repository.GetByIdAsync(new CrateId(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Null(reloaded);

        await host.StopAsync(TestContext.Current.CancellationToken);
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

    private static async Task<Crate> LoadAsync(IHost host, CrateId id)
    {
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Crate, CrateId>>();
        var crate = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(crate);
        return crate!;
    }

    private static async Task MutateAsync(IHost host, CrateId id, Action<Crate> mutate, bool create = false)
    {
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Crate, CrateId>>();

        Crate crate;

        if (create)
        {
            crate = Crate.Open(id, "kept");
            await repository.AddAsync(crate, TestContext.Current.CancellationToken);
        }
        else
        {
            crate = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("The crate was not found.");
        }

        mutate(crate);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(CrateOpened).Assembly)
            .UsePersistence(new EfCorePersistenceAdapter<CrateContext>(
                new SqliteDatabaseDriver(),
                ConnectionString))
                .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(wolverine =>
            {
                wolverine.Durability.Mode = DurabilityMode.Solo;
                wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
            }));

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CrateContext>();

        await context.Database.ExecuteSqlRawAsync(
            context.Database.GenerateCreateScript(),
            TestContext.Current.CancellationToken);

        return host;
    }
}
