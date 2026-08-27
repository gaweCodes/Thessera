using System.Text.Json;

namespace DomainOnly;

public sealed record OperationResult(
    bool Success,
    string Operation,
    string? Error,
    ReadingSnapshot? Reading,
    IReadOnlyList<ReadingSnapshot> Readings,
    IReadOnlyList<JsonElement> DomainEvents)
{
    public static OperationResult Completed(
        string operation,
        ReadingSnapshot? reading,
        IReadOnlyList<ReadingSnapshot> readings,
        IReadOnlyList<JsonElement> domainEvents) =>
        new(true, operation, null, reading, readings, domainEvents);

    public static OperationResult Failure(string operation, string error) =>
        new(false, operation, error, null, [], []);
}
