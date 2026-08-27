namespace StateStored;

public interface IReadingIdSequence
{
    void Initialize(int current);

    ReadingId ReserveNext();

    void TryRelease(ReadingId id);
}
