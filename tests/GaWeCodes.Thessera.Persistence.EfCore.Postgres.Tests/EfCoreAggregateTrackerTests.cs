using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EfCoreAggregateTrackerTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Commit_UsesTheAggregateCurrentState_NotTheStateCapturedAtAddTime()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new FlushProbeId(Guid.NewGuid());

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var probe = FlushProbe.Create(id);
            await repository.AddAsync(probe, TestContext.Current.CancellationToken);
            probe.Rename("renamed-before-commit");
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }

        using (var verification = host.Services.CreateScope())
        {
            var context = verification.ServiceProvider.GetRequiredService<FlushProbeContext>();
            var row = await context.Probes.SingleAsync(state => state.Id == id, TestContext.Current.CancellationToken);
            Assert.Equal("renamed-before-commit", row.Name);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Commit_AfterSuccess_ClearsTheAggregatesDomainEvents()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();

        using (var scope = host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var probe = FlushProbe.Create(new FlushProbeId(Guid.NewGuid()));

            await repository.AddAsync(probe, TestContext.Current.CancellationToken);
            Assert.NotEmpty(probe.DomainEvents);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
            Assert.Empty(probe.DomainEvents);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Commit_WhenSaveFails_KeepsTheAggregatesDomainEvents()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new FlushProbeId(Guid.NewGuid());

        using (var seed = host.Services.CreateScope())
        {
            var repository = seed.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
            var unitOfWork = seed.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var probe = FlushProbe.Create(id);
            await repository.AddAsync(probe, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }

        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        var firstRepository = first.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();

        var firstProbe = await firstRepository.GetByIdAsync(id, TestContext.Current.CancellationToken);
        var secondProbe = await secondRepository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        firstProbe!.Rename("first");
        secondProbe!.Rename("second");

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken));

        Assert.NotEmpty(secondProbe.DomainEvents);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
            .UseEfCoreStateStore<FlushProbeContext>(fixture.ConnectionString)
            .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
            .CustomizeWolverine(wolverine =>
            {
                wolverine.Durability.Mode = DurabilityMode.Solo;
                wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
            }));

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FlushProbeContext>().Database.ExecuteSqlRawAsync(
            "create table if not exists flush_probe_rows (id uuid primary key, name text not null, version bigint not null)",
            TestContext.Current.CancellationToken);

        return host;
    }
}
