using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EfCoreAggregateRoundTripTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task LoadedAggregate_KeepsItsIdentityAndPersistsSubsequentChanges()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = Guid.NewGuid();

        await SendAsync(host, new StartFlushProbe(id));
        await SendAsync(host, new RenameFlushProbe(id, "renamed"));

        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
        var reloaded = await repository.GetByIdAsync(new FlushProbeId(id), TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);

        Assert.Equal(id, reloaded!.Id.Value);
        Assert.Equal("renamed", reloaded.Name);

        Assert.Empty(reloaded.DomainEvents);
        Assert.Equal(2, ((IStateOwner)reloaded).Version);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TwoConcurrentRenames_LetTheSecondCommitFailAsAConflict()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = Guid.NewGuid();
        await SendAsync(host, new StartFlushProbe(id));

        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        var firstProbe = await LoadAsync(first, id);
        var secondProbe = await LoadAsync(second, id);

        firstProbe.Rename("first");
        secondProbe.Rename("second");

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MissingAggregate_ReturnsNull()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();

        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();

        var missing = await repository.GetByIdAsync(
            new FlushProbeId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Null(missing);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<FlushProbe> LoadAsync(IServiceScope scope, Guid id)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
        var probe = await repository.GetByIdAsync(new FlushProbeId(id), TestContext.Current.CancellationToken);
        return probe!;
    }

    private static async Task SendAsync(IHost host, ICommand command)
    {
        using var scope = host.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.SendAsync(command, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }

    private async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
                .UseEfCoreStateStore<FlushProbeContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        builder.Services.AddScoped<ICommandHandler<StartFlushProbe>, StartFlushProbeHandler>();
        builder.Services.AddScoped<ICommandHandler<RenameFlushProbe>, RenameFlushProbeHandler>();

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FlushProbeContext>().Database.ExecuteSqlRawAsync(
            "create table if not exists flush_probe_rows (id uuid primary key, name text not null, version bigint not null)",
            TestContext.Current.CancellationToken);

        return host;
    }
}

