using GaWeCodes.Thessera.Core.DependencyInjection;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MartenEventAliasTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task TheEventStore_StoresTheDeclaredEventNameNotTheClrTypeName()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var services = new ServiceCollection();
        services.AddThessera(options => options
            .AddDomainEventsFrom(typeof(FlushCounterCreated).Assembly)
            .UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup));

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>();
        var streamKey = "flush-counter/" + Guid.NewGuid();

        await using (var session = store.LightweightSession())
        {
            session.Events.Append(streamKey, new FlushCounterCreated(new FlushCounterId(Guid.NewGuid())));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = store.LightweightSession();
        var stream = await reader.Events.FetchStreamAsync(streamKey, token: TestContext.Current.CancellationToken);

        var stored = Assert.Single(stream);
        Assert.Equal("flush-counter-created-v1", stored.EventTypeName);
        Assert.DoesNotContain(
            nameof(FlushCounterCreated),
            stored.EventTypeName,
            StringComparison.OrdinalIgnoreCase);
    }
}
