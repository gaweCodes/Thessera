using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

/// <summary>
/// Resolves the concrete type behind a <see cref="ServiceDescriptor"/> regardless of which
/// registration overload produced it.
/// </summary>
/// <remarks>
/// A descriptor registered through <c>AddScoped&lt;TService, TImplementation&gt;</c> carries
/// <see cref="ServiceDescriptor.ImplementationType"/>, but one registered through an instance
/// (for example <c>AddSingleton(instance)</c>) carries only
/// <see cref="ServiceDescriptor.ImplementationInstance"/>, and one registered through a factory
/// delegate carries neither — its concrete type is only known once the factory runs. A startup
/// check that reflects over <see cref="IServiceCollection"/> to find, say, every command handler's
/// implementation type and reads only <see cref="ServiceDescriptor.ImplementationType"/> silently
/// skips the other two registration styles instead of reporting them, turning "this handler is not
/// covered by the check" into a code path nothing ever reports.
/// </remarks>
internal static class ServiceDescriptorExtensions
{
    /// <summary>
    /// Returns the concrete implementation type of <paramref name="descriptor"/>, or
    /// <see langword="null"/> if it was registered through a factory delegate whose return type is
    /// not known without invoking it.
    /// </summary>
    public static Type? ResolveImplementationType(this ServiceDescriptor descriptor) =>
        descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
}
