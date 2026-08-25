using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Tests;

public readonly record struct BasketId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public readonly record struct BasketLineId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record BasketLine(BasketLineId Id, string Label, int Quantity)
    : EntityState<BasketLine, BasketLineId>
{
    public override BasketLine Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        BasketLineQuantityChanged changed => this with { Quantity = changed.Quantity },
        _ => this,
    };
}

public sealed class BasketLineEntity : Entity<BasketLineId, BasketLine>
{
    internal BasketLineEntity(Basket basket, BasketLineId id)
        : base(basket, id)
    {
    }

    public string Label => GetCurrentState().Label;

    public int Quantity => GetCurrentState().Quantity;

    public void ChangeQuantity(int quantity) => RaiseEvent(new BasketLineQuantityChanged(Id, quantity));
}

[EventName("basket-opened-v1")]
public sealed record BasketOpened(BasketId BasketId, BasketLineId LineId, string Label) : DomainEvent;

[EventName("basket-line-added-v1")]
public sealed record BasketLineAdded(BasketLineId LineId, string Label, int Quantity) : DomainEvent;

[EventName("basket-line-quantity-changed-v1")]
public sealed record BasketLineQuantityChanged(BasketLineId LineId, int Quantity) : DomainEvent;

[EventName("basket-line-removed-v1")]
public sealed record BasketLineRemoved(BasketLineId LineId) : DomainEvent;

[EventName("array-basket-opened-v1")]
public sealed record ArrayBasketOpened(BasketId BasketId, string Label) : DomainEvent;

public sealed record BasketState(BasketId Id) : AggregateState<BasketState, BasketId>
{
    public IReadOnlyCollection<BasketLine> Lines { get; init; } = new List<BasketLine>();

    public static BasketState Empty => new(default(BasketId));

    public override BasketState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        BasketOpened opened => this with
        {
            Id = opened.BasketId,
            Lines = Lines.Append(new BasketLine(opened.LineId, opened.Label, 1)).ToList(),
        },
        BasketLineAdded added => this with
        {
            Lines = Lines.Append(new BasketLine(added.LineId, added.Label, added.Quantity)).ToList(),
        },
        BasketLineQuantityChanged changed => this with
        {
            Lines = Lines
                .Select(line => line.Id == changed.LineId ? line.Apply(changed) : line)
                .ToList(),
        },
        BasketLineRemoved removed => this with
        {
            Lines = Lines.Where(line => line.Id != removed.LineId).ToList(),
        },
        _ => this,
    };
}

[AggregateName("basket")]
public sealed class Basket : AggregateRoot<BasketId, BasketState>,
    IChildOwner<BasketLineId, BasketLine>
{
    private Basket() : base(BasketState.Empty)
    {
    }

    public IReadOnlyCollection<BasketLine> Lines => State.Lines;

    public static Basket Open(BasketId id, string label)
    {
        var basket = new Basket();
        basket.RaiseEvent(new BasketOpened(id, new BasketLineId(Guid.NewGuid()), label));
        return basket;
    }

    public BasketLineId AddLine(string label, int quantity)
    {
        var lineId = new BasketLineId(Guid.NewGuid());
        RaiseEvent(new BasketLineAdded(lineId, label, quantity));
        return lineId;
    }

    public void ChangeQuantity(BasketLineId lineId, int quantity) =>
        RaiseEvent(new BasketLineQuantityChanged(lineId, quantity));

    public void RemoveLine(BasketLineId lineId) => RaiseEvent(new BasketLineRemoved(lineId));

    public BasketLineEntity Line(BasketLineId lineId) => new(this, lineId);

    internal BasketLine? FindLine(BasketLineId lineId) =>
        State.Lines.FirstOrDefault(line => line.Id == lineId);

    BasketLine? IChildOwner<BasketLineId, BasketLine>.FindChild(BasketLineId childId) =>
        FindLine(childId);
}

