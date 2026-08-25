using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class OutboxFlushOnCommitTests(PostgreSqlFixture fixture)
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task MartenCommit_FlushesOutboxToProjectionWithoutDurabilityAgent()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(FlushCounterCreated).Assembly);
                    options.UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                });
                services.AddScoped<ICommandHandler<CreateFlushCounter>, CreateFlushCounterHandler>();
                services.AddScoped<IProjectionHandler<FlushCounterCreated>, FlushCounterProjection>();
                services.AddSingleton<FlushDeliverySignal>();
            })
            .UseWolverine(ConfigureFlushOnlyDurability)
            .StartAsync(TestContext.Current.CancellationToken);

        var id = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(new CreateFlushCounter(id), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        var (deliveredEvent, metadata) = await host.Services.GetRequiredService<FlushDeliverySignal>()
            .Delivered.WaitAsync(DeliveryTimeout, TestContext.Current.CancellationToken);
        var created = Assert.IsType<FlushCounterCreated>(deliveredEvent);
        Assert.Equal(id, created.CounterId.Value);

        AssertWatermarkMetadata(metadata, "flush-counter", id);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EfCoreCommit_FlushesOutboxToProjectionWithoutDurabilityAgent()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options =>
            {
                options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
                options.UseEfCoreStateStore<FlushProbeContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                options.CustomizeWolverine(ConfigureFlushOnlyDurability);
            });

        builder.Services.AddScoped<ICommandHandler<StartFlushProbe>, StartFlushProbeHandler>();
        builder.Services.AddScoped<IProjectionHandler<FlushProbeStarted>, FlushProbeProjection>();
        builder.Services.AddSingleton<FlushDeliverySignal>();

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var id = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FlushProbeContext>();
            await context.Database.ExecuteSqlRawAsync(
                "create table if not exists flush_probe_rows (id uuid primary key, name text not null, version bigint not null)",
                TestContext.Current.CancellationToken);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(new StartFlushProbe(id), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        var (deliveredEvent, metadata) = await host.Services.GetRequiredService<FlushDeliverySignal>()
            .Delivered.WaitAsync(DeliveryTimeout, TestContext.Current.CancellationToken);
        var started = Assert.IsType<FlushProbeStarted>(deliveredEvent);
        Assert.Equal(id, started.ProbeId.Value);

        AssertWatermarkMetadata(metadata, "flush-probe", id);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertWatermarkMetadata(DomainEventMetadata metadata, string aggregateName, Guid aggregateId)
    {
        Assert.Equal(aggregateName, metadata.AggregateName);
        Assert.Equal(aggregateId.ToString(), metadata.AggregateId);
        Assert.Equal(1, metadata.Version);
        Assert.NotEqual(Guid.Empty, metadata.EventId);
        Assert.NotEqual(default, metadata.OccurredAt);
    }

    private static void ConfigureFlushOnlyDurability(WolverineOptions options)
    {
        options.Durability.Mode = DurabilityMode.Solo;

        options.Durability.ScheduledJobFirstExecution = TimeSpan.FromHours(1);
        options.Durability.ScheduledJobPollingTime = TimeSpan.FromHours(1);

        options.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
    }
}

