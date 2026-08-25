using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Registration;

internal sealed class ProvisioningRegistrar(ProvisioningSelection provisioning)
{
    public void Select(InfrastructureProvisioning mode) => provisioning.Select(mode);
}
