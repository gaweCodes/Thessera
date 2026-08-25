using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Validation;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using ValidHandlersFixture;

namespace GaWeCodes.Thessera.Tests;

public sealed class UnitOfWorkPresenceCheckTests
{
    [Fact]
    public async Task NoPersistenceAndScannedCommands_FailsNamingTheCommands()
    {
        using var provider = BuildProvider(options => options.AddHandlersFrom(typeof(RegistrationCommand).Assembly));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Check(provider).RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains(nameof(RegistrationCommand), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ThesseraOptions.UseNoPersistence), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoPersistenceAndNoScannedCommands_Passes()
    {
        using var provider = BuildProvider(_ => { });

        await Check(provider).RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostRegisteredUnitOfWork_Passes()
    {
        using var provider = BuildProvider(
            options => options.AddHandlersFrom(typeof(RegistrationCommand).Assembly),
            services => services.AddScoped<IUnitOfWork, ProbeUnitOfWork>());

        await Check(provider).RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnitOfWorkRegisteredAfterThessera_Passes()
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddThessera(options => options.AddHandlersFrom(typeof(RegistrationCommand).Assembly));
        services.AddScoped<IUnitOfWork, ProbeUnitOfWork>();

        using var provider = services.BuildServiceProvider();

        await Check(provider).RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UseNoPersistence_PassesAndLogsTheDeliberateChoice()
    {
        using var provider = BuildProvider(options => options
            .AddHandlersFrom(typeof(RegistrationCommand).Assembly)
            .UseNoPersistence());

        await Check(provider).RunAsync(TestContext.Current.CancellationToken);

        Assert.Contains(
            provider.GetRequiredService<FakeLogCollector>().GetSnapshot(),
            record => record.Level == LogLevel.Information
                && record.Message.Contains("UseNoPersistence", StringComparison.Ordinal));
    }

    [Fact]
    public void UseNoPersistence_CombinedWithAPersistenceStrategy_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddThessera(options => options
            .UseNoPersistence()
            .UseMartenEventStore("Host=localhost;Database=probe;Username=test;Password=test")));

        Assert.Contains("UseNoPersistence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UseNoPersistence_RegistersNoMessageStoreAndNeedsNoDomainEvents()
    {
        using var provider = BuildProvider(options => options.UseNoPersistence());

        var wiring = provider.GetRequiredService<IWiringSnapshot>();

        Assert.False(wiring.PersistenceSelected);
        Assert.False(wiring.RequiresRuntime);
    }

    private static IStartupCheck Check(ServiceProvider provider) =>
        Assert.Single(
            provider.GetServices<IStartupCheck>(),
            check => check.GetType().Name == "UnitOfWorkPresenceCheck");

    private static ServiceProvider BuildProvider(
        Action<ThesseraOptions> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        configureServices?.Invoke(services);
        services.AddThessera(configure);
        return services.BuildServiceProvider();
    }

    private sealed class ProbeUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
