using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests;
using StateStored;

[Collection(PostgreSqlCollection.Name)]
public sealed class StateStoredApplicationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task CrudFlow_UsesTheEfCorePostgresStore()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await StateStoredApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var created = await app.CreateAsync(10, TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value.DomainEvents);

        var listed = await app.ListAsync(TestContext.Current.CancellationToken);
        Assert.True(listed.IsSuccess);
        Assert.Single(listed.Value.Readings);

        var updated = await app.UpdateAsync(created.Value.Reading.Id, 15, TestContext.Current.CancellationToken);
        Assert.True(updated.IsSuccess);
        Assert.Equal(15, updated.Value.Reading.Value);

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

        await using var app = await StateStoredApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

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
}