public sealed record ArrayBasketState(BasketId Id) : AggregateState<ArrayBasketState, BasketId>
{
    public IReadOnlyCollection<BasketLine> Lines { get; init; } = new List<BasketLine>();

    public static ArrayBasketState Empty => new(default(BasketId));

    public override ArrayBasketState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ArrayBasketOpened opened => this with
        {
            Id = opened.BasketId,
            Lines = new[] { new BasketLine(new BasketLineId(Guid.NewGuid()), opened.Label, 1) },
        },
        _ => this,
    };
}

[AggregateName("array-basket")]
public sealed class ArrayBasket : AggregateRoot<BasketId, ArrayBasketState>
{
    private ArrayBasket() : base(ArrayBasketState.Empty)
    {
    }

    public static ArrayBasket Create(BasketId id)
    {
        var basket = new ArrayBasket();
        basket.RaiseEvent(new ArrayBasketOpened(id, "bread"));
        return basket;
    }
}

public readonly record struct CartId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public readonly record struct CartLineId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public readonly record struct CartTagId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record CartTag(CartTagId Id, string Name);

public sealed record CartLine(CartLineId Id, string Label)
{
    public IReadOnlyCollection<CartTag> Tags { get; init; } = new List<CartTag>();
}

public sealed record CartAddress(string City);

public sealed record CartNote(string Text);

[EventName("cart-opened-v1")]
public sealed record CartOpened(CartId CartId, string City) : DomainEvent;

[EventName("cart-line-added-v1")]
public sealed record CartLineAdded(CartLineId LineId, string Label) : DomainEvent;

[EventName("cart-line-tagged-v1")]
public sealed record CartLineTagged(CartLineId LineId, CartTagId TagId, string Name) : DomainEvent;

[EventName("cart-line-tagged-unsafely-v1")]
public sealed record CartLineTaggedUnsafely(CartLineId LineId, CartTagId TagId, string Name) : DomainEvent;

[EventName("cart-tag-renamed-v1")]
public sealed record CartTagRenamed(CartLineId LineId, CartTagId TagId, string Name) : DomainEvent;

[EventName("cart-tag-removed-v1")]
public sealed record CartTagRemoved(CartLineId LineId, CartTagId TagId) : DomainEvent;

[EventName("cart-note-added-v1")]
public sealed record CartNoteAdded(string Text) : DomainEvent;

[EventName("cart-address-changed-v1")]
public sealed record CartAddressChanged(string City) : DomainEvent;

[EventName("cart-address-cleared-v1")]
public sealed record CartAddressCleared : DomainEvent;

public sealed record CartState(CartId Id) : AggregateState<CartState, CartId>
{
    public CartAddress? Address { get; init; }

    public IReadOnlyCollection<CartLine> Lines { get; init; } = new List<CartLine>();

    public IReadOnlyCollection<CartNote> Notes { get; init; } = new List<CartNote>();

    public static CartState Empty => new(default(CartId));

    public override CartState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        CartOpened opened => this with { Id = opened.CartId, Address = new CartAddress(opened.City) },
        CartLineAdded added => this with
        {
            Lines = Lines.Append(new CartLine(added.LineId, added.Label)).ToList(),
        },
        CartLineTagged tagged => this with
        {
            Lines = MapLine(
                tagged.LineId,
                line => line with { Tags = line.Tags.Append(new CartTag(tagged.TagId, tagged.Name)).ToList() }),
        },
        CartLineTaggedUnsafely tagged => this with
        {
            Lines = MapLine(
                tagged.LineId,
                line => line with { Tags = new[] { new CartTag(tagged.TagId, tagged.Name) } }),
        },
        CartTagRenamed renamed => this with
        {
            Lines = MapLine(
                renamed.LineId,
                line => line with
                {
                    Tags = line.Tags
                        .Select(tag => tag.Id == renamed.TagId ? tag with { Name = renamed.Name } : tag)
                        .ToList(),
                }),
        },
        CartTagRemoved removed => this with
        {
            Lines = MapLine(
                removed.LineId,
                line => line with { Tags = line.Tags.Where(tag => tag.Id != removed.TagId).ToList() }),
        },
        CartNoteAdded added => this with { Notes = Notes.Append(new CartNote(added.Text)).ToList() },
        CartAddressChanged changed => this with { Address = new CartAddress(changed.City) },
        CartAddressCleared _ => this with { Address = null },
        _ => this,
    };

    private IReadOnlyCollection<CartLine> MapLine(CartLineId lineId, Func<CartLine, CartLine> map) =>
        Lines.Select(line => line.Id == lineId ? map(line) : line).ToList();
}

