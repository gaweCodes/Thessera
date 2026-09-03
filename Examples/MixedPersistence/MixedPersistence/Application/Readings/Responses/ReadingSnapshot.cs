namespace MixedPersistence;

public sealed record ReadingSnapshot(
    int Id,
    int Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    long Version)
{
    public static ReadingSnapshot From(Reading reading) =>
        new(reading.Id.Value, reading.Value, reading.CreatedAt, reading.UpdatedAt, reading.IsDeleted, reading.DeletedAt, reading.Version);
}
