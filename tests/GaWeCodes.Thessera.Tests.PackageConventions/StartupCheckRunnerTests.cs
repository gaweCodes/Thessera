using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Persistence.Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class StartupCheckRunnerTests
{
    [Fact]
    public async Task ABeforeCheck_RunsBeforeTheStartedPhaseCompletes()
    {
        var observed = new List<string>();
        var check = new CallbackCheck(StartupPhase.BeforeHostedServicesStart, () => observed.Add("check"));

        using var host = BuildHost(services =>
        {
            services.AddSingleton<IStartupCheck>(check);
            services.AddHostedService(_ => new MarkerLifecycleService(() => observed.Add("started-phase")));
        });

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["check", "started-phase"], observed);
    }

    [Fact]
    public async Task AnAfterCheck_RunsExactlyOnce()
    {
        var check = new RecordingCheck(StartupPhase.AfterHostedServicesStarted);

        using var host = BuildHost(services => services.AddSingleton<IStartupCheck>(check));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, check.Runs);
    }

    [Fact]
    public async Task AFailingCheck_StopsTheStartupImmediately()
    {
        var first = new ThrowingCheck();
        var second = new RecordingCheck(StartupPhase.BeforeHostedServicesStart);

        using var host = BuildHost(services =>
        {
            services.AddSingleton<IStartupCheck>(first);
            services.AddSingleton<IStartupCheck>(second);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, second.Runs);
    }

    [Fact]
    public async Task TheAfterPhase_RunsAfterTheStartOfAServiceRegisteredLater()
    {
        var observed = new List<string>();

        using var host = BuildHost(services =>
        {
            services.AddSingleton<IStartupCheck>(
                new CallbackCheck(StartupPhase.AfterHostedServicesStarted, () => observed.Add("check")));
            services.AddHostedService(_ => new CallbackHostedService(() => observed.Add("late-service")));
        });

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["late-service", "check"], observed);
    }

    private static readonly string[] ContributedByAPersistenceAdapter =
    [
        "AggregateStateModelCheck`1",
        "MartenSchemaProvisioner",
    ];

    [Fact]
    public void EveryStartupCheckInTheAssembly_IsReachableThroughTheRunner()
    {
        var implementations = typeof(ThesseraOptions).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IStartupCheck).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(_ => { });
        using var provider = services.BuildServiceProvider();

        var registered = provider.GetServices<IStartupCheck>()
            .Select(check => check.GetType().Name)
            .ToHashSet(StringComparer.Ordinal);

        var unregistered = implementations
            .Except(registered, StringComparer.Ordinal)
            .Except(ContributedByAPersistenceAdapter, StringComparer.Ordinal);

        Assert.Empty(unregistered);
    }

    [Fact]
    public void AChoosingPersistenceAdapter_ContributesItsOwnStartupCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            options.UseMartenEventStore("Host=localhost;Database=test;Username=test;******");
        });

        using var provider = services.BuildServiceProvider();

        Assert.Contains(
            provider.GetServices<IStartupCheck>(),
            check => check.GetType().Name == "MartenSchemaProvisioner");
    }

    private static IHost BuildHost(Action<IServiceCollection> configureServices) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                configureServices(services);
                services.AddThessera(options => options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly));
            })
            .Build();

    private sealed class RecordingCheck(StartupPhase phase) : SynchronousStartupCheck
    {
        public int Runs { get; private set; }

        public override StartupPhase Phase => phase;

        protected override void Run() => Runs++;
    }

    private sealed class CallbackCheck(StartupPhase phase, Action onRun) : SynchronousStartupCheck
    {
        public override StartupPhase Phase => phase;

        protected override void Run() => onRun();
    }

    private sealed class ThrowingCheck : SynchronousStartupCheck
    {
        public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

        protected override void Run() => throw new InvalidOperationException("boom");
    }

    private sealed class CallbackHostedService(Action onStart) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            onStart();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MarkerLifecycleService(Action onStarted) : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken)
        {
            onStarted();
            return Task.CompletedTask;
        }

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
