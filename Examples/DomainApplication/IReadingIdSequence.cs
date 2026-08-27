namespace DomainApplication;

public interface IReadingIdSequence
{
    ReadingId ReserveNext();

    void TryRelease(ReadingId id);
}
