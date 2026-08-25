using GaWeCodes.Thessera.Core.DependencyInjection;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GaWeCodes.Thessera.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EntityKeyEventStreamFormatTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task TheEventStore_StoresATypedKeyAsABareValue_NotAsAnObjectWithIsEmpty()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        var services = new ServiceCollection();
        services.AddThessera(options => options
            .AddDomainEventsFrom(typeof(FlushCounterCreated).Assembly)
            .UseMartenEventStore(fixture.ConnectionString)
                    .ProvisionInfrastructure(InfrastructureProvisioning.AtStartup));

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDocumentStore>();
        var counterId = Guid.NewGuid();
        var streamKey = "flush-counter/" + counterId;

        await using (var session = store.LightweightSession())
        {
            session.Events.Append(streamKey, new FlushCounterCreated(new FlushCounterId(counterId)));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var storedJson = await ReadStoredEventJsonAsync(streamKey);

        Assert.Contains($"\"CounterId\": \"{counterId}\"", storedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsEmpty", storedJson, StringComparison.Ordinal);
    }

    private async Task<string> ReadStoredEventJsonAsync(string streamKey)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand(
            "select data::text from public.mt_events where stream_id = @stream",
            connection);
        command.Parameters.AddWithValue("stream", streamKey);

        var stored = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Assert.IsType<string>(stored);
    }
}
