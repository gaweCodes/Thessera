
namespace GaWeCodes.Thessera.Tests;

[CollectionDefinition(Name)]
public sealed class KafkaAndDatabaseCollectionDefinition
    : ICollectionFixture<PostgreSqlFixture>, ICollectionFixture<KafkaFixture>
{
    public const string Name = "KafkaAndDatabase";
}
