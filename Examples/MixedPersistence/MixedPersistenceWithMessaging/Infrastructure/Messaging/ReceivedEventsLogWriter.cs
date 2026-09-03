using System.Text.Json;

namespace MixedPersistenceWithMessaging;

public sealed class ReceivedEventsLogWriter(string logFilePath)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _entryCount;

    public int EntryCount => Volatile.Read(ref _entryCount);

    public string LogFilePath { get; } = logFilePath;

    public async Task AppendAsync(string routingKey, string payload, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(
            new ReceivedEventLogEntry(DateTimeOffset.UtcNow, routingKey, payload),
            MixedPersistenceWithMessagingJson.Options);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath) ?? Environment.CurrentDirectory);
            await File.AppendAllTextAsync(LogFilePath, line + Environment.NewLine, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _entryCount);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task WaitForCountAsync(int expectedCount, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (EntryCount < expectedCount)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Expected {expectedCount} received events but saw {EntryCount}.");
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }
}