[AggregateName("cart")]
public sealed class Cart : AggregateRoot<CartId, CartState>
{
    private Cart() : base(CartState.Empty)
    {
    }

    public CartAddress? Address => State.Address;

    public IReadOnlyCollection<CartLine> Lines => State.Lines;

    public IReadOnlyCollection<CartNote> Notes => State.Notes;

    public static Cart Open(CartId id, string city)
    {
        var cart = new Cart();
        cart.RaiseEvent(new CartOpened(id, city));
        return cart;
    }

    public CartLineId AddLine(string label)
    {
        var lineId = new CartLineId(Guid.NewGuid());
        RaiseEvent(new CartLineAdded(lineId, label));
        return lineId;
    }

    public CartTagId Tag(CartLineId lineId, string name)
    {
        var tagId = new CartTagId(Guid.NewGuid());
        RaiseEvent(new CartLineTagged(lineId, tagId, name));
        return tagId;
    }

    public void TagUnsafely(CartLineId lineId, string name) =>
        RaiseEvent(new CartLineTaggedUnsafely(lineId, new CartTagId(Guid.NewGuid()), name));

    public void RenameTag(CartLineId lineId, CartTagId tagId, string name) =>
        RaiseEvent(new CartTagRenamed(lineId, tagId, name));

    public void RemoveTag(CartLineId lineId, CartTagId tagId) => RaiseEvent(new CartTagRemoved(lineId, tagId));

    public void AddNote(string text) => RaiseEvent(new CartNoteAdded(text));

    public void MoveTo(string city) => RaiseEvent(new CartAddressChanged(city));

    public void ClearAddress() => RaiseEvent(new CartAddressCleared());
}

public sealed class BasketContext(DbContextOptions<BasketContext> options) : DbContext(options)
{
    public DbSet<BasketState> Baskets => Set<BasketState>();

    public DbSet<ArrayBasketState> ArrayBaskets => Set<ArrayBasketState>();

    public DbSet<NullBasketState> NullBaskets => Set<NullBasketState>();

    public DbSet<CartState> Carts => Set<CartState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<BasketState>(entity =>
        {
            entity.ToTable("baskets");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            entity.OwnsMany(state => state.Lines, lines =>
            {
                lines.ToTable("basket_lines");
                lines.WithOwner().HasForeignKey("basket_id");
                lines.HasKey(line => line.Id);
                lines.Property(line => line.Id).HasColumnName("id");
                lines.Property(line => line.Label).HasColumnName("label");
                lines.Property(line => line.Quantity).HasColumnName("quantity");
            });
        });

        modelBuilder.Entity<ArrayBasketState>(entity =>
        {
            entity.ToTable("array_baskets");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            entity.OwnsMany(state => state.Lines, lines =>
            {
                lines.ToTable("array_basket_lines");
                lines.WithOwner().HasForeignKey("basket_id");
                lines.HasKey(line => line.Id);
                lines.Property(line => line.Id).HasColumnName("id");
                lines.Property(line => line.Label).HasColumnName("label");
                lines.Property(line => line.Quantity).HasColumnName("quantity");
            });
        });

