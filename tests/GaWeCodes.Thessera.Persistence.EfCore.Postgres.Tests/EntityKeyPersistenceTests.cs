using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EntityKeyPersistenceTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task StronglyTypedKey_RoundTripsThroughPostgres()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var schema = "recipes_" + Guid.NewGuid().ToString("N")[..8];
        var options = new DbContextOptionsBuilder<RecipeContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        var id = new RecipeId(Guid.NewGuid());

        await using (var context = new RecipeContext(options, schema))
        {
            await context.GetService<IRelationalDatabaseCreator>()
                .CreateTablesAsync(TestContext.Current.CancellationToken);
            context.Recipes.Add(new RecipeRow { Id = id, Name = "Pasta" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = new RecipeContext(options, schema))
        {
            var found = await context.Recipes.FindAsync([id], TestContext.Current.CancellationToken);

            Assert.NotNull(found);
            Assert.Equal(id, found!.Id);
            Assert.Equal("Pasta", found.Name);
        }
    }

    private sealed class RecipeContext(DbContextOptions<RecipeContext> options, string schema) : DbContext(options)
    {
        public DbSet<RecipeRow> Recipes => Set<RecipeRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(schema);

            modelBuilder.Entity<RecipeRow>(entity =>
            {
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id);
                entity.Property(row => row.Name);
            });

            modelBuilder.ApplyEntityKeyConversions();
        }
    }

    private sealed class RecipeRow
    {
        public RecipeId Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private readonly record struct RecipeId(Guid Value) : IEntityKey<Guid>
    {
        public bool IsEmpty => Value == Guid.Empty;
    }
}
