namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

internal sealed class ProvisioningSelection
{
    public InfrastructureProvisioning Mode { get; private set; } = InfrastructureProvisioning.Never;

    public bool ProvisionsInfrastructure => Mode == InfrastructureProvisioning.AtStartup;

    public void Select(InfrastructureProvisioning mode) => Mode = mode;
}
