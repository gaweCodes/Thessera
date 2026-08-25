namespace GaWeCodes.Thessera.Domain;

public interface IClock
{
    DateTimeOffset Now { get; }
}
