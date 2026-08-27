using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace StateStoredWithMessaging;

public sealed class ReadingDbContext(DbContextOptions<ReadingDbContext> options) : DbContext(options)
{
    public DbSet<ReadingState> Readings => Set<ReadingState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ReadingState>(entity =>
        {
            entity.ToTable("readings");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Value).HasColumnName("value");
            entity.Property(state => state.CreatedAt).HasColumnName("created_at");
            entity.Property(state => state.UpdatedAt).HasColumnName("updated_at");
            entity.Property(state => state.IsDeleted).HasColumnName("is_deleted");
            entity.Property(state => state.DeletedAt).HasColumnName("deleted_at");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
