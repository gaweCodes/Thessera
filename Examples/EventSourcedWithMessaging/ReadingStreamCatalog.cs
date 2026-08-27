using Npgsql;

namespace EventSourcedWithMessaging;

public sealed class ReadingStreamCatalog(string connectionString) : IReadingStreamCatalog
{
    public async Task<int> GetMaxIdAsync(CancellationToken cancellationToken)
    {
        var streamKeys = await ListStreamKeysAsync(cancellationToken).ConfigureAwait(false);
        return streamKeys
            .Select(streamKey => streamKey[EventSourcedWithMessagingApplication.StreamKeyPrefix.Length..])
            .Select(rawId => int.TryParse(rawId, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public async Task<IReadOnlyList<string>> ListStreamKeysAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select id
            from public.mt_streams
            where id like @prefix || '%'
            order by id
            """;
        command.Parameters.AddWithValue("prefix", EventSourcedWithMessagingApplication.StreamKeyPrefix);

        var streamKeys = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            streamKeys.Add(reader.GetString(0));
        }

        return streamKeys;
    }
}
