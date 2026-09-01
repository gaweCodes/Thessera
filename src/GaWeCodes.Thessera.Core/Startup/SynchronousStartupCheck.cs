namespace GaWeCodes.Thessera.Core.Startup;

/// <summary>
/// A startup check that has nothing to await — most of them, since they inspect registrations and
/// types rather than talk to anything.
/// </summary>
/// <remarks>
/// Implement <see cref="Run"/> and the asynchronous half is taken care of. Use
/// <see cref="IStartupCheck"/> directly when the check has to reach a database or a broker.
/// </remarks>
public abstract class SynchronousStartupCheck : IStartupCheck
{
    /// <inheritdoc/>
    public abstract StartupPhase Phase { get; }

    /// <inheritdoc/>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        Run();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <exception cref="Exception">
    /// The check failed. Any exception stops the host from starting.
    /// </exception>
    protected abstract void Run();
}
