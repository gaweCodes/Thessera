using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Wolverine.Runtime;

namespace GaWeCodes.Thessera.Tests;

public sealed class WolverineWiringStartupValidationTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test;******";

    [Fact]
    public async Task PersistenceSelected_WithoutWolverine_FailsAtStartupNamingUseWolverine()
    {
        using var provider = BuildProvider(options =>
            options.UseEfCoreStateStore<TestDbContext>(ConnectionString));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetValidator(provider).RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("UseWolverine", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersistenceSelected_WithWolverineRuntime_Passes()
    {
        using var provider = BuildProvider(
            options => options.UseEfCoreStateStore<TestDbContext>(ConnectionString),
            services => services.AddSingleton(Substitute.For<IWolverineRuntime>()));

        await GetValidator(provider).RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void NoWolverineCapabilitySelected_TheCheckIsNotRegisteredBecauseNothingCanNeedARuntime()
    {
        using var provider = BuildProvider(_ => { });

        Assert.DoesNotContain(
            provider.GetServices<IStartupCheck>(),
            check => check.GetType().Name == "WolverineRuntimeCheck");
    }

    [Fact]
    public void ACapabilityNeedingWolverine_RegistersTheCheck()
    {
        using var provider = BuildProvider(options =>
            options.UseEfCoreStateStore<TestDbContext>(ConnectionString));

        Assert.Single(provider.GetServices<IStartupCheck>(), check => check.GetType().Name == "WolverineRuntimeCheck");
    }

    private static ServiceProvider BuildProvider(
        Action<ThesseraOptions> configure,
        Action<IServiceCollection>? registerExtras = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        registerExtras?.Invoke(services);
        services.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            configure(options);
        });
        return services.BuildServiceProvider();
    }

    private static IStartupCheck GetValidator(ServiceProvider provider) =>
        Assert.Single(provider.GetServices<IStartupCheck>(), check => check.GetType().Name == "WolverineRuntimeCheck");

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
