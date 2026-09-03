namespace MixedPersistenceWithMessaging;

public interface IAccountIdSequence
{
    void Initialize(int current);

    AccountId ReserveNext();

    void TryRelease(AccountId id);
}
