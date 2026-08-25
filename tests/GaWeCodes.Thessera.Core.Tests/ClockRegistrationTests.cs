using GaWeCodes.Thessera.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace GaWeCodes.Thessera.Tests;

public sealed class ClockRegistrationTests
{
    private static readonly DateTimeOffset FixedInstant = new(2026, 7, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddThessera_RegistersAClock()
    {
        using var provider = new ServiceCollection()
            .AddThessera(_ => { })
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<IClock>());
    }

    [Fact]
    public void AddThessera_RegistersAClockBackedByTheRegisteredTimeProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(FixedInstant));

        using var provider = services
            .AddThessera(_ => { })
            .BuildServiceProvider();

        Assert.Equal(FixedInstant, provider.GetRequiredService<IClock>().Now);
    }

    [Fact]
    public void AddThessera_WithAnAlreadyRegisteredClock_KeepsThatClock()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new StoppedClock(FixedInstant));

        using var provider = services
            .AddThessera(_ => { })
            .BuildServiceProvider();

        Assert.IsType<StoppedClock>(provider.GetRequiredService<IClock>());
    }

    private sealed class StoppedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }
}
