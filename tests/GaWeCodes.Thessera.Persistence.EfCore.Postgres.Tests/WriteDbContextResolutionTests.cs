using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class WriteDbContextResolutionTests
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=write_context_resolution;Username=none;Password=none";

    [Fact]
    public async Task Repository_WhenAForeignContextOwnsTheBareDbContextKey_StillWritesToTheWriteContext()
    {
        using var host = BuildHostWithReadContextUnderTheBareKey();
        using var scope = host.Services.CreateScope();

        Assert.IsType<ReadProbeContext>(scope.ServiceProvider.GetRequiredService<DbContext>());

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
        await repository.AddAsync(
            FlushProbe.Create(new FlushProbeId(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        var writeContext = scope.ServiceProvider.GetRequiredService<FlushProbeContext>();
        var readContext = scope.ServiceProvider.GetRequiredService<ReadProbeContext>();

        Assert.Single(writeContext.ChangeTracker.Entries<FlushProbeState>());
        Assert.Empty(readContext.ChangeTracker.Entries<FlushProbeState>());
    }

    [Fact]
    public async Task Repository_ResolutionsWithinOneScope_UseTheSameWriteContext()
    {
        using var host = BuildHostWithReadContextUnderTheBareKey();
        using var scope = host.Services.CreateScope();

        var firstRepository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();
        var secondRepository = scope.ServiceProvider.GetRequiredService<IRepository<FlushProbe, FlushProbeId>>();

        await firstRepository.AddAsync(
            FlushProbe.Create(new FlushProbeId(Guid.NewGuid())),
            TestContext.Current.CancellationToken);
        await secondRepository.AddAsync(
            FlushProbe.Create(new FlushProbeId(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        var writeContext = scope.ServiceProvider.GetRequiredService<FlushProbeContext>();
        var readContext = scope.ServiceProvider.GetRequiredService<ReadProbeContext>();

        Assert.Equal(2, writeContext.ChangeTracker.Entries<FlushProbeState>().Count());
        Assert.Empty(readContext.ChangeTracker.Entries<FlushProbeState>());
    }

    private static IHost BuildHostWithReadContextUnderTheBareKey()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddDbContext<ReadProbeContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        builder.Services.AddScoped<DbContext>(static provider => provider.GetRequiredService<ReadProbeContext>());

        builder.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            options.UseEfCoreStateStore<FlushProbeContext>(UnusedConnectionString);
        });

        return builder.Build();
    }
}

public sealed class ReadProbeContext(DbContextOptions<ReadProbeContext> options) : DbContext(options)
{
    public DbSet<FlushProbeState> Probes => Set<FlushProbeState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<FlushProbeState>(entity =>
        {
            entity.ToTable("flush_probe_rows");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Name).HasColumnName("name");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
