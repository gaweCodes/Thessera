using System.Text.Json;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Tests;

public sealed class EntityKeyJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = EntityKeyJsonOptions.Create();

    [Fact]
    public void AGuidKey_IsWrittenAsABareValue_NotAsAnObject()
    {
        var key = new SampleGuidKey(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var json = JsonSerializer.Serialize(key, Options);

        Assert.Equal("\"11111111-1111-1111-1111-111111111111\"", json);
    }

    [Fact]
    public void ThePayload_CarriesNoComputedIsEmptyMember()
    {
        var json = JsonSerializer.Serialize(
            new SampleEvent(new SampleGuidKey(Guid.NewGuid()), new SampleStringKey("abc"), new SampleIntKey(7)),
            Options);

        Assert.DoesNotContain("IsEmpty", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Value", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"Id":"11111111-1111-1111-1111-111111111111","Name":"abc","Number":7}""")]
    public void ARecordWithTypedKeys_RoundTripsThroughTheBareValueFormat(string expectedJson)
    {
        var original = new SampleEvent(
            new SampleGuidKey(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SampleStringKey("abc"),
            new SampleIntKey(7));

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<SampleEvent>(json, Options);

        Assert.Equal(expectedJson, json);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void AnEmptyKey_RoundTripsAsItsUnderlyingValue()
    {
        var json = JsonSerializer.Serialize(default(SampleGuidKey), Options);

        Assert.Equal("\"00000000-0000-0000-0000-000000000000\"", json);
        Assert.True(JsonSerializer.Deserialize<SampleGuidKey>(json, Options).IsEmpty);
    }

    [Fact]
    public void ANullValue_ThrowsAClearJsonException_NotANullReference()
    {
        var exception = Record.Exception(() => JsonSerializer.Deserialize<SampleStringKey>("null", Options));

        Assert.IsType<JsonException>(exception);
    }

    [Fact]
    public void AKeyWithoutASingleValueConstructor_ThrowsAndNamesTheKeyType()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => JsonSerializer.Deserialize<KeyWithoutValueConstructor>("\"abc\"", Options));

        Assert.Contains(nameof(KeyWithoutValueConstructor), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeThatIsNoEntityKey_IsSerializedByTheDefaultConverter()
    {
        var guid = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var withEntityKeyOptions = JsonSerializer.Serialize(guid, Options);
        var withDefaultOptions = JsonSerializer.Serialize(guid);

        Assert.Equal(withDefaultOptions, withEntityKeyOptions);
    }

    private readonly record struct SampleGuidKey(Guid Value) : IEntityKey<Guid>
    {
        public bool IsEmpty => Value == Guid.Empty;
    }

    private readonly record struct SampleStringKey(string Value) : IEntityKey<string>
    {
        public bool IsEmpty => string.IsNullOrEmpty(Value);
    }

    private readonly record struct SampleIntKey(int Value) : IEntityKey<int>
    {
        public bool IsEmpty => Value == 0;
    }

    private readonly record struct KeyWithoutValueConstructor : IEntityKey<string>
    {
        public string Value => string.Empty;

        public bool IsEmpty => true;
    }

    private sealed record SampleEvent(SampleGuidKey Id, SampleStringKey Name, SampleIntKey Number);
}
