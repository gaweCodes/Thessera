using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GaWeCodes.Thessera.Wolverine.Diagnostics;

/// <summary>
/// Registers the health check that reports whether messages have been dead-lettered.
/// </summary>
public static class DeadLetterHealthCheckRegistration
{
    /// <summary>
    /// Adds the dead-letter health check to the host.
    /// </summary>
    /// <param name="services">The service collection being built.</param>
    /// <remarks>
    /// Both store packages call this, so a host that selected a store already has it.
    /// <para>
    /// The check reports <em>degraded</em> rather than unhealthy on purpose: the host is still
    /// serving requests correctly, but the work in those messages did not happen. A dead-lettered
    /// projection envelope in particular means a read model that stays wrong until it is rebuilt,
    /// which is a problem a person has to look at rather than one a restart fixes.
    /// </para>
    /// </remarks>
    public static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<DeadLetterInspector>();

        services.AddHealthChecks()
            .AddCheck<DeadLetterHealthCheck>(
                DeadLetterHealthCheck.Name,
                HealthStatus.Degraded,
                [DeadLetterHealthCheck.Tag]);
    }
}
