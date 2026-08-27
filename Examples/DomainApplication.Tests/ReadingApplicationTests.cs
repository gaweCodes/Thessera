using DomainApplication;
using GaWeCodes.Thessera.Domain.Rules;

public sealed class ReadingApplicationTests
{
    [Fact]
    public async Task CrudFlow_UsesTheThesseraContractsAgainstAnInMemoryRepository()
    {
        var app = new ReadingApplication();

        var created = await app.CreateAsync(14, TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value.DomainEvents);

        var listed = await app.ListAsync(TestContext.Current.CancellationToken);
        Assert.True(listed.IsSuccess);
        Assert.Single(listed.Value.Readings);

        var updated = await app.UpdateAsync(created.Value.Reading.Id, 21, TestContext.Current.CancellationToken);
        Assert.True(updated.IsSuccess);
        Assert.Equal(21, updated.Value.Reading.Value);

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
        var app = new ReadingApplication();

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
