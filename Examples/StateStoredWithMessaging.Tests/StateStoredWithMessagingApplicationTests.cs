using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests;
using StateStoredWithMessaging;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class StateStoredWithMessagingApplicationTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    [Fact]
    public async Task CrudFlow_PublishesAndReceivesRabbitMqMessages()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await StateStoredWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var created = await app.CreateAsync(11, TestContext.Current.CancellationToken);
            Assert.True(created.IsSuccess);

            var updated = await app.UpdateAsync(created.Value.Reading.Id, 19, TestContext.Current.CancellationToken);
            Assert.True(updated.IsSuccess);

            var deleted = await app.DeleteAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
            Assert.True(deleted.IsSuccess);

            var logPath = Path.Combine(artifactDirectory, "received-events.log");
            Assert.True(File.Exists(logPath));

            var content = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);
            Assert.Contains("state-readings.reading-created", content, StringComparison.Ordinal);
            Assert.Contains("state-readings.reading-updated", content, StringComparison.Ordinal);
            Assert.Contains("state-readings.reading-deleted", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Create_WithNonPositiveValue_ReturnsValidationFailure()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await StateStoredWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var result = await app.CreateAsync(0, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal("reading.value.not-positive", result.Failures[0].Code);

            var listed = await app.ListAsync(TestContext.Current.CancellationToken);
            Assert.True(listed.IsSuccess);
            Assert.Empty(listed.Value.Readings);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Reading_Record_ThrowsWhenTheValueIsNotPositive()
    {
        Assert.Throws<DomainValidationException>(() => Reading.Record(new ReadingId(1), -1));
    }

    [Fact]
    public async Task StartAsync_RebuildsTheReadModelFromWriteRowsWrittenByAnEarlierProcess()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        Result<ReadingOperationResponse> created = default!;

        try
        {
            await using (var first = await StateStoredWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken))
            {
                created = await first.CreateAsync(42, TestContext.Current.CancellationToken);
                Assert.True(created.IsSuccess);
            }

            // The read model is in-memory only, so a fresh process has to re-read the write table
            // to know the reading exists at all - this is what StartAsync does on boot.
            await using var second = await StateStoredWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var listed = await second.ListAsync(TestContext.Current.CancellationToken);
            Assert.True(listed.IsSuccess);
            Assert.Contains(listed.Value.Readings, reading => reading.Id == created.Value.Reading.Id && reading.Value == 42);
        }
        finally
        {
            if (created is { IsSuccess: true })
            {
                await using var cleanup = await StateStoredWithMessagingApplication.StartAsync(
                    postgres.ConnectionString,
                    rabbit.ConnectionUri,
                    artifactDirectory,
                    TestContext.Current.CancellationToken);
                await cleanup.DeleteAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
            }

            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }
}
