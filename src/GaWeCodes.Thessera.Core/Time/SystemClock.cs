using GaWeCodes.Thessera.Domain;

namespace GaWeCodes.Thessera.Core.Time;

internal sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset Now => timeProvider.GetUtcNow().ToUniversalTime();
}
