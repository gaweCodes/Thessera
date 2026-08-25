using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EfCoreChildCollectionTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task ChildrenOfANewAggregate_ArePersistedAndReloadedWithTheirParent()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new BasketId(Guid.NewGuid());

        await MutateAsync(host, id, basket => basket.AddLine("salt", 1), create: true);

        var reloaded = await LoadAsync(host, id);

        Assert.Equal(2, reloaded.Lines.Count);
        Assert.Equal(["bread", "salt"], reloaded.Lines.Select(line => line.Label).Order());

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangedChild_KeepsItsRowAndItsIdentity()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new BasketId(Guid.NewGuid());

        await MutateAsync(host, id, basket => basket.AddLine("salt", 1), create: true);

        var lineId = (await LoadAsync(host, id)).Lines.Single(line => line.Label == "salt").Id;

        await MutateAsync(host, id, basket => basket.ChangeQuantity(lineId, 42));

        var reloaded = await LoadAsync(host, id);
        var changed = reloaded.Lines.Single(line => line.Id == lineId);

        Assert.Equal(42, changed.Quantity);
        Assert.Equal("salt", changed.Label);
        Assert.Equal(2, reloaded.Lines.Count);
        Assert.Equal(2, await CountRowsAsync(host, id));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChildRaisedChange_RoundTripsThroughTheOwnedGraph()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new BasketId(Guid.NewGuid());

        await MutateAsync(host, id, basket => basket.AddLine("salt", 1), create: true);

        var lineId = (await LoadAsync(host, id)).Lines.Single(line => line.Label == "salt").Id;

        await MutateAsync(host, id, basket => basket.Line(lineId).ChangeQuantity(42));

        var reloaded = await LoadAsync(host, id);

        Assert.Equal(42, reloaded.Line(lineId).Quantity);
        Assert.Equal("salt", reloaded.Line(lineId).Label);
        Assert.Equal(2, await CountRowsAsync(host, id));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RemovedChild_IsDeletedFromTheDatabase()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new BasketId(Guid.NewGuid());

        await MutateAsync(host, id, basket => basket.AddLine("salt", 1), create: true);

        var lineId = (await LoadAsync(host, id)).Lines.Single(line => line.Label == "salt").Id;

        await MutateAsync(host, id, basket => basket.RemoveLine(lineId));
        var reloaded = await LoadAsync(host, id);

        Assert.Equal(["bread"], reloaded.Lines.Select(line => line.Label));
        Assert.Equal(1, await CountRowsAsync(host, id));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChildOnlyChange_AdvancesTheAggregateVersion()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new BasketId(Guid.NewGuid());

        await MutateAsync(host, id, basket => basket.AddLine("salt", 1), create: true);

        var before = ((IStateOwner)await LoadAsync(host, id)).Version;

        await MutateAsync(host, id, basket => basket.AddLine("pepper", 3));

        var after = ((IStateOwner)await LoadAsync(host, id)).Version;

        Assert.Equal(before + 1, after);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConcurrentChildChanges_LetTheSecondCommitFailAsAConflict()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new BasketId(Guid.NewGuid());

        await MutateAsync(host, id, basket => basket.AddLine("salt", 1), create: true);

        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        var firstBasket = await LoadAsync(first, id);
        var secondBasket = await LoadAsync(second, id);

        firstBasket.AddLine("first", 1);
        secondBasket.AddLine("second", 1);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FixedSizeChildCollection_IsRejectedWithAnActionableMessage()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();

        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<ArrayBasket, BasketId>>();

        var thrown = await Assert.ThrowsAsync<NotSupportedException>(
            () => repository.AddAsync(
                ArrayBasket.Create(new BasketId(Guid.NewGuid())),
                TestContext.Current.CancellationToken));

        Assert.Contains("ArrayBasketState.Lines", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("ToList()", thrown.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StateWithNavigationToAnIndependentEntity_IsRejectedAtStartup()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(BasketOpened).Assembly)
                .UseEfCoreStateStore<LooseContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        using var host = builder.Build();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("LooseState.Owner", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("owned type", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StateWithAColumnNameLeftToConvention_IsRejectedAtStartup()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(BasketOpened).Assembly)
                .UseEfCoreStateStore<DerivedNameContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        using var host = builder.Build();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("DerivedNameState.Label", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("HasColumnName", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullChildCollection_IsRejectedWithAnActionableMessage()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();

        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<NullBasket, BasketId>>();

        var thrown = await Assert.ThrowsAsync<NotSupportedException>(
            () => repository.AddAsync(
                NullBasket.Create(new BasketId(Guid.NewGuid())),
                TestContext.Current.CancellationToken));

        Assert.Contains("NullBasketState.Lines", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("is null", thrown.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NestedChildren_AreInsertedUpdatedAndDeletedThroughTheirGrandparent()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CartId(Guid.NewGuid());

        CartLineId lineId = default;
        CartTagId tagId = default;

        await MutateCartAsync(host, id, cart =>
        {
            lineId = cart.AddLine("bread");
            tagId = cart.Tag(lineId, "fresh");
        });

        var created = await LoadCartAsync(host, id);

        Assert.Equal(["fresh"], created.Lines.Single().Tags.Select(tag => tag.Name));
        Assert.Equal(1, await CountTagsAsync(host, lineId));

        await MutateCartAsync(host, id, cart =>
        {
            cart.RenameTag(lineId, tagId, "stale");
            cart.Tag(lineId, "second");
        });

        var changed = await LoadCartAsync(host, id);

        Assert.Equal(["second", "stale"], changed.Lines.Single().Tags.Select(tag => tag.Name).Order());
        Assert.Equal(2, await CountTagsAsync(host, lineId));

        await MutateCartAsync(host, id, cart => cart.RemoveTag(lineId, tagId));

        var reduced = await LoadCartAsync(host, id);

        Assert.Equal(["second"], reduced.Lines.Single().Tags.Select(tag => tag.Name));
        Assert.Equal(1, await CountTagsAsync(host, lineId));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FixedSizeCollectionInsideANestedChild_IsRejectedWithAnActionableMessage()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CartId(Guid.NewGuid());

        CartLineId lineId = default;

        await MutateCartAsync(host, id, cart => lineId = cart.AddLine("bread"));

        using var scope = host.Services.CreateScope();
        var cart = await LoadCartAsync(scope, id);

        cart.TagUnsafely(lineId, "fresh");

        var thrown = await Assert.ThrowsAsync<NotSupportedException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .CommitAsync(TestContext.Current.CancellationToken));

        Assert.Contains("CartLine.Tags", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("ToList()", thrown.Message, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OwnedSingleChild_IsUpdatedAndCanBeCleared()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CartId(Guid.NewGuid());

        await MutateCartAsync(host, id, cart => cart.AddLine("bread"));

        Assert.Equal("Bern", (await LoadCartAsync(host, id)).Address?.City);

        await MutateCartAsync(host, id, cart => cart.MoveTo("Zurich"));

        Assert.Equal("Zurich", (await LoadCartAsync(host, id)).Address?.City);

        await MutateCartAsync(host, id, cart => cart.ClearAddress());

        Assert.Null((await LoadCartAsync(host, id)).Address);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task JsonChildCollection_RoundTripsThroughItsColumn()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        using var host = await StartHostAsync();
        var id = new CartId(Guid.NewGuid());

        await MutateCartAsync(host, id, cart => cart.AddNote("first"));
        await MutateCartAsync(host, id, cart => cart.AddNote("second"));

        var reloaded = await LoadCartAsync(host, id);

        Assert.Equal(["first", "second"], reloaded.Notes.Select(note => note.Text));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Basket> LoadAsync(IHost host, BasketId id)
    {
        using var scope = host.Services.CreateScope();
        return await LoadAsync(scope, id);
    }

    private static async Task<Basket> LoadAsync(IServiceScope scope, BasketId id)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Basket, BasketId>>();
        var basket = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(basket);
        return basket!;
    }

    private static async Task MutateAsync(IHost host, BasketId id, Action<Basket> mutate, bool create = false)
    {
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Basket, BasketId>>();

        Basket basket;

        if (create)
        {
            basket = Basket.Open(id, "bread");
            await repository.AddAsync(basket, TestContext.Current.CancellationToken);
        }
        else
        {
            basket = await LoadAsync(scope, id);
        }

        mutate(basket);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);
    }

    private static Task<int> CountRowsAsync(IHost host, BasketId id) =>
        CountAsync(host, $"select count(*)::int as \"Value\" from basket_lines where basket_id = {id.Value}");

    private static Task<int> CountTagsAsync(IHost host, CartLineId lineId) =>
        CountAsync(host, $"select count(*)::int as \"Value\" from cart_line_tags where cart_line_id = {lineId.Value}");

    private static async Task<int> CountAsync(IHost host, FormattableString sql)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BasketContext>();

        return await context.Database.SqlQuery<int>(sql).SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Cart> LoadCartAsync(IHost host, CartId id)
    {
        using var scope = host.Services.CreateScope();
        return await LoadCartAsync(scope, id);
    }

    private static async Task<Cart> LoadCartAsync(IServiceScope scope, CartId id)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Cart, CartId>>();
        var cart = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(cart);
        return cart!;
    }

    private static async Task MutateCartAsync(IHost host, CartId id, Action<Cart> mutate)
    {
        using var scope = host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Cart, CartId>>();
        var cart = await repository.GetByIdAsync(id, TestContext.Current.CancellationToken);

        if (cart is null)
        {
            cart = Cart.Open(id, "Bern");
            await repository.AddAsync(cart, TestContext.Current.CancellationToken);
        }

        mutate(cart);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(
            options => options
                .AddDomainEventsFrom(typeof(BasketOpened).Assembly)
                .UseEfCoreStateStore<BasketContext>(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)
                .CustomizeWolverine(wolverine =>
                {
                    wolverine.Durability.Mode = DurabilityMode.Solo;
                    wolverine.ApplicationAssembly = typeof(DomainEventEnvelopeHandler).Assembly;
                }));

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BasketContext>();

        await context.Database.ExecuteSqlRawAsync(
            "create table if not exists baskets (id uuid primary key, version bigint not null);"
            + "create table if not exists array_baskets (id uuid primary key, version bigint not null);"
            + "create table if not exists null_baskets (id uuid primary key, version bigint not null);"
            + "create table if not exists basket_lines (id uuid primary key, basket_id uuid not null, "
            + "label text not null, quantity integer not null);"
            + "create table if not exists array_basket_lines (id uuid primary key, basket_id uuid not null, "
            + "label text not null, quantity integer not null);"
            + "create table if not exists null_basket_lines (id uuid primary key, basket_id uuid not null, "
            + "label text not null, quantity integer not null);"
            + "create table if not exists carts (id uuid primary key, version bigint not null, "
            + "address_city text, notes jsonb);"
            + "create table if not exists cart_lines (id uuid primary key, cart_id uuid not null, "
            + "label text not null);"
            + "create table if not exists cart_line_tags (id uuid primary key, cart_line_id uuid not null, "
            + "name text not null);",
            TestContext.Current.CancellationToken);

        return host;
    }
}
