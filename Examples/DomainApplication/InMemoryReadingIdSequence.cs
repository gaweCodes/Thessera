namespace DomainApplication;

public sealed class InMemoryReadingIdSequence : IReadingIdSequence
{
    private int _current;

    public ReadingId ReserveNext() => new(Interlocked.Increment(ref _current));

    public void TryRelease(ReadingId id)
    {
        if (id.IsEmpty)
        {
            return;
        }

        while (true)
        {
            var current = Volatile.Read(ref _current);
            if (current != id.Value)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _current, current - 1, current) == current)
            {
                return;
            }
        }
    }
}
