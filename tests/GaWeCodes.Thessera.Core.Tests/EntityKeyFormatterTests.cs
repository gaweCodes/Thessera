using System.Globalization;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;

public sealed class EntityKeyFormatterTests
{
    [Fact]
    public void GetStreamKey_ComposesTheAggregateNameAndTheKeyValue()
    {
        var streamKey = EntityKeyFormatter.GetStreamKey(
            EntityKeyFormatter.GetAggregateName(typeof(Recipe)),
            EntityKeyFormatter.GetKeyValue(new RecipeId(42)));

        Assert.Equal("recipe/42", streamKey);
    }

    [Fact]
    public void GetAggregateName_DoesNotFollowTheClrTypeName()
    {
        Assert.Equal("recipe", EntityKeyFormatter.GetAggregateName(typeof(Recipe)));
        Assert.Equal("recipe", EntityKeyFormatter.GetAggregateName(typeof(RenamedRecipe)));
    }

    [Fact]
    public void GetAggregateName_WithoutTheAttribute_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EntityKeyFormatter.GetAggregateName(typeof(UnnamedAggregate)));

        Assert.Contains("AggregateName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetKeyValue_IsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var german = EntityKeyFormatter.GetKeyValue(new RecipeId(1234567));

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariant = EntityKeyFormatter.GetKeyValue(new RecipeId(1234567));

            Assert.Equal(invariant, german);
            Assert.Equal("1234567", german);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void GetKeyValue_WithAnEmptyKey_IsRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EntityKeyFormatter.GetKeyValue(new RecipeId(0)));

        Assert.Contains("empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetKeyValue_WithANegativeKey_IsRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EntityKeyFormatter.GetKeyValue(new RecipeId(-1)));

        Assert.Contains("negative", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetKeyValue_WithAStringKeyContainingTheSeparator_IsRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EntityKeyFormatter.GetKeyValue(new CountryCode("ch/zh")));

        Assert.Contains("same stream", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetKeyValue_WithAStringKey_KeepsTheValueVerbatim() =>
        Assert.Equal("ch", EntityKeyFormatter.GetKeyValue(new CountryCode("ch")));

    [Fact]
    public void GetKeyValue_WithAGuidKey_IsLowerCaseAndHyphenated() =>
        Assert.Equal(
            "2f1b7c4e-0000-0000-0000-000000000000",
            EntityKeyFormatter.GetKeyValue(new WidgetId(new Guid("2F1B7C4E-0000-0000-0000-000000000000"))));

    [Fact]
    public void GetKeyValue_WithAnUndeclaredValueType_IsRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EntityKeyFormatter.GetKeyValue(new PriceId(1.50m)));

        Assert.Contains("no declared stream-key format", exception.Message, StringComparison.Ordinal);
    }

    [AggregateName("recipe")]
    private sealed class Recipe;

    [AggregateName("recipe")]
    private sealed class RenamedRecipe;

    private sealed class UnnamedAggregate;

    private readonly record struct RecipeId(int Value) : IEntityKey<int>
    {
        public bool IsEmpty => Value == 0;
    }

    private readonly record struct CountryCode(string Value) : IEntityKey<string>
    {
        public bool IsEmpty => string.IsNullOrEmpty(Value);
    }

    private readonly record struct WidgetId(Guid Value) : IEntityKey<Guid>
    {
        public bool IsEmpty => Value == Guid.Empty;
    }

    private readonly record struct PriceId(decimal Value) : IEntityKey<decimal>
    {
        public bool IsEmpty => Value == 0m;
    }
}
