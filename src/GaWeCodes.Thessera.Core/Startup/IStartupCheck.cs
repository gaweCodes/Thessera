namespace GaWeCodes.Thessera.Core.Startup;

/// <summary>
/// A check that turns silent misconfiguration into a message at boot.
/// </summary>
/// <remarks>
/// Five ship with the core: every command and query has exactly one handler, the aggregate style
/// matches the store, an aggregate state names itself, every integration-event mapper is reachable,
/// and a unit of work exists when commands do. Each is there because the failure it catches is
/// otherwise invisible until production.
/// <para>
/// Register your own as an enumerable service. Throwing is how a check fails, and the message it
/// throws is what somebody will read at three in the morning — say what is wrong, what it will cost,
/// and what to do about it.
/// </para>
/// </remarks>
/// <seealso cref="SynchronousStartupCheck"/>
public interface IStartupCheck
{
    /// <summary>
    /// Gets when this check runs.
    /// </summary>
    StartupPhase Phase { get; }

    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the check has passed.</returns>
    /// <exception cref="Exception">
    /// The check failed. Any exception stops the host from starting; the message is the whole point.
    /// </exception>
    Task RunAsync(CancellationToken cancellationToken);
}
