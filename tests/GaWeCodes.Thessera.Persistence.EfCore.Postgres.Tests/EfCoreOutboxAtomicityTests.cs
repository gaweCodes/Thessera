using System.Collections.Concurrent;
using System.Data.Common;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EfCoreOutboxAtomicityTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task WhenTheOutboxWriteFails_TheAggregateWriteIsRolledBackToo()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var interceptor = new FailingOutboxWriteInterceptor();

        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
                .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .UseEfCoreStateStore<FlushProbeContext>(
                fixture.ConnectionString,
                context => context.AddInterceptors(interceptor))
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        builder.Services.AddScoped<ICommandHandler<StartFlushProbe>, StartFlushProbeHandler>();

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FlushProbeContext>().Database.ExecuteSqlRawAsync(
                "create table if not exists flush_probe_rows (id uuid primary key, name text not null, version bigint not null)",
                TestContext.Current.CancellationToken);
        }

        var probeId = Guid.NewGuid();
        interceptor.Arm();

        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await Assert.ThrowsAnyAsync<Exception>(
                () => sender.SendAsync(new StartFlushProbe(probeId), TestContext.Current.CancellationToken));
        }

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FlushProbeContext>();
            var persisted = await context.Probes.CountAsync(
                probe => probe.Id == new FlushProbeId(probeId),
                TestContext.Current.CancellationToken);

            Assert.Equal(
                0,
                persisted);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FailingOutboxWriteInterceptor : DbCommandInterceptor
    {
        private bool _armed;

        public void Arm() => _armed = true;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ShouldFail(command)
                ? throw new InvalidOperationException("Simulated outbox write failure for the atomicity test.")
                : base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            ShouldFail(command)
                ? throw new InvalidOperationException("Simulated outbox write failure for the atomicity test.")
                : base.ReaderExecutingAsync(command, eventData, result, cancellationToken);

        private bool ShouldFail(DbCommand command) =>
            _armed && command.CommandText.Contains("wolverine_", StringComparison.OrdinalIgnoreCase);
    }
}

