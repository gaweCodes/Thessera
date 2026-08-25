using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Persistence.EfCore;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class SameAggregateOverBothStoresTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task TheAggregateBehavesIdenticallyWhicheverStoreIsWiredUp()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var overState = await RunScenarioAsync(StateStoredHostAsync);
        var overEvents = await RunScenarioAsync(EventSourcedHostAsync);

        Assert.Equal(overState, overEvents);
    }

    [Fact]
    public async Task TheStateStoredHostKeepsNoStreamWhileTheEventSourcedHostDoes()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        Assert.Equal(0, await StoredEventCountAsync(StateStoredHostAsync));
        Assert.Equal(3, await StoredEventCountAsync(EventSourcedHostAsync));
    }

    private static async Task<ThermostatSnapshot> RunScenarioAsync(Func<Task<IHost>> startHost)
    {
        using var host = await startHost();
        var id = new ThermostatId(Guid.NewGuid());

        using (var write = host.Services.CreateScope())
        {
            var repository = Repository(write);
            var thermostat = Thermostat.Install(id, 18);
            thermostat.SetTarget(21);

            await repository.AddAsync(thermostat, TestContext.Current.CancellationToken);
            await Commit(write);
        }

        using (var change = host.Services.CreateScope())
        {
            var thermostat = await Repository(change).GetByIdAsync(id, TestContext.Current.CancellationToken);
            thermostat!.SetTarget(23);

            await Commit(change);
        }

        var rejection = Record.Exception(() => Thermostat.Install(new ThermostatId(Guid.NewGuid()), 4));

        using var read = host.Services.CreateScope();
        var reloaded = await Repository(read).GetByIdAsync(id, TestContext.Current.CancellationToken);
        var missing = await Repository(read).GetByIdAsync(
            new ThermostatId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);

        return new ThermostatSnapshot(
            reloaded!.Target,
            ((IStateOwner)reloaded).Version,
            reloaded.DomainEvents.Count,
            missing is null,
            (rejection as DomainValidationException)?.Violations[0].Code);
    }

    private static async Task<long> StoredEventCountAsync(Func<Task<IHost>> startHost)
    {
        using var host = await startHost();
        var id = new ThermostatId(Guid.NewGuid());

        using (var write = host.Services.CreateScope())
        {
            var thermostat = Thermostat.Install(id, 18);
            thermostat.SetTarget(21);
            thermostat.SetTarget(23);

            await Repository(write).AddAsync(thermostat, TestContext.Current.CancellationToken);
            await Commit(write);
        }

        var streamKey = EntityKeyFormatter.GetStreamKey("thermostat", id.Value.ToString());
        var counted = await host.Services.GetRequiredService<IStoredEventCounter>()
            .CountAsync(streamKey, TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
        return counted;
    }

    private static IRepository<Thermostat, ThermostatId> Repository(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IRepository<Thermostat, ThermostatId>>();

    private static Task Commit(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);

    private async Task<IHost> StateStoredHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(ThermostatInstalled).Assembly)
                .UseEfCoreStateStore<ThermostatContext>(fixture.ConnectionString)
                    .WithoutEventHistory()
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(Solo));

        builder.Services.AddSingleton<IStoredEventCounter, NoStreamCounter>();

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ThermostatContext>().Database.ExecuteSqlRawAsync(
            "create table if not exists thermostat_rows (id uuid primary key, target int not null, version bigint not null)",
            TestContext.Current.CancellationToken);

        return host;
    }

    private async Task<IHost> EventSourcedHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(ThermostatInstalled).Assembly)
                .UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(Solo));

        builder.Services.AddSingleton<IStoredEventCounter, MartenStreamCounter>();

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static void Solo(WolverineOptions options)
    {
        options.Durability.Mode = DurabilityMode.Solo;
        options.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
    }
}

public sealed record ThermostatSnapshot(
    int Target,
    long Version,
    int PendingEvents,
    bool MissingReadsAsNull,
    string? RejectedCode);

public interface IStoredEventCounter
{
    Task<long> CountAsync(string streamKey, CancellationToken cancellationToken);
}

internal sealed class NoStreamCounter : IStoredEventCounter
{
    public Task<long> CountAsync(string streamKey, CancellationToken cancellationToken) => Task.FromResult(0L);
}

internal sealed class MartenStreamCounter(Marten.IDocumentStore store) : IStoredEventCounter
{
    public async Task<long> CountAsync(string streamKey, CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();
        var stream = await session.Events.FetchStreamAsync(streamKey, token: cancellationToken)
            .ConfigureAwait(false);

        return stream.Count;
    }
}

public readonly record struct ThermostatId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("thermostat-installed-v1")]
public sealed record ThermostatInstalled(ThermostatId ThermostatId, int Target) : DomainEvent;

[EventName("thermostat-target-set-v1")]
public sealed record ThermostatTargetSet(ThermostatId ThermostatId, int Target) : DomainEvent;

public sealed record TargetMustBeHabitable(int Degrees) : IDomainValidationRule
{
    public string Code => "thermostat.target.out-of-range";

    public string? Target => nameof(Degrees);

    public string Message => "A thermostat target must be between 5 and 30 degrees.";

    public bool IsInvalid() => Degrees is < 5 or > 30;
}

public sealed record ThermostatState(ThermostatId Id, int Target)
    : AggregateState<ThermostatState, ThermostatId>
{
    public static ThermostatState Empty => new(default, 0);

    public override ThermostatState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ThermostatInstalled installed => this with { Id = installed.ThermostatId, Target = installed.Target },
        ThermostatTargetSet set => this with { Target = set.Target },
        _ => this,
    };
}

[AggregateName("thermostat")]
public sealed class Thermostat : EventSourcedAggregateRoot<ThermostatId, ThermostatState>
{
    private Thermostat() : base(ThermostatState.Empty)
    {
    }

    public int Target => State.Target;

    public static Thermostat Install(ThermostatId id, int target)
    {
        RuleChecker.CheckValidationRule(new TargetMustBeHabitable(target));

        var thermostat = new Thermostat();
        thermostat.RaiseEvent(new ThermostatInstalled(id, target));
        return thermostat;
    }

    public void SetTarget(int target)
    {
        RuleChecker.CheckValidationRule(new TargetMustBeHabitable(target));

        RaiseEvent(new ThermostatTargetSet(Id, target));
    }
}

public sealed class ThermostatContext(DbContextOptions<ThermostatContext> options) : DbContext(options)
{
    public DbSet<ThermostatState> Thermostats => Set<ThermostatState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ThermostatState>(entity =>
        {
            entity.ToTable("thermostat_rows");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Target).HasColumnName("target");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
