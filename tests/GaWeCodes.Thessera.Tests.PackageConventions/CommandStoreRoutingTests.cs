using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class CommandStoreRoutingTests
{
    private const string WriteConnectionString = "Host=localhost;Database=routing;Username=test;******";

    [Fact]
    public async Task AHostWithOneStore_NeverRunsTheRoutingWalk()
    {
        await RunChecksAsync(
            options => options.UseEfCoreStateStore<RoutingDbContext>(WriteConnectionString),
            services => services
                .AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>()
                .AddScoped<ICommandHandler<OpenJournal>, CrossStoreJournalHandler>());
    }

    [Fact]
    public async Task AMixedHost_WithOneAggregatePerHandler_PassesTheStart() =>
        await RunChecksAsync(
            options => options
                .UseEfCoreStateStore<RoutingDbContext>(WriteConnectionString)
                .UseMartenEventStore(WriteConnectionString, typeof(Journal)),
            services => services
                .AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>()
                .AddScoped<ICommandHandler<OpenJournal>, OpenJournalHandler>());

    [Fact]
    public async Task AMixedHost_WithAHandlerSpanningBothStores_FailsTheStartWithTheReason()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunChecksAsync(
            options => options
                .UseEfCoreStateStore<RoutingDbContext>(WriteConnectionString)
                .UseMartenEventStore(WriteConnectionString, typeof(Journal)),
            services => services
                .AddScoped<ICommandHandler<RecordDeposit>, RecordDepositHandler>()
                .AddScoped<ICommandHandler<OpenJournal>, CrossStoreJournalHandler>()));

        Assert.Contains(nameof(CrossStoreJournalHandler), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2 different", thrown.Message, StringComparison.Ordinal);
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
            if (check.GetType().Name.StartsWith("CommandStoreRouting", StringComparison.Ordinal))
            {
                await check.RunAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class RoutingDbContext(DbContextOptions<RoutingDbContext> options) : DbContext(options);

    private sealed class CrossStoreJournalHandler(
        IRepository<Ledger, LedgerId> ledgers,
        IRepository<Journal, JournalId> journals) : ICommandHandler<OpenJournal>
    {
        public Task<Result> HandleAsync(OpenJournal command, CancellationToken cancellationToken) =>
            ledgers is null || journals is null ? throw new InvalidOperationException() : Task.FromResult(Result.Success());
    }
}
