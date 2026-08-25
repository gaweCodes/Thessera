using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Rules;

namespace DeadLetterFixture;

[IntegrationEventTopic("upstream.always-fails")]
public sealed record AlwaysFailsIntegrationEvent(string Name) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AttemptRecorder
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _names = new();

    public int Attempts => _names.Count;

    public IReadOnlyCollection<string> Names => [.. _names];

    public void Record(string name) => _names.Enqueue(name);
}

public sealed class AlwaysFailsConsumer
{
    public static void Handle(AlwaysFailsIntegrationEvent message, AttemptRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recorder);

        recorder.Record(message.Name);

        throw new InvalidOperationException($"'{message.Name}' can never be handled.");
    }
}

[IntegrationEventTopic("upstream.always-invalid")]
public sealed record AlwaysInvalidIntegrationEvent(string Name) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AlwaysInvalidConsumer
{
    public static void Handle(AlwaysInvalidIntegrationEvent message, AttemptRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recorder);

        recorder.Record(message.Name);

        throw new DomainValidationException($"'{message.Name}' will never become valid.");
    }
}

[IntegrationEventTopic("upstream.recorded")]
public sealed record RecordedIntegrationEvent(string Name) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RecordingConsumer
{
    public static void Handle(RecordedIntegrationEvent message, AttemptRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recorder);

        recorder.Record(message.Name);
    }
}

[IntegrationEventTopic("upstream.always-times-out")]
public sealed record AlwaysTimesOutIntegrationEvent(string Name) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AlwaysTimesOutConsumer
{
    public static void Handle(AlwaysTimesOutIntegrationEvent message, AttemptRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recorder);

        recorder.Record(message.Name);

        throw new TimeoutException($"'{message.Name}' timed out; the store may come back.");
    }
}
