namespace MixedPersistenceWithMessaging;

public sealed record ReceivedEventLogEntry(DateTimeOffset ReceivedAt, string RoutingKey, string Payload);
