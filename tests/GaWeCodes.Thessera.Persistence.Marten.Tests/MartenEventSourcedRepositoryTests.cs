using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using JasperFx.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MartenEventSourcedRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveThenReload_ReturnsTheFoldedStateAndVersion()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CounterId(Guid.NewGuid());

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = Counter.Create(id);
            counter.Increment(5);

            await repository.AddAsync(counter, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }

        using (var verification = host.Services.CreateScope())
        {
            var repository = verification.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var reloaded = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

            Assert.NotNull(reloaded);
            Assert.Equal(5, reloaded!.Total);
            Assert.Equal(2, ((IStateOwner)reloaded).Version);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reload_ChangeAndCommit_AdvancesTheVersion()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CounterId(Guid.NewGuid());

        await SeedAsync(host, id, increments: 5);

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

            counter!.Increment(3);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }

        using (var verification = host.Services.CreateScope())
        {
            var repository = verification.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var reloaded = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

            Assert.NotNull(reloaded);
            Assert.Equal(8, reloaded!.Total);
            Assert.Equal(3, ((IStateOwner)reloaded).Version);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConcurrentCommitsOnTheSameStream_RaiseAConcurrencyException()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CounterId(Guid.NewGuid());

        await SeedAsync(host, id, increments: 1);

        using var scopeA = host.Services.CreateScope();
        using var scopeB = host.Services.CreateScope();

        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var counterA = await repositoryA.GetByIdAsync(id, TestContext.Current.CancellationToken);
        var counterB = await repositoryB.GetByIdAsync(id, TestContext.Current.CancellationToken);

        counterA!.Increment(1);
        await scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(TestContext.Current.CancellationToken);

        counterB!.Increment(1);
        var exception = await Record.ExceptionAsync(
            () => scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<EventStreamUnexpectedMaxEventIdException>(exception);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddAsync_WithEmptyIdentity_Throws()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var emptyHull = AggregateFactory.CreateEmpty<Counter>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddAsync(emptyHull, TestContext.Current.CancellationToken));

        Assert.Contains("has no identity", exception.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyIdentity_ReturnsNull()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();

        var reloaded = await repository.GetByIdAsync(new CounterId(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Null(reloaded);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(CounterCreated).Assembly)
            .UseMartenEventStore(fixture.ConnectionString)
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(ConfigureDurability));

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static async Task SeedAsync(IHost host, CounterId id, int increments)
    {
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var counter = Counter.Create(id);
        counter.Increment(increments);
        await repository.AddAsync(counter, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static void ConfigureDurability(WolverineOptions options)
    {
        options.Durability.Mode = DurabilityMode.Solo;
        options.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
    }
}
