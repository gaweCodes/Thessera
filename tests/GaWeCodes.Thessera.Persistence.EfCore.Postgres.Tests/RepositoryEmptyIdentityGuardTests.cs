using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class RepositoryEmptyIdentityGuardTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task AddAsync_WithEmptyIdentity_Throws()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var emptyHull = AggregateFactory.CreateEmpty<Counter>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddAsync(emptyHull, TestContext.Current.CancellationToken));

        Assert.Contains("has no identity", exception.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddAsync_WithIdentity_DoesNotThrow()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Counter, CounterId>>();
        var counter = Counter.Create(new CounterId(Guid.NewGuid()));

        await repository.AddAsync(counter, TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(CounterCreated).Assembly)
            .UseEfCoreStateStore<GuardProbeContext>(fixture.ConnectionString));

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private sealed class GuardProbeContext(DbContextOptions<GuardProbeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CounterState>(entity =>
            {
                entity.HasKey(state => state.Id);
                entity.Property(state => state.Id)
                    .HasColumnName("id")
                    .HasConversion(id => id.Value, value => new CounterId(value));
                entity.Property(state => state.Total).HasColumnName("total");
                entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();
            });
        }
    }
}
