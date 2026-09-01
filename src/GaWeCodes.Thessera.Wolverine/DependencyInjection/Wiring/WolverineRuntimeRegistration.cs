using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;

/// <summary>
/// How a store or transport adapter announces that it needs the Wolverine runtime.
/// </summary>
/// <remarks>
/// A host runs exactly one runtime. Both overloads reach the same activator, so a store and a
/// transport that each ask for it end up sharing one rather than fighting over it — the runtime owns
/// the outbox, the inbox and the local queues, and two of them would each hold half of the delivery
/// guarantees.
/// </remarks>
public static class WolverineRuntimeRegistration
{
    /// <summary>
    /// Announces the runtime from a store adapter's <c>Register</c> method.
    /// </summary>
    /// <param name="context">The registration context handed to the adapter.</param>
    /// <returns>
    /// The activator, so the caller can chain <c>AddOutboxDurability</c> and bind the outbox to its
    /// own database.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static WolverineRuntimeActivator UseWolverineRuntime(this PersistenceRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterRuntimeServices(context.Services);
        return context.UseRuntime(static () => new WolverineRuntimeActivator());
    }

    /// <summary>
    /// Announces the runtime from a transport adapter's <c>Register</c> method.
    /// </summary>
    /// <param name="context">The registration context handed to the adapter.</param>
    /// <returns>
    /// The activator, shared with the store adapter when that one asked for it too.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static WolverineRuntimeActivator UseWolverineRuntime(this MessagingTransportRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterRuntimeServices(context.Services);
        return context.UseRuntime(static () => new WolverineRuntimeActivator());
    }

    private static void RegisterRuntimeServices(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IWolverineExtension, ThesseraWolverineExtension>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupCheck, WolverineRuntimeCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupCheck, InfrastructurePresenceCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupCheck, IntegrationEventSubscriptionCheck>());
    }
}
