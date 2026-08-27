namespace StateStored;

public sealed class ReadingIdSequence : IReadingIdSequence
{
    private int _current;
    private int _initialized;

    public void Initialize(int current)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            Interlocked.Exchange(ref _current, current);
            return;
        }

        while (true)
        {
            var existing = Volatile.Read(ref _current);
            if (existing >= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _current, current, existing) == existing)
            {
                return;
            }
        }
    }

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