        modelBuilder.Entity<NullBasketState>(entity =>
        {
            entity.ToTable("null_baskets");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            entity.OwnsMany(state => state.Lines, lines =>
            {
                lines.ToTable("null_basket_lines");
                lines.WithOwner().HasForeignKey("basket_id");
                lines.HasKey(line => line.Id);
                lines.Property(line => line.Id).HasColumnName("id");
                lines.Property(line => line.Label).HasColumnName("label");
                lines.Property(line => line.Quantity).HasColumnName("quantity");
            });
        });

        modelBuilder.Entity<CartState>(entity =>
        {
            entity.ToTable("carts");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();

            entity.OwnsOne(state => state.Address, address =>
                address.Property(value => value.City).HasColumnName("address_city"));

            entity.OwnsMany(state => state.Notes, notes =>
            {
                notes.ToJson("notes");
                notes.Property(note => note.Text).HasJsonPropertyName("text");
            });

            entity.OwnsMany(state => state.Lines, lines =>
            {
                lines.ToTable("cart_lines");
                lines.WithOwner().HasForeignKey("cart_id");
                lines.HasKey(line => line.Id);
                lines.Property(line => line.Id).HasColumnName("id");
                lines.Property(line => line.Label).HasColumnName("label");

                lines.OwnsMany(line => line.Tags, tags =>
                {
                    tags.ToTable("cart_line_tags");
                    tags.WithOwner().HasForeignKey("cart_line_id");
                    tags.HasKey(tag => tag.Id);
                    tags.Property(tag => tag.Id).HasColumnName("id");
                    tags.Property(tag => tag.Name).HasColumnName("name");
                });
            });
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}

[EventName("null-basket-opened-v1")]
public sealed record NullBasketOpened(BasketId BasketId) : DomainEvent;

public sealed record NullBasketState(BasketId Id) : AggregateState<NullBasketState, BasketId>
{
    public IReadOnlyCollection<BasketLine> Lines { get; init; } = new List<BasketLine>();

    public static NullBasketState Empty => new(default(BasketId));

    public override NullBasketState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        NullBasketOpened opened => this with { Id = opened.BasketId, Lines = null! },
        _ => this,
    };
}

[AggregateName("null-basket")]
public sealed class NullBasket : AggregateRoot<BasketId, NullBasketState>
{
    private NullBasket() : base(NullBasketState.Empty)
    {
    }

    public static NullBasket Create(BasketId id)
    {
        var basket = new NullBasket();
        basket.RaiseEvent(new NullBasketOpened(id));
        return basket;
    }
}

public sealed record LooseState(BasketId Id) : AggregateState<LooseState, BasketId>
{
    public LooseOwner? Owner { get; init; }

    public static LooseState Empty => new(default(BasketId));

    public override LooseState Apply(IDomainEvent domainEvent) => this;
}

public sealed class LooseOwner
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class LooseContext(DbContextOptions<LooseContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<LooseOwner>(entity =>
        {
            entity.ToTable("loose_owners");
            entity.HasKey(owner => owner.Id);
        });

        modelBuilder.Entity<LooseState>(entity =>
        {
            entity.ToTable("loose_states");
            entity.HasKey(state => state.Id);
            entity.HasOne(state => state.Owner).WithMany();
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}

public sealed record DerivedNameState(BasketId Id) : AggregateState<DerivedNameState, BasketId>
{
    public string Label { get; init; } = string.Empty;

    public static DerivedNameState Empty => new(default(BasketId));

    public override DerivedNameState Apply(IDomainEvent domainEvent) => this;
}

public sealed class DerivedNameContext(DbContextOptions<DerivedNameContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<DerivedNameState>(entity =>
        {
            entity.ToTable("derived_names");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.Version).HasColumnName("version").IsConcurrencyToken();
            entity.Property(state => state.Label);
        });

        modelBuilder.ApplyEntityKeyConversions();
    }
}
