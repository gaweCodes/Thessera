using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Core;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using Microsoft.Extensions.DependencyInjection;

// Deliberate exception to the package/namespace rule. The composition entry points stay in the
// shared root namespace so a consumer's Program.cs reaches AddThessera and every Use*
// call with one using -- the same reason AddConsole() lives in Microsoft.Extensions.Logging
// and not in Microsoft.Extensions.Logging.Console. Every other type in this package matches
// its package name, so IDE0130 is suppressed here and nowhere else.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130
public static class ServiceCollectionExtensions
{
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    [RequiresDynamicCode(TrimmingMessages.DynamicGenerics)]
    public static IServiceCollection AddThessera(this IServiceCollection services, Action<ThesseraOptions> configure)
    {
        AddThesseraCore(services, configure);
        return services;
    }

    internal static ThesseraWiringSettings AddThesseraCore(IServiceCollection services, Action<ThesseraOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return ThesseraComposition.Compose(services, configure);
    }
}
