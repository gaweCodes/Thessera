namespace GaWeCodes.Thessera.Core.Startup;

/// <summary>
/// When a startup check runs, relative to the host's hosted services.
/// </summary>
public enum StartupPhase
{
    /// <summary>
    /// Before any hosted service starts. The right phase for anything that should stop the host
    /// from coming up at all, because nothing has begun serving yet.
    /// </summary>
    BeforeHostedServicesStart = 0,

    /// <summary>
    /// After the hosted services are running. For checks that need something a hosted service
    /// brings up — a live connection, a started message engine.
    /// </summary>
    AfterHostedServicesStarted,
}
