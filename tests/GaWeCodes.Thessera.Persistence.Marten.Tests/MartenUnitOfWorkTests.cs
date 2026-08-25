using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using JasperFx.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MartenUnitOfWorkTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Commit_PersistsAndReloadsFoldedStateAndVersion()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CounterId(Guid.NewGuid());

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var counter = Counter.Create(id);
            counter.Increment(1);
            counter.Increment(2);
            await repository.AddAsync(counter, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }

        using (var verification = host.Services.CreateScope())
        {
            var repository = verification.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var reloaded = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

            Assert.NotNull(reloaded);
            Assert.Equal(3, reloaded!.Total);
            Assert.Equal(3, ((IStateOwner)reloaded).Version);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Commit_AfterSuccessfulSave_ClearsTrackedEvents()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = Counter.Create(new CounterId(Guid.NewGuid()));

            await repository.AddAsync(counter, TestContext.Current.CancellationToken);
            Assert.NotEmpty(counter.DomainEvents);

            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

            Assert.Empty(counter.DomainEvents);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Commit_WhenSaveFails_KeepsTrackedEvents()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CounterId(Guid.NewGuid());

        using (var seed = host.Services.CreateScope())
        {
            var repository = seed.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
            var unitOfWork = seed.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = Counter.Create(id);
            await repository.AddAsync(counter, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }

        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        var firstRepository = first.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();

        var firstCounter = await firstRepository.GetByIdAsync(id, TestContext.Current.CancellationToken);
        var secondCounter = await secondRepository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        firstCounter!.Increment(1);
        secondCounter!.Increment(1);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(TestContext.Current.CancellationToken);

        var exception = await Record.ExceptionAsync(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<EventStreamUnexpectedMaxEventIdException>(exception);
        Assert.NotEmpty(secondCounter.DomainEvents);

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

    private static void ConfigureDurability(WolverineOptions options)
    {
        options.Durability.Mode = DurabilityMode.Solo;
        options.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
    }
}
