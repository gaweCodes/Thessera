namespace GaWeCodes.Thessera.Core.Persistence;

public interface IPersistenceAdapter
{
    string Description { get; }

    string WriteConnectionString { get; }

    AggregateStyle AggregateStyle { get; }

    bool IsTransientFault(Exception exception);

    void Register(PersistenceRegistrationContext context);
}
