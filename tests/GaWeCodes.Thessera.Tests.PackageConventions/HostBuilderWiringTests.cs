using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Wolverine.EntityFrameworkCore;
using Wolverine.RDBMS;
using Wolverine.Runtime;

namespace GaWeCodes.Thessera.Tests;

public sealed class HostBuilderWiringTests
{
    private const string WriteConnectionString = "Host=localhost;Database=wiring-write;Username=test;Password=test";

    private static readonly Uri RabbitMqUri = new("amqp://guest:guest@localhost:5672");

    [Fact]
    public void EfCoreSelection_PointsWolverinesMessageStoreAtTheSelectedWriteDatabase()
    {
        var builder = BuildHost(options => options.UseEfCoreStateStore<TestDbContext>(WriteConnectionString));

        var settings = Assert.Single(
            builder.Services
                .Select(descriptor => descriptor.ImplementationInstance)
                .OfType<DatabaseSettings>());

        Assert.Equal(WriteConnectionString, settings.ConnectionString);
    }

    [Fact]
    public void EfCoreSelection_AppliesTheEntityFrameworkCoreTransactionalMiddleware()
    {
        var builder = BuildHost(options => options.UseEfCoreStateStore<TestDbContext>(WriteConnectionString));

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IDbContextOutbox));
    }

    [Fact]
    public void MartenSelection_ConfiguresWolverineWithoutThePostgresqlMessageStore()
    {
        var builder = BuildHost(options => options.UseMartenEventStore(WriteConnectionString));

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IWolverineRuntime));
        Assert.DoesNotContain(
            builder.Services,
            descriptor => descriptor.ImplementationInstance is DatabaseSettings);
    }

    [Fact]
    public void MessagingSelection_ConfiguresWolverine()
    {
        var builder = BuildHost(options => options
            .UseMartenEventStore(WriteConnectionString)
            .UseWolverineMessaging(RabbitMqUri, TestMessaging.ExchangeName, TestMessaging.ContextName));

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IWolverineRuntime));
    }

    [Fact]
    public void NoCapabilitySelected_LeavesWolverineUnconfigured()
    {
        var builder = BuildHost(_ => { });

        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(IWolverineRuntime));
    }

    [Fact]
    public void HostSpecificWolverineConfiguration_IsApplied()
    {
        var builder = Host.CreateApplicationBuilder();
        var applied = false;

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly)
            .UseEfCoreStateStore<TestDbContext>(WriteConnectionString)
            .CustomizeWolverine(_ => applied = true));

        Assert.True(applied);
    }

    [Fact]
    public void HostSpecificWolverineConfiguration_WithoutAnyCapability_StillConfiguresWolverine()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options.CustomizeWolverine(_ => { }));

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IWolverineRuntime));
    }

    private static HostApplicationBuilder BuildHost(Action<ThesseraOptions> configure)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddThessera(options =>
        {
            options.AddDomainEventsFrom(typeof(FlushProbeStarted).Assembly);
            configure(options);
        });
        return builder;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}

