using GaWeCodes.Thessera.Core.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class WriteDbContextLifetimeCheckTests
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=write_context_lifetime;Username=none;Password=none";

    [Fact]
    public async Task ATransientWriteContext_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunCheckAsync(ServiceLifetime.Transient));

        Assert.Contains(nameof(FlushProbeContext), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Transient", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("writes no row at all", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASingletonWriteContext_FailsTheStartWithItsOwnReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunCheckAsync(ServiceLifetime.Singleton));

        Assert.Contains("Singleton", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("change tracker", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScopedWriteContext_PassesTheStart() => await RunCheckAsync(ServiceLifetime.Scoped);

    [Fact]
    public async Task AWriteContextLeftToUseEfCoreStateStore_PassesTheStart() => await RunCheckAsync(null);

    private static async Task RunCheckAsync(ServiceLifetime? preRegisteredLifetime)
    {
        var builder = Host.CreateApplicationBuilder();

        if (preRegisteredLifetime is { } lifetime)
        {
            builder.Services.AddDbContext<FlushProbeContext>(
                options => options.UseNpgsql(UnusedConnectionString),
                lifetime);
        }

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
            .UseEfCoreStateStore<FlushProbeContext>(UnusedConnectionString));

        using var host = builder.Build();

        var check = host.Services.GetServices<IStartupCheck>()
            .Single(candidate =>
                candidate.GetType().Name == "WriteDbContextLifetimeCheck`1"
                && candidate.GetType().GenericTypeArguments.SingleOrDefault() == typeof(FlushProbeContext));

        await check.RunAsync(TestContext.Current.CancellationToken);
    }
}
