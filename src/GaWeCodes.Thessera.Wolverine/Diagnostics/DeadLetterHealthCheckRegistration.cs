using System.Linq;
using Microsoft.Extensions.DependencyInjection;
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
    /// Both store packages call this, so a host that selected a store already has it. A host that
    /// selects both a main and an ancillary store calls this twice; the guard below makes the
    /// second call a no-op instead of registering the same health check name twice, which
    /// <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService"/> rejects
    /// at host startup.
    /// <para>
    /// The check reports <em>degraded</em> rather than unhealthy on purpose: the host is still
    /// serving requests correctly, but the work in those messages did not happen. A dead-lettered
    /// projection envelope in particular means a read model that stays wrong until it is rebuilt,
    /// which is a problem a person has to look at rather than one a restart fixes.
    /// </para>
    /// </remarks>
    public static void Register(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(DeadLetterInspector)))
        {
            return;
        }

        services.AddSingleton<DeadLetterInspector>();

        services.AddHealthChecks()
            .AddCheck<DeadLetterHealthCheck>(
                DeadLetterHealthCheck.Name,
                HealthStatus.Degraded,
                [DeadLetterHealthCheck.Tag]);
    }
}
