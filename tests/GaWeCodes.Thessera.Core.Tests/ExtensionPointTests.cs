using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class ExtensionPointTests
{
    [Fact]
    public async Task AConsumerStartupCheck_RunsInTheDeclaredPhase()
    {
        var observed = new List<string>();

        using var host = Host.CreateApplicationBuilder().ConfigureForTest(services =>
        {
            services.AddSingleton<IStartupCheck>(
                new ConsumerStartupCheck(StartupPhase.AfterHostedServicesStarted, () => observed.Add("consumer-check")));
            services.AddHostedService(_ => new CallbackHostedService(() => observed.Add("consumer-service")));
        });

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["consumer-service", "consumer-check"], observed);
    }

    [Fact]
    public async Task AFailingConsumerStartupCheck_StopsTheHost()
    {
        using var host = Host.CreateApplicationBuilder().ConfigureForTest(services =>
            services.AddSingleton<IStartupCheck>(
                new ConsumerStartupCheck(
                    StartupPhase.BeforeHostedServicesStart,
                    () => throw new InvalidOperationException("the consumer said no"))));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("the consumer said no", thrown.Message);
    }

    [Fact]
    public async Task AConsumerFaultTranslator_TranslatesAFaultNoBuiltInTranslatorKnows()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new ThrowingUnitOfWork(new TimeoutException("the tenant quota lease timed out")));
        services.AddScoped<ICommandHandler<ProbeCommand>>(_ => new PassingCommandHandler());
        services.AddSingleton<IPersistenceFaultTranslator, TenantQuotaTranslator>();
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new ProbeCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("tenant.quota-exceeded", failure.Code);
        Assert.Equal(FailureCategory.Conflict, failure.Category);
    }

    private sealed record ProbeCommand : ICommand;

    private sealed class PassingCommandHandler : ICommandHandler<ProbeCommand>
    {
        public Task<Result> HandleAsync(ProbeCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class ThrowingUnitOfWork(Exception exception) : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => throw exception;
    }

    private sealed class TenantQuotaTranslator : IPersistenceFaultTranslator
    {
        public bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure)
        {
            if (exception is TimeoutException)
            {
                failure = Failure.Conflict("tenant.quota-exceeded", "The tenant has no capacity left.");
                return true;
            }

            failure = null;
            return false;
        }
    }

    private sealed class ConsumerStartupCheck(StartupPhase phase, Action onRun) : IStartupCheck
    {
        public StartupPhase Phase => phase;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            onRun();
            return Task.CompletedTask;
        }
    }

    private sealed class CallbackHostedService(Action onStart) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            onStart();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

internal static class ExtensionPointHostExtensions
{
    public static IHost ConfigureForTest(this HostApplicationBuilder builder, Action<IServiceCollection> configure)
    {
        configure(builder.Services);
        builder.Services.AddThessera(_ => { });
        return builder.Build();
    }
}
