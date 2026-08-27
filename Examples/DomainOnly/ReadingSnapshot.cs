namespace DomainOnly;

public sealed record ReadingSnapshot(int Id, int Value, bool IsRemoved)
{
    public static ReadingSnapshot From(Reading reading) => new(reading.Id.Value, reading.Value, reading.IsRemoved);
}
