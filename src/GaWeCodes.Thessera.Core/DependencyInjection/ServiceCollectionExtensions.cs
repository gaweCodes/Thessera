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
/// <summary>
/// Registers Thessera into a bare service collection, for the cases where there is no host builder.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Thessera's services without activating a runtime.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Picks the handlers, the domain events and everything else.</param>
    /// <returns>The same <paramref name="services"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The wiring is contradictory.</exception>
    /// <remarks>
    /// <strong>This overload cannot activate a message engine.</strong> A host that selects a store
    /// or a transport and uses this overload registers everything and then starts without a runtime —
    /// which a startup check reports rather than letting the host run half-wired. Use the
    /// <see cref="Microsoft.Extensions.Hosting.IHostApplicationBuilder"/> overload instead, or wire
    /// the engine on the host builder yourself.
    /// <para>
    /// It is the right choice for a host that only dispatches commands and delivers domain events in
    /// process, and for tests that build a container directly.
    /// </para>
    /// </remarks>
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
