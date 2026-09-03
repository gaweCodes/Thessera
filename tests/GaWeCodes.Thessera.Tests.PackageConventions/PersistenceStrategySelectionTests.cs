using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class PersistenceStrategySelectionTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public void AddThessera_WithEfCoreOnly_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddThessera(options => WithDomainEvents(options).UseEfCoreStateStore<TestDbContext>(ConnectionString)));

        Assert.Null(exception);
    }

    [Fact]
    public void AddThessera_WithMartenOnly_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddThessera(options => WithDomainEvents(options).UseMartenEventStore(ConnectionString)));

        Assert.Null(exception);
    }

    [Fact]
    public void AddThessera_WithEfCoreSelectedTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)));

        Assert.Null(exception);
    }

    [Fact]
    public void AddThessera_WithEfCoreSelectedTwiceUnderDifferentConnectionStrings_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)
                .UseEfCoreStateStore<TestDbContext>("Host=elsewhere;Database=other;Username=test;******")));

        Assert.Contains("different databases", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddThessera_WithMartenSelectedTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseMartenEventStore(ConnectionString)
                .UseMartenEventStore(ConnectionString)));

        Assert.Null(exception);
    }

    [Fact]
    public void AddThessera_WithEfCoreThenMarten_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => options
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)
                .UseMartenEventStore(ConnectionString)));

        Assert.Contains("persistence strateg", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UseEfCoreStateStore", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UseMartenEventStore", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddThessera_WithMartenThenEfCore_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => options
                .UseMartenEventStore(ConnectionString)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)));
    }

    [Fact]
    public void AddThessera_WithEfCoreMainAndMartenAncillaryForADifferentAggregate_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)
                .UseMartenEventStore(ConnectionString, typeof(FlushProbe))));

        Assert.Null(exception);
    }

    [Fact]
    public void AddThessera_WithTheSameAggregateClaimedByTwoStores_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString, forAggregates: typeof(FlushProbe))
                .UseMartenEventStore("Host=elsewhere;Database=other;Username=test;******", typeof(FlushProbe))));

        Assert.Contains("is claimed by both", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(FlushProbe), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddThessera_WithTwoAncillaryStoresForDifferentAggregatesAndNoMainStore_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString, forAggregates: typeof(FlushProbe))
                .UseMartenEventStore("Host=elsewhere;Database=other;Username=test;******", typeof(FlushCounter))));

        Assert.Null(exception);
    }

    [Fact]
    public void AddThessera_WithMartenSelectedTwiceUnderDifferentConnectionStrings_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseMartenEventStore(ConnectionString)
                .UseMartenEventStore("Host=elsewhere;Database=other;Username=test;******")));

        Assert.Contains("different databases", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddThessera_UseNoPersistenceCombinedWithAStrategy_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseMartenEventStore(ConnectionString)
                .UseNoPersistence()));

        Assert.Contains("UseNoPersistence", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UseMartenEventStore", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddThessera_WithoutAnyPersistenceChoice_IsNotSelectedAndDoesNotRequireRuntime()
    {
        var services = new ServiceCollection();

        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var wiring = provider.GetRequiredService<IWiringSnapshot>();

        Assert.False(wiring.PersistenceSelected);
        Assert.False(wiring.RequiresRuntime);
    }

    [Fact]
    public void AddThessera_WithADeliberateNoPersistenceChoice_IsNotSelectedAndDoesNotRequireRuntime()
    {
        var services = new ServiceCollection();

        services.AddThessera(options => WithDomainEvents(options).UseNoPersistence());

        using var provider = services.BuildServiceProvider();
        var wiring = provider.GetRequiredService<IWiringSnapshot>();

        Assert.False(wiring.PersistenceSelected);
        Assert.False(wiring.RequiresRuntime);
    }

    [Fact]
    public void AddThessera_WithAForeignPersistenceAdapter_IsSelectedAndRequiresRuntime()
    {
        var services = new ServiceCollection();

        services.AddThessera(options => WithDomainEvents(options).UsePersistence(new ForeignAdapter(ConnectionString)));

        using var provider = services.BuildServiceProvider();
        var wiring = provider.GetRequiredService<IWiringSnapshot>();

        Assert.True(wiring.PersistenceSelected);
        Assert.True(wiring.RequiresRuntime);
    }

    [Fact]
    public void AddThessera_WithAForeignPersistenceAdapterAlongsideEfCore_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => WithDomainEvents(options)
                .UseEfCoreStateStore<TestDbContext>(ConnectionString)
                .UsePersistence(new ForeignAdapter(ConnectionString))));

        Assert.Contains("persistence strateg", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UseForeignPersistence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddThessera_CombinesSelectionsFromSeparateSatellitePackages()
    {
        var services = new ServiceCollection();

        services.AddThessera(options => WithDomainEvents(options)
            .UseEfCoreStateStore<TestDbContext>(ConnectionString)
            .UseWolverineMessaging(
                new Uri("amqp://localhost:5672"),
                TestMessaging.ExchangeName,
                TestMessaging.ContextName));

        using var provider = services.BuildServiceProvider();
        var wiring = provider.GetRequiredService<IWiringSnapshot>();

        Assert.True(wiring.PersistenceSelected);
        Assert.NotNull(wiring.Transport);
    }

    private static ThesseraOptions WithDomainEvents(ThesseraOptions options) =>
        options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    private sealed record ForeignAdapter(string WriteConnectionString) : IPersistenceAdapter
    {
        public string Description => "UseForeignPersistence";

        public AggregateStyle AggregateStyle => AggregateStyle.StateStored;

        public bool IsTransientFault(Exception exception) => false;

        public void Register(PersistenceRegistrationContext context)
        {
        }
    }
}

