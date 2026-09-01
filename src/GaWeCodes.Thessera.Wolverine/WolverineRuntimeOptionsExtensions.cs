using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;
using Wolverine;

// Deliberate exception to the package/namespace rule. The composition entry points stay in the
// shared root namespace so a consumer's Program.cs reaches AddThessera and every Use*
// call with one using -- the same reason AddConsole() lives in Microsoft.Extensions.Logging
// and not in Microsoft.Extensions.Logging.Console. Every other type in this package matches
// its package name, so IDE0130 is suppressed here and nowhere else.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130
/// <summary>
/// Reaches the message engine's own configuration from inside a Thessera host.
/// </summary>
public static class WolverineRuntimeOptionsExtensions
{
    /// <summary>
    /// Applies your own configuration to the message engine, on top of everything the family
    /// configures.
    /// </summary>
    /// <param name="options">The options being configured inside <c>AddThessera</c>.</param>
    /// <param name="customize">Your configuration, applied last.</param>
    /// <returns>The same <paramref name="options"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="customize"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Because it runs last, it can override anything — including the parts you should leave alone.
    /// Use it for what belongs to you: extra endpoints, retry policies, durability mode. Leave the
    /// outbox and the domain-event routing untouched unless you know what you are replacing; they
    /// are what carry the delivery guarantees.
    /// </remarks>
    public static ThesseraOptions CustomizeWolverine(
        this ThesseraOptions options,
        Action<WolverineOptions> customize)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(customize);

        options.Runtime.GetOrAdd(static () => new WolverineRuntimeActivator()).Customize(customize);
        return options;
    }
}
