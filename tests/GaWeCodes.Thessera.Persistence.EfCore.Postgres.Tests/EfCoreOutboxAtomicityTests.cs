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
    public async Task AggregateAndOutboxEntry_ArePersistedByOneCommand()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var recorder = new CommandRecorder();

        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
                .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .UseEfCoreStateStore<FlushProbeContext>(
                fixture.ConnectionString,
                context => context.AddInterceptors(recorder))
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

        recorder.Clear();

        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(
                new StartFlushProbe(Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        var combined = recorder.Commands
            .Where(sql => sql.Contains("flush_probe_rows", StringComparison.OrdinalIgnoreCase))
            .Where(sql => sql.Contains("wolverine", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            combined.Count == 1,
            "The aggregate row and the outbox envelope must be written by a single command so they share one " +
            $"transaction. Commands touching both: {combined.Count}. All recorded commands:{Environment.NewLine}" +
            string.Join(Environment.NewLine + "---" + Environment.NewLine, recorder.Commands));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();

        public IReadOnlyList<string> Commands => [.. _commands];

        public void Clear() => _commands.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Record(DbCommand command) => _commands.Enqueue(command.CommandText);
    }
}

