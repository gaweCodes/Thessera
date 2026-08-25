using AmbiguousRequestsFixture;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using OrphanRequestsFixture;
using ValidHandlersFixture;

namespace GaWeCodes.Thessera.Tests;

public sealed class HandlerStartupValidationTests
{
    [Fact]
    public async Task StartupValidation_AllHandlersRegistered_Passes()
    {
        using var provider = BuildProvider(options =>
            options.AddHandlersFrom(typeof(RegistrationCommand).Assembly));

        await GetValidator(provider).RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartupValidation_CommandAndQueryWithoutHandlers_FailsNamingEveryRequestType()
    {
        using var provider = BuildProvider(options =>
            options.AddHandlersFrom(typeof(OrphanCommand).Assembly));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetValidator(provider).RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains(nameof(OrphanCommand), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(OrphanQuery), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupValidation_RequestTypeWithMultipleResultContracts_FailsNamingTypeAndContracts()
    {
        using var provider = BuildProvider(options =>
            options.AddHandlersFrom(typeof(AmbiguousQuery).Assembly));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetValidator(provider).RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains(nameof(AmbiguousQuery), exception.Message, StringComparison.Ordinal);
        Assert.Contains("IQuery<Int32>", exception.Message, StringComparison.Ordinal);
        Assert.Contains("IQuery<String>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupValidation_IsRegisteredByDefault()
    {
        using var provider = BuildProvider(_ => { });

        Assert.Single(provider.GetServices<IStartupCheck>(), check => check.GetType().Name == "HandlerRegistrationCheck");
    }

    [Fact]
    public void StartupValidation_CannotBeTurnedOff()
    {
        var switches = typeof(ThesseraOptions)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => name.Contains("Validate", StringComparison.Ordinal));

        Assert.Empty(switches);
    }

    private static ServiceProvider BuildProvider(Action<ThesseraOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(configure);
        return services.BuildServiceProvider();
    }

    private static IStartupCheck GetValidator(ServiceProvider provider) =>
        Assert.Single(provider.GetServices<IStartupCheck>(), check => check.GetType().Name == "HandlerRegistrationCheck");
}
