using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;

namespace GaWeCodes.Thessera.Wolverine.Diagnostics;

internal sealed class DeadLetterHealthCheck(DeadLetterInspector inspector) : IHealthCheck
{
    public const string Name = "thessera-dead-letters";

    public const string Tag = "dead-letters";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var count = await inspector.CountAsync(cancellationToken).ConfigureAwait(false);

        return count == 0
            ? HealthCheckResult.Healthy("No message has been dead-lettered.")
            : HealthCheckResult.Degraded(
                $"{count.ToString(CultureInfo.InvariantCulture)} message(s) were given up on and moved to the dead "
                + "letter queue. This host keeps serving requests, which is why this is degraded rather than "
                + "unhealthy, but the work in those messages did not happen: a dead-lettered projection envelope "
                + "means the read model is missing that change and will stay wrong until the projection is fixed "
                + "and the read model rebuilt.",
                data: new Dictionary<string, object> { ["count"] = count });
    }
}

internal sealed class DeadLetterInspector(IMessageStore store)
{
    public async Task<long> CountAsync(CancellationToken cancellationToken)
    {
        var results = await store.DeadLetters
            .QueryAsync(new DeadLetterEnvelopeQuery { PageSize = 1 }, cancellationToken)
            .ConfigureAwait(false);

        return results.TotalCount;
    }
}
