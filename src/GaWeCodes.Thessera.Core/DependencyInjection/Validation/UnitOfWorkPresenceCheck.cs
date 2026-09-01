using System.Reflection;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

internal sealed partial class UnitOfWorkPresenceCheck(
    IServiceProvider serviceProvider,
    PersistenceSelection persistence,
    IReadOnlyCollection<Assembly> scannedAssemblies,
    ILogger<UnitOfWorkPresenceCheck> logger) : SynchronousStartupCheck
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        if (persistence.IsSelected)
        {
            return;
        }

        if (persistence.IsDeliberatelyWithoutPersistence)
        {
            LogNoPersistenceSelected(logger);
            return;
        }

        using var scope = serviceProvider.CreateScope();
        if (scope.ServiceProvider.GetService<IUnitOfWork>() is not NullUnitOfWork)
        {
            return;
        }

        var commands = CommandsIn(scannedAssemblies);
        if (commands.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "No persistence strategy was selected, but the scanned assemblies contain commands and no IUnitOfWork " +
            "is registered. Every one of these commands would report success while nothing is committed, and " +
            $"nothing at run time would say so: {string.Join(", ", commands.Take(5))}" +
            $"{(commands.Count > 5 ? $" and {commands.Count - 5} more" : string.Empty)}. Select " +
            "UseEfCoreStateStore<TContext>(writeConnectionString) or UseMartenEventStore(writeConnectionString), " +
            "register the host's own IUnitOfWork, or state the intent with UseNoPersistence().");
    }

    private static List<string> CommandsIn(IReadOnlyCollection<Assembly> assemblies)
    {
        var commands = new List<string>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsClass: true, IsAbstract: false } && !type.IsGenericTypeDefinition && IsCommand(type))
                {
                    commands.Add($"'{type}'");
                }
            }
        }

        return commands;
    }

    private static bool IsCommand(Type type) =>
        Array.Exists(
            type.GetInterfaces(),
            static contract => contract == typeof(ICommand)
                || (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)));

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "UseNoPersistence was selected — commands are dispatched without a unit of work and nothing is committed.")]
    private static partial void LogNoPersistenceSelected(ILogger logger);
}
