using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Tests;

public readonly record struct CrateId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public readonly record struct CrateItemId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public readonly record struct CrateTagId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record CrateTag(CrateTagId Id, string Name);

public sealed record CrateItem(CrateItemId Id, string Label, int Quantity)
{
    public IReadOnlyCollection<CrateTag> Tags { get; init; } = new List<CrateTag>();
}

[EventName("crate-opened-v1")]
public sealed record CrateOpened(CrateId CrateId, CrateItemId ItemId, string Label) : DomainEvent;

[EventName("crate-item-added-v1")]
public sealed record CrateItemAdded(CrateItemId ItemId, string Label, int Quantity) : DomainEvent;

[EventName("crate-item-quantity-changed-v1")]
public sealed record CrateItemQuantityChanged(CrateItemId ItemId, int Quantity) : DomainEvent;

[EventName("crate-item-removed-v1")]
public sealed record CrateItemRemoved(CrateItemId ItemId) : DomainEvent;

[EventName("crate-item-tagged-v1")]
public sealed record CrateItemTagged(CrateItemId ItemId, CrateTagId TagId, string Name) : DomainEvent;

public sealed record CrateState(CrateId Id) : AggregateState<CrateState, CrateId>
{
    public IReadOnlyCollection<CrateItem> Items { get; init; } = new List<CrateItem>();

    public static CrateState Empty => new(default(CrateId));

    public override CrateState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        CrateOpened opened => this with
        {
            Id = opened.CrateId,
            Items = Items.Append(new CrateItem(opened.ItemId, opened.Label, 1)).ToList(),
        },
        CrateItemAdded added => this with
        {
            Items = Items.Append(new CrateItem(added.ItemId, added.Label, added.Quantity)).ToList(),
        },
        CrateItemQuantityChanged changed => this with
        {
            Items = MapItem(changed.ItemId, item => item with { Quantity = changed.Quantity }),
        },
        CrateItemRemoved removed => this with
        {
            Items = Items.Where(item => item.Id != removed.ItemId).ToList(),
        },
        CrateItemTagged tagged => this with
        {
            Items = MapItem(
                tagged.ItemId,
                item => item with { Tags = item.Tags.Append(new CrateTag(tagged.TagId, tagged.Name)).ToList() }),
        },
        _ => this,
    };

    private IReadOnlyCollection<CrateItem> MapItem(CrateItemId itemId, Func<CrateItem, CrateItem> map) =>
        Items.Select(item => item.Id == itemId ? map(item) : item).ToList();
}

[AggregateName("crate")]
public sealed class Crate : AggregateRoot<CrateId, CrateState>
{
    private Crate() : base(CrateState.Empty)
    {
    }

    public IReadOnlyCollection<CrateItem> Items => State.Items;

    public static Crate Open(CrateId id, string label)
    {
        var crate = new Crate();
        crate.RaiseEvent(new CrateOpened(id, new CrateItemId(Guid.NewGuid()), label));
        return crate;
    }

    public CrateItemId AddItem(string label, int quantity)
    {
        var itemId = new CrateItemId(Guid.NewGuid());
        RaiseEvent(new CrateItemAdded(itemId, label, quantity));
        return itemId;
    }

    public void ChangeQuantity(CrateItemId itemId, int quantity) =>
        RaiseEvent(new CrateItemQuantityChanged(itemId, quantity));

    public void RemoveItem(CrateItemId itemId) => RaiseEvent(new CrateItemRemoved(itemId));

    public CrateTagId Tag(CrateItemId itemId, string name)
    {
        var tagId = new CrateTagId(Guid.NewGuid());
        RaiseEvent(new CrateItemTagged(itemId, tagId, name));
        return tagId;
    }
}

public sealed class CrateContext(DbContextOptions<CrateContext> options) : DbContext(options)
{
    public DbSet<CrateState> Crates => Set<CrateState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<CrateState>(entity =>
        {
            entity.ToTable("crates");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            entity.OwnsMany(state => state.Items, items =>
            {
                items.ToTable("crate_items");
                items.WithOwner().HasForeignKey("crate_id");
                items.HasKey(item => item.Id);
                items.Property(item => item.Id).HasColumnName("id");
                items.Property(item => item.Label).HasColumnName("label");
                items.Property(item => item.Quantity).HasColumnName("quantity");

                items.OwnsMany(item => item.Tags, tags =>
                {
                    tags.ToTable("crate_item_tags");
                    tags.WithOwner().HasForeignKey("crate_item_id");
                    tags.HasKey(tag => tag.Id);
                    tags.Property(tag => tag.Id).HasColumnName("id");
                    tags.Property(tag => tag.Name).HasColumnName("name");
                });
            });
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
