using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Persistence;

public interface IOutboxDurabilityConfigurator
{
    void Configure(WolverineOptions options);
}
