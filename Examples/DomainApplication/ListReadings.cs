using GaWeCodes.Thessera.Application.Cqrs;

namespace DomainApplication;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
