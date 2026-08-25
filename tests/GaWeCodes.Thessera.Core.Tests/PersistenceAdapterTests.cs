using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class PersistenceAdapterTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test;******";

    [Fact]
    public void TheRegistrarLetsTheAdapterRegisterItsOwnServices()
    {
        var adapter = new RecordingAdapter(ConnectionString);
        using var provider = BuildProvider(
            options => options.UsePersistence(adapter),
            out _);

        var wiring = provider.GetRequiredService<IWiringSnapshot>();
        Assert.True(adapter.WasRegistered);
        Assert.NotNull(adapter.SeenServices);
        Assert.True(wiring.PersistenceSelected);
    }

    [Fact]
    public void TheAdapterSeesProvisioningDisabledByDefault()
    {
        var adapter = new RecordingAdapter(ConnectionString);
        using var provider = BuildProvider(
            options => options.UsePersistence(adapter),
            out _);

        Assert.False(adapter.SeenContext!.ProvisionsInfrastructure);
        Assert.False(provider.GetRequiredService<IWiringSnapshot>().ProvisionsInfrastructure);
    }

    [Fact]
    public void TheAdapterSeesProvisioningEnabledWhenSelected()
    {
        var adapter = new RecordingAdapter(ConnectionString);
        using var provider = BuildProvider(
            options => options
                .UsePersistence(adapter)
                .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup),
            out _);

        Assert.True(adapter.SeenContext!.ProvisionsInfrastructure);
        Assert.True(provider.GetRequiredService<IWiringSnapshot>().ProvisionsInfrastructure);
    }

    [Fact]
    public void TheRuntimeContributedByTheAdapter_ReachesTheWiring()
    {
        using var provider = BuildProvider(
            options => options.UsePersistence(new RecordingAdapter(ConnectionString) { ContributesRuntime = true }),
            out var runtime);

        Assert.IsType<RecordingActivator>(runtime.Activator);
        Assert.NotNull(provider.GetRequiredService<IWiringSnapshot>());
    }

    [Fact]
    public void TwoAdaptersAskingForTheSameRuntime_ShareOneActivator()
    {
        var runtime = new RuntimeActivation();

        var first = runtime.GetOrAdd(static () => new RecordingActivator());
        var second = runtime.GetOrAdd(static () => new RecordingActivator());

        Assert.Same(first, second);
    }

    [Fact]
    public void TwoDifferentRuntimes_FailWithAnExplanation()
    {
        var runtime = new RuntimeActivation();
        runtime.GetOrAdd(static () => new RecordingActivator());

        var thrown = Assert.Throws<InvalidOperationException>(
            () => runtime.GetOrAdd(static () => new OtherActivator()));

        Assert.Contains("exactly one messaging runtime", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTransientFaultDecision_ComesFromTheChosenAdapter()
    {
        var transient = new TimeoutException();
        var adapter = new RecordingAdapter(ConnectionString) { TransientFault = transient };
        using var provider = BuildProvider(
            options => options.UsePersistence(adapter),
            out _);
        var wiring = provider.GetRequiredService<IWiringSnapshot>();

        Assert.True(wiring.IsTransientFault(transient));
        Assert.False(wiring.IsTransientFault(new InvalidOperationException()));
    }

    private static ServiceProvider BuildProvider(
        Action<ThesseraOptions> configure,
        out RuntimeActivation runtime)
    {
        RuntimeActivation? capturedRuntime = null;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            capturedRuntime = options.Runtime;
            configure(options);
        });

        runtime = capturedRuntime ?? throw new InvalidOperationException("Thessera runtime was not captured.");
        return services.BuildServiceProvider();
    }

    private sealed record RecordingAdapter(string WriteConnectionString) : IPersistenceAdapter
    {
        public string Description => "UseRecordingPersistence";

        public AggregateStyle AggregateStyle => AggregateStyle.StateStored;

        public bool ContributesRuntime { get; init; }

        public Exception? TransientFault { get; init; }

        public bool WasRegistered { get; private set; }

        public IServiceCollection? SeenServices { get; private set; }

        public PersistenceRegistrationContext? SeenContext { get; private set; }

        public bool IsTransientFault(Exception exception) =>
            TransientFault is not null && ReferenceEquals(TransientFault, exception);

        public void Register(PersistenceRegistrationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            WasRegistered = true;
            SeenServices = context.Services;
            SeenContext = context;

            if (ContributesRuntime)
            {
                context.UseRuntime(static () => new RecordingActivator());
            }
        }
    }

    private sealed class RecordingActivator : IRuntimeActivator
    {
        public void Activate(IHostApplicationBuilder builder, IWiringSnapshot wiring)
        {
        }
    }

    private sealed class OtherActivator : IRuntimeActivator
    {
        public void Activate(IHostApplicationBuilder builder, IWiringSnapshot wiring)
        {
        }
    }
}
