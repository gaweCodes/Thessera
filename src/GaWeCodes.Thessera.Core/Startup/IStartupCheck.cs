namespace GaWeCodes.Thessera.Core.Startup;

public interface IStartupCheck
{
    StartupPhase Phase { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
