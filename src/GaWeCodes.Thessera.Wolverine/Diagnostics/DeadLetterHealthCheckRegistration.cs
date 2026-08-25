using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GaWeCodes.Thessera.Wolverine.Diagnostics;

public static class DeadLetterHealthCheckRegistration
{
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
