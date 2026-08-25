using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GaWeCodes.Thessera.Tests;

public sealed class EntityKeyConversionTests
{
    [Fact]
    public void EntityKeyConversion_ConvertsBetweenKeyAndPrimitive()
    {
        using var context = new SampleContext();
        var converter = context.Model
            .FindEntityType(typeof(RecipeRow))!
            .FindProperty(nameof(RecipeRow.Id))!
            .GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(5, Assert.IsType<int>(converter.ConvertToProvider(new RecipeId(5))));
        Assert.Equal(new RecipeId(5), Assert.IsType<RecipeId>(converter.ConvertFromProvider(5)));
    }

    [Fact]
    public void ApplyEntityKeyConversions_ConfiguresConverterForMappedKeyProperties()
    {
        using var context = new SampleContext();

        var converter = context.Model
            .FindEntityType(typeof(RecipeRow))!
            .FindProperty(nameof(RecipeRow.Id))!
            .GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(9, Assert.IsType<int>(converter.ConvertToProvider(new RecipeId(9))));
        Assert.Equal(new RecipeId(9), Assert.IsType<RecipeId>(converter.ConvertFromProvider(9)));
    }

    [Fact]
    public void ApplyEntityKeyConversions_ConfiguresConverterForOwnedChildKeys()
    {
        using var context = new SampleContext();

        var converter = context.Model
            .FindEntityType(typeof(RecipeRow))!
            .FindNavigation(nameof(RecipeRow.Steps))!
            .TargetEntityType
            .FindProperty(nameof(RecipeStepRow.Id))!
            .GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(3, Assert.IsType<int>(converter.ConvertToProvider(new RecipeStepId(3))));
        Assert.Equal(new RecipeStepId(3), Assert.IsType<RecipeStepId>(converter.ConvertFromProvider(3)));
    }

    [Fact]
    public void ApplyEntityKeyConversions_ConfiguresConverterForComplexTypeKeys()
    {
        using var context = new SampleContext();

        var converter = context.Model
            .FindEntityType(typeof(SummaryRow))!
            .FindComplexProperty(nameof(SummaryRow.Author))!
            .ComplexType
            .FindProperty(nameof(AuthorInfo.UserId))!
            .GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(11, Assert.IsType<int>(converter.ConvertToProvider(new RecipeId(11))));
        Assert.Equal(new RecipeId(11), Assert.IsType<RecipeId>(converter.ConvertFromProvider(11)));
    }

    [Fact]
    public void ApplyEntityKeyConversions_ConfiguresConverterForNestedComplexTypeKeys()
    {
        using var context = new SampleContext();

        var converter = context.Model
            .FindEntityType(typeof(SummaryRow))!
            .FindComplexProperty(nameof(SummaryRow.Author))!
            .ComplexType
            .FindComplexProperty(nameof(AuthorInfo.Audit))!
            .ComplexType
            .FindProperty(nameof(AuditInfo.ChangedBy))!
            .GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(7, Assert.IsType<int>(converter.ConvertToProvider(new RecipeId(7))));
        Assert.Equal(new RecipeId(7), Assert.IsType<RecipeId>(converter.ConvertFromProvider(7)));
    }

    [Fact]
    public void ApplyEntityKeyConversions_LeavesAlreadyConfiguredPropertiesUntouched()
    {
        using var context = new SampleContext();

        var converter = context.Model
            .FindEntityType(typeof(TaggedRow))!
            .FindProperty(nameof(TaggedRow.Reference))!
            .GetValueConverter();

        Assert.IsType<CustomReferenceConverter>(converter);
    }

    [Fact]
    public void ApplyEntityKeyConversions_TouchesNothingTheModelDoesNotMap()
    {
        using var context = new SampleContext();

        var entityType = context.Model.FindEntityType(typeof(TaggedRow))!;

        Assert.Null(entityType.FindProperty(nameof(TaggedRow.IgnoredReference)));
        Assert.Null(entityType.FindProperty(nameof(TaggedRow.ComputedReference)));
    }

    [Fact]
    public void AnUnmappedKeyProperty_FailsLoudlyInsteadOfBeingDiscovered()
    {
        using var context = new UnmappedContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.Model);

        Assert.Contains(nameof(RecipeRow.Id), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RecipeId), exception.Message, StringComparison.Ordinal);
    }

    private sealed class SampleContext : DbContext
    {
        public DbSet<RecipeRow> Recipes => Set<RecipeRow>();

        public DbSet<TaggedRow> Tagged => Set<TaggedRow>();

        public DbSet<SummaryRow> Summaries => Set<SummaryRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseNpgsql("Host=localhost;Database=sample");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RecipeRow>(entity =>
            {
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id);
                entity.Property(row => row.Name);

                entity.OwnsMany(row => row.Steps, steps =>
                {
                    steps.HasKey(step => step.Id);
                    steps.Property(step => step.Id);
                    steps.Property(step => step.Label);
                });
            });

            modelBuilder.Entity<SummaryRow>(entity =>
            {
                entity.HasKey(row => row.Id);

                entity.ComplexProperty(row => row.Author, author =>
                {
                    author.Property(info => info.UserId);
                    author.Property(info => info.Name);

                    author.ComplexProperty(info => info.Audit, audit =>
                        audit.Property(info => info.ChangedBy));
                });
            });

            modelBuilder.Entity<TaggedRow>(entity =>
            {
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Reference).HasConversion(new CustomReferenceConverter());
                entity.Ignore(row => row.IgnoredReference);
            });

            modelBuilder.ApplyEntityKeyConversions();
        }
    }

    private sealed class UnmappedContext : DbContext
    {
        public DbSet<RecipeRow> Recipes => Set<RecipeRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseNpgsql("Host=localhost;Database=unmapped");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RecipeRow>().Ignore(row => row.Steps);
            modelBuilder.ApplyEntityKeyConversions();
        }
    }

    private sealed class RecipeRow
    {
        public RecipeId Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public IReadOnlyCollection<RecipeStepRow> Steps { get; init; } = new List<RecipeStepRow>();
    }

    private sealed class RecipeStepRow
    {
        public RecipeStepId Id { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class TaggedRow
    {
        public int Id { get; set; }

        public RecipeId Reference { get; set; }

        public RecipeId IgnoredReference { get; set; }

        public RecipeId ComputedReference => new(Reference.Value + 1);
    }

    private sealed class SummaryRow
    {
        public int Id { get; set; }

        public AuthorInfo Author { get; set; } = new();
    }

    private sealed class AuthorInfo
    {
        public RecipeId UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public AuditInfo Audit { get; set; } = new();
    }

    private sealed class AuditInfo
    {
        public RecipeId ChangedBy { get; set; }
    }

    private sealed class CustomReferenceConverter() : ValueConverter<RecipeId, int>(
        key => key.Value,
        value => new RecipeId(value));

    private readonly record struct RecipeId(int Value) : IEntityKey<int>
    {
        public bool IsEmpty => Value == 0;
    }

    private readonly record struct RecipeStepId(int Value) : IEntityKey<int>
    {
        public bool IsEmpty => Value == 0;
    }
}
