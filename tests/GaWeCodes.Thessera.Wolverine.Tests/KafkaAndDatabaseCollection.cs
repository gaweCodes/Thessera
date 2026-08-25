
namespace GaWeCodes.Thessera.Tests;

[CollectionDefinition(Name)]
public sealed class KafkaAndDatabaseCollection
    : ICollectionFixture<PostgreSqlFixture>, ICollectionFixture<KafkaFixture>
{
    public const string Name = "KafkaAndDatabase";
}
