using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace MixedPersistenceWithMessaging;

public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<AccountState> Accounts => Set<AccountState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AccountState>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Balance).HasColumnName("balance");
            entity.Property(state => state.OpenedAt).HasColumnName("opened_at");
            entity.Property(state => state.IsClosed).HasColumnName("is_closed");
            entity.Property(state => state.ClosedAt).HasColumnName("closed_at");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
