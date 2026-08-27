namespace StateStoredWithMessaging;

public sealed record ReceivedEventLogEntry(DateTimeOffset ReceivedAt, string RoutingKey, string Payload);
