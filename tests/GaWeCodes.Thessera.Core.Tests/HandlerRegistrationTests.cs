using ConflictingHandlersFixture;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using ValidHandlersFixture;

namespace GaWeCodes.Thessera.Tests;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public void AddHandlersFrom_RegistersCommandQueryProjectionHandlersAndMappers()
    {
        using var provider = BuildProvider(handlerScans: 1);

        Assert.Single(provider.GetServices<ICommandHandler<RegistrationCommand>>());
        Assert.Single(provider.GetServices<IQueryHandler<RegistrationQuery, int>>());
        Assert.Single(provider.GetServices<IProjectionHandler<RegistrationEvent>>());
        Assert.Contains(provider.GetServices<IIntegrationEventMapper<RegistrationEvent>>(), mapper => mapper is RegistrationMapper);
    }

    [Fact]
    public void AddHandlersFrom_CalledTwiceForSameAssembly_DoesNotDuplicateProjectionHandlers()
    {
        using var provider = BuildProvider(handlerScans: 2);

        Assert.Single(provider.GetServices<IProjectionHandler<RegistrationEvent>>());
    }

    [Fact]
    public void AddHandlersFrom_CalledTwiceForSameAssembly_DoesNotDuplicateIntegrationEventMappers()
    {
        using var provider = BuildProvider(handlerScans: 2);

        Assert.Single(provider.GetServices<IIntegrationEventMapper<RegistrationEvent>>(), mapper => mapper is RegistrationMapper);
    }

    [Fact]
    public void AddHandlersFrom_CalledTwiceForSameAssembly_DoesNotThrowForSingleHandlers()
    {
        using var provider = BuildProvider(handlerScans: 2);

        Assert.Single(provider.GetServices<ICommandHandler<RegistrationCommand>>());
        Assert.Single(provider.GetServices<IQueryHandler<RegistrationQuery, int>>());
    }

    [Fact]
    public void AddHandlersFrom_TwoDifferentHandlersForSameCommand_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options =>
                options.AddHandlersFrom(typeof(ConflictingCommand).Assembly)));

        Assert.Contains(nameof(FirstConflictingCommandHandler), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SecondConflictingCommandHandler), exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(int handlerScans)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(options =>
        {
            for (var scan = 0; scan < handlerScans; scan++)
            {
                options.AddHandlersFrom(typeof(RegistrationCommand).Assembly);
            }
        });
        return services.BuildServiceProvider();
    }
}
