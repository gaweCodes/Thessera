using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Events;

namespace DomainApplication;

public static class ReadingResponseFactory
{
    public static Result<ReadingOperationResponse> ForMutation(string operation, Reading reading)
    {
        var response = new ReadingOperationResponse(
            operation,
            ReadingSnapshot.From(reading),
            [.. reading.DomainEvents.Select(ReadingEventInfo.From)]);

        ((IDomainEventOwner)reading).ClearDomainEvents();
        return response;
    }
}
