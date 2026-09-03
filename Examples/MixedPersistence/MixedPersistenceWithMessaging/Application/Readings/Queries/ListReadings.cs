using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
