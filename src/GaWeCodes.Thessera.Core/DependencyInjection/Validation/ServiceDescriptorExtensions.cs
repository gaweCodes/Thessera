using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

internal static class ServiceDescriptorExtensions
{
    public static Type? ResolveImplementationType(this ServiceDescriptor descriptor) =>
        descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
}
