using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregatePersistenceMatchTests
{
    private const string WriteConnectionString = "Host=localhost;Database=match;Username=test;******";

    [Fact]
    public async Task EventSourcingWithAStateStoredAggregate_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunChecksAsync(
            options => options.UseMartenEventStore(WriteConnectionString),
            services => services.AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>()));

        Assert.Contains("UseMartenEventStore", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Ledger), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("keep no history", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StateStorageWithAnEventSourcedAggregate_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunChecksAsync(
            options => options.UseEfCoreStateStore<MatchDbContext>(WriteConnectionString),
            services => services.AddScoped<ICommandHandler<OpenJournal>, OpenJournalHandler>()));

        Assert.Contains("UseEfCoreStateStore", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Journal), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("record of truth", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("WithoutEventHistory", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StateStorageWithAnEventSourcedAggregate_PassesTheStartWhenTheHistoryIsWaived() =>
        await RunChecksAsync(
            options => options
                .UseEfCoreStateStore<MatchDbContext>(WriteConnectionString)
                .WithoutEventHistory(),
            services => services.AddScoped<ICommandHandler<OpenJournal>, OpenJournalHandler>());

    [Fact]
    public async Task WaivingTheHistoryOnAnEventSourcingStore_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunChecksAsync(
            options => options
                .UseMartenEventStore(WriteConnectionString)
                .WithoutEventHistory(),
            services => services.AddScoped<ICommandHandler<OpenJournal>, OpenJournalHandler>()));

        Assert.Contains("WithoutEventHistory", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("UseMartenEventStore", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaivingTheHistoryWithoutAnyStore_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunChecksAsync(
            options => options.WithoutEventHistory(),
            _ => { }));

        Assert.Contains("WithoutEventHistory", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("no persistence strategy", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventSourcingWithAnEventSourcedAggregate_PassesTheStart() =>
        await RunChecksAsync(
            options => options.UseMartenEventStore(WriteConnectionString),
            services => services.AddScoped<ICommandHandler<OpenJournal>, OpenJournalHandler>());

    [Fact]
    public async Task StateStorageWithAStateStoredAggregate_PassesTheStart() =>
        await RunChecksAsync(
            options => options.UseEfCoreStateStore<MatchDbContext>(WriteConnectionString),
            services => services.AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>());

    [Fact]
    public async Task AnAggregateNoHandlerAsksFor_IsNotJudged() =>
        await RunChecksAsync(options => options.UseMartenEventStore(WriteConnectionString), _ => { });

    [Fact]
    public async Task AMixedHost_WithEachAggregateOnItsMatchingStore_PassesTheStart() =>
        await RunChecksAsync(
            options => options
                .UseEfCoreStateStore<MatchDbContext>(WriteConnectionString)
                .UseMartenEventStore(WriteConnectionString, typeof(Journal)),
            services => services
                .AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>()
                .AddScoped<ICommandHandler<OpenJournal>, OpenJournalHandler>());

    [Fact]
    public async Task AMixedHost_WithTheStateStoredAggregateClaimedByTheAncillaryEventStore_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunChecksAsync(
            options => options
                .UseEfCoreStateStore<MatchDbContext>(WriteConnectionString)
                .UseMartenEventStore(WriteConnectionString, typeof(Ledger)),
            services => services.AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>()));

        Assert.Contains("UseMartenEventStore", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Ledger), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("keep no history", thrown.Message, StringComparison.Ordinal);
    }

    private static async Task RunChecksAsync(
        Action<ThesseraOptions> configure,
        Action<IServiceCollection> register)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(DepositRecorded).Assembly);
            configure(options);
        });

        register(builder.Services);

        using var provider = builder.Services.BuildServiceProvider();

        foreach (var check in provider.GetServices<IStartupCheck>().OfType<SynchronousStartupCheck>())
        {
            if (check.GetType().Name.StartsWith("AggregatePersistenceMatch", StringComparison.Ordinal))
            {
                await check.RunAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class MatchDbContext(DbContextOptions<MatchDbContext> options) : DbContext(options);
}

public readonly record struct LedgerId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("match-deposit-recorded-v1")]
public sealed record DepositRecorded(LedgerId LedgerId) : DomainEvent;

public sealed record LedgerState(LedgerId Id) : AggregateState<LedgerState, LedgerId>
{
    public static LedgerState Empty => new(new LedgerId(Guid.Empty));

    public override LedgerState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        DepositRecorded recorded => this with { Id = recorded.LedgerId },
        _ => this,
    };
}

[AggregateName("match-ledger")]
public sealed class Ledger : AggregateRoot<LedgerId, LedgerState>
{
    private Ledger() : base(LedgerState.Empty)
    {
    }
}

public sealed record RecordDeposit(Guid Id) : ICommand;

public sealed class RecordDepositHandler(IRepository<Ledger, LedgerId> repository)
    : ICommandHandler<RecordDeposit>
{
    public Task<Result> HandleAsync(RecordDeposit command, CancellationToken cancellationToken) =>
        repository is null ? throw new InvalidOperationException() : Task.FromResult(Result.Success());
}

public readonly record struct JournalId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("match-journal-opened-v1")]
public sealed record JournalOpened(JournalId JournalId) : DomainEvent;

public sealed record JournalState(JournalId Id) : AggregateState<JournalState, JournalId>
{
    public static JournalState Empty => new(new JournalId(Guid.Empty));

    public override JournalState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        JournalOpened opened => this with { Id = opened.JournalId },
        _ => this,
    };
}

[AggregateName("match-journal")]
public sealed class Journal : EventSourcedAggregateRoot<JournalId, JournalState>
{
    private Journal() : base(JournalState.Empty)
    {
    }
}

public sealed record OpenJournal(Guid Id) : ICommand;

public sealed class OpenJournalHandler(IRepository<Journal, JournalId> repository)
    : ICommandHandler<OpenJournal>
{
    public Task<Result> HandleAsync(OpenJournal command, CancellationToken cancellationToken) =>
        repository is null ? throw new InvalidOperationException() : Task.FromResult(Result.Success());
}
