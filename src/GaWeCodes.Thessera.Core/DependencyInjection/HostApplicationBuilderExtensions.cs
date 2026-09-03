using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Core;
using GaWeCodes.Thessera.Core.DependencyInjection;
using Microsoft.Extensions.Hosting;

#pragma warning disable IDE0130
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130

/// <summary>
/// The way a host wires up Thessera. This is the overload to use.
/// </summary>
/// <remarks>
/// Declared in the shared <c>GaWeCodes.Thessera</c> namespace rather than this package's own — like
/// <c>AddConsole()</c> in <c>Microsoft.Extensions.Logging</c> — so a consumer reaches every
/// <c>Use*</c>/<c>AddThessera</c> call with a single <c>using</c>.
/// </remarks>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers Thessera and activates the runtime the selected packages asked for.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="configure">
    /// Picks the handlers, the domain events, the store, the transport and everything else. Called
    /// once.
    /// </param>
    /// <returns>The same <paramref name="builder"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The wiring is contradictory — two stores, two runtimes, a transport without a store, or a
    /// persistence choice with no registered domain events. These are reported here rather than left
    /// to fail later.
    /// </exception>
    /// <remarks>
    /// Call this <strong>once per host</strong>. Every satellite package contributes through a
    /// <c>Use*</c> extension inside <paramref name="configure"/> rather than through a call of its
    /// own, and one <c>using GaWeCodes.Thessera;</c> reaches all of them.
    /// <para>
    /// Prefer this overload over the <c>IServiceCollection</c> one whenever a store or a transport is
    /// involved: activating a runtime needs the host builder, and the other overload cannot do it.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    [RequiresDynamicCode(TrimmingMessages.DynamicGenerics)]
    public static TBuilder AddThessera<TBuilder>(
        this TBuilder builder,
        Action<ThesseraOptions> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var wiring = ServiceCollectionExtensions.AddThesseraCore(builder.Services, configure);

        wiring.Runtime.Activator?.Activate(builder, wiring);

        return builder;
    }
}
