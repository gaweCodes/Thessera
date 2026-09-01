namespace GaWeCodes.Thessera.Core.DependencyInjection;

/// <summary>
/// Whether a host may create schema, exchanges and queues on its own.
/// </summary>
public enum InfrastructureProvisioning
{
    /// <summary>
    /// The host creates nothing and expects the infrastructure to be there. The default, and what a
    /// service should say: starting a second instance then cannot change the database or the
    /// broker, and a missing exchange is reported by a startup check instead of being created by
    /// whichever instance won the race.
    /// </summary>
    Never = 0,

    /// <summary>
    /// The host creates what is missing while it starts. Appropriate for a migration job or for
    /// local development, not for a service in production.
    /// </summary>
    AtStartup = 1,
}
