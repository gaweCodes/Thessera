using GaWeCodes.Thessera.Application.Results;

namespace StateStored;

public static class ReadingResponseFactory
{
    public static Result<ReadingOperationResponse> ForMutation(string operation, Reading reading) =>
        new ReadingOperationResponse(
            operation,
            ReadingSnapshot.From(reading),
            [.. reading.DomainEvents.Select(ReadingEventInfo.From)]);
}
