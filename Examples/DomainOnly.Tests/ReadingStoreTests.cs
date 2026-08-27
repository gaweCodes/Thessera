using DomainOnly;
using GaWeCodes.Thessera.Domain.Rules;

public sealed class ReadingStoreTests
{
    [Fact]
    public void CrudFlow_UpdatesTheInMemoryStore()
    {
        var store = new ReadingStore();

        var created = store.Create(12);
        Assert.True(created.Success);
        Assert.NotNull(created.Reading);
        Assert.Single(created.DomainEvents);

        var listed = store.List();
        Assert.True(listed.Success);
        Assert.Single(listed.Readings);

        var updated = store.Update(created.Reading!.Id, 18);
        Assert.True(updated.Success);
        Assert.Equal(18, updated.Reading!.Value);
        Assert.Single(updated.DomainEvents);

        var deleted = store.Delete(created.Reading.Id);
        Assert.True(deleted.Success);
        Assert.True(deleted.Reading!.IsRemoved);
        Assert.Single(deleted.DomainEvents);

        var empty = store.List();
        Assert.Empty(empty.Readings);
    }

    [Fact]
    public void Create_WithNonPositiveValue_FailsWithoutCreatingAReading()
    {
        var store = new ReadingStore();

        var result = store.Create(0);

        Assert.False(result.Success);
        Assert.Null(result.Reading);
        Assert.Empty(store.List().Readings);
    }

    [Fact]
    public void Reading_Record_RaisesTheDomainEventThroughTheAggregate()
    {
        Assert.Throws<DomainValidationException>(() => Reading.Record(new ReadingId(1), -1));
    }

    [Fact]
    public void Update_OnMissingId_Fails()
    {
        var store = new ReadingStore();

        var result = store.Update(999, 5);

        Assert.False(result.Success);
        Assert.Equal("Reading not found.", result.Error);
    }
}
