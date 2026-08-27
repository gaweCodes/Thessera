using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStoredWithMessaging;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
