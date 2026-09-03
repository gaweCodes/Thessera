using EventSourced;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventSourcedApplicationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task CrudFlow_UsesTheMartenEventStore()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var created = await app.CreateAsync(8, TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value.DomainEvents);

        var listed = await app.ListAsync(TestContext.Current.CancellationToken);
        Assert.True(listed.IsSuccess);
        Assert.Single(listed.Value.Readings);

        var updated = await app.UpdateAsync(created.Value.Reading.Id, 13, TestContext.Current.CancellationToken);
        Assert.True(updated.IsSuccess);
        Assert.Equal(13, updated.Value.Reading.Value);

        var deleted = await app.DeleteAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
        Assert.True(deleted.IsSuccess);
        Assert.True(deleted.Value.Reading.IsDeleted);

        var empty = await app.ListAsync(TestContext.Current.CancellationToken);
        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Value.Readings);
    }

    [Fact]
    public async Task Create_WithNonPositiveValue_ReturnsValidationFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.CreateAsync(0, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("reading.value.not-positive", result.Failures[0].Code);

        var listed = await app.ListAsync(TestContext.Current.CancellationToken);
        Assert.True(listed.IsSuccess);
        Assert.Empty(listed.Value.Readings);
    }

    [Fact]
    public void Reading_Record_ThrowsWhenTheValueIsNotPositive()
    {
        Assert.Throws<DomainValidationException>(() => Reading.Record(new ReadingId(1), -1));
    }

    [Fact]
    public async Task Update_WithUnknownId_ReturnsNotFound()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.UpdateAsync(999, 21, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("reading.not_found", result.Failures[0].Code);
    }

    [Fact]
    public async Task Delete_WithUnknownId_ReturnsNotFound()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.DeleteAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("reading.not_found", result.Failures[0].Code);
    }

    [Fact]
    public async Task Update_WithNonPositiveValue_ReturnsValidationFailureAndLeavesTheReadingUnchanged()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
        var created = await app.CreateAsync(8, TestContext.Current.CancellationToken);

        try
        {
            var result = await app.UpdateAsync(created.Value.Reading.Id, 0, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal("reading.value.not-positive", result.Failures[0].Code);

            var listed = await app.ListAsync(TestContext.Current.CancellationToken);
            var reading = listed.Value.Readings.Single(reading => reading.Id == created.Value.Reading.Id);
            Assert.Equal(8, reading.Value);
        }
        finally
        {
            await app.DeleteAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Create_AfterAFailedCreate_ReusesTheReleasedId()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
        var baseline = await app.CreateAsync(1, TestContext.Current.CancellationToken);

        try
        {
            var failed = await app.CreateAsync(0, TestContext.Current.CancellationToken);
            Assert.False(failed.IsSuccess);

            var created = await app.CreateAsync(5, TestContext.Current.CancellationToken);

            try
            {
                Assert.True(created.IsSuccess);
                Assert.Equal(baseline.Value.Reading.Id + 1, created.Value.Reading.Id);
            }
            finally
            {
                await app.DeleteAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            await app.DeleteAsync(baseline.Value.Reading.Id, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StartAsync_RebuildsTheReadModelFromEventStreamsWrittenByAnEarlierProcess()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        Result<ReadingOperationResponse> created;
        await using (var first = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken))
        {
            created = await first.CreateAsync(42, TestContext.Current.CancellationToken);
            Assert.True(created.IsSuccess);
        }

        try
        {
            // The read model is in-memory only, so a fresh process has to replay the Marten
            // streams to know the reading exists at all - this is what StartAsync does on boot.
            await using var second = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

            var listed = await second.ListAsync(TestContext.Current.CancellationToken);
            Assert.True(listed.IsSuccess);
            Assert.Contains(listed.Value.Readings, reading => reading.Id == created.Value.Reading.Id && reading.Value == 42);
        }
        finally
        {
            await using var cleanup = await EventSourcedApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
            await cleanup.DeleteAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
        }
    }
}
