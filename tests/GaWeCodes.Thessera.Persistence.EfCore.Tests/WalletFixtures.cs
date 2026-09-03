using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Tests;

public readonly record struct WalletId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record Money(string Currency, decimal Amount);

[EventName("wallet-opened-v1")]
public sealed record WalletOpened(WalletId WalletId, string Currency, decimal Amount) : DomainEvent;

public sealed record WalletState(WalletId Id) : AggregateState<WalletState, WalletId>
{
    public Money Balance { get; init; } = new Money("USD", 0m);

    public static WalletState Empty => new(default(WalletId));

    public override WalletState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        WalletOpened opened => this with { Id = opened.WalletId, Balance = new Money(opened.Currency, opened.Amount) },
        _ => this,
    };
}

[AggregateName("wallet")]
public sealed class Wallet : AggregateRoot<WalletId, WalletState>
{
    private Wallet() : base(WalletState.Empty)
    {
    }

    public static Wallet Open(WalletId id, string currency, decimal amount)
    {
        var wallet = new Wallet();
        wallet.RaiseEvent(new WalletOpened(id, currency, amount));
        return wallet;
    }
}

public sealed class WalletContextWithUndeclaredColumnName(DbContextOptions<WalletContextWithUndeclaredColumnName> options)
    : DbContext(options)
{
    public DbSet<WalletState> Wallets => Set<WalletState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<WalletState>(entity =>
        {
            entity.ToTable("wallets");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            entity.ComplexProperty(
                state => state.Balance,
                money => money.Property(m => m.Currency).HasColumnName("currency"));
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
