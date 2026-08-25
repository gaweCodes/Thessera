using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class DeadLetterVisibilityTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    private const string CheckName = "thessera-dead-letters";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task AProjectionThatKeepsFailing_TurnsTheHealthCheckDegraded()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var signal = new IsolationSignal();
        using var host = await StartHostAsync(signal, projectionThrows: true);

        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.SendAsync(
                new StartIsolationProbe(Guid.NewGuid(), Reject: false),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        await signal.Attempted.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var entry = await WaitForStatusAsync(host, HealthStatus.Degraded);

        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Contains("read model", entry.Description, StringComparison.Ordinal);
        Assert.True(
            Convert.ToInt64(entry.Data["count"], System.Globalization.CultureInfo.InvariantCulture) > 0,
            "The degraded reading must carry the number of dead-lettered messages, otherwise the check tells an "
            + "operator that something is wrong without saying how much.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AHostWhoseProjectionsSucceed_StaysHealthy()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var signal = new IsolationSignal();
        using var host = await StartHostAsync(signal, projectionThrows: false);

        using (var scope = host.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.SendAsync(
                new StartIsolationProbe(Guid.NewGuid(), Reject: false),
                TestContext.Current.CancellationToken);
        }

        await signal.Projected.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == CheckName, TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, report.Entries[CheckName].Status);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheCheckIsTaggedSoItCanBeSeparatedFromReadiness()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var signal = new IsolationSignal();
        using var host = await StartHostAsync(signal, projectionThrows: false);

        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Contains(CheckName, report.Entries.Keys);
        Assert.Contains("dead-letters", report.Entries[CheckName].Tags);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HealthReportEntry> WaitForStatusAsync(IHost host, HealthStatus expected)
    {
        var service = host.Services.GetRequiredService<HealthCheckService>();
        var deadline = DateTimeOffset.UtcNow.Add(Timeout);
        HealthReportEntry entry = default;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var report = await service.CheckHealthAsync(
                registration => registration.Name == CheckName,
                TestContext.Current.CancellationToken);

            entry = report.Entries[CheckName];
            if (entry.Status == expected)
            {
                return entry;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        }

        Assert.Fail(
            $"The check stayed '{entry.Status}' for {Timeout} instead of becoming '{expected}'. "
            + $"Description: {entry.Description}");

        return entry;
    }

    private async Task<IHost> StartHostAsync(IsolationSignal signal, bool projectionThrows)
    {
        var host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddThessera(options =>
                {
                    options.AddDomainEventsFrom(typeof(IsolationProbeStarted).Assembly);
                    options.UseMartenEventStore(postgres.ConnectionString)
                        .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup);
                    options.UseWolverineMessaging(
                        rabbit.ConnectionUri,
                        TestMessaging.UniqueExchangeName("dead-letter-visibility"),
                        TestMessaging.ContextName);
                });

                services.AddSingleton(signal);
                services.AddScoped<ICommandHandler<StartIsolationProbe>, StartIsolationProbeHandler>();
                services.AddScoped<IProjectionHandler<IsolationProbeStarted>>(
                    provider => new IsolationProjection(provider.GetRequiredService<IsolationSignal>(), projectionThrows));
                services.AddScoped<IIntegrationEventMapper<IsolationProbeStarted>>(
                    provider => new IsolationMapper(provider.GetRequiredService<IsolationSignal>(), throws: false));
            })
            .UseWolverine(options => options.Durability.Mode = DurabilityMode.Solo)
            .StartAsync(TestContext.Current.CancellationToken);

        await ClearDeadLettersAsync();

        return host;
    }

    private async Task ClearDeadLettersAsync()
    {
        var dataSource = NpgsqlDataSource.Create(postgres.ConnectionString);

        await using (dataSource.ConfigureAwait(false))
        {
            var command = dataSource.CreateCommand("delete from wolverine_dead_letters");

            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
        }
    }
}
