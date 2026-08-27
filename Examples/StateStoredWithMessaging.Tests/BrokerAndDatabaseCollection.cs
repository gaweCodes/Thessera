using GaWeCodes.Thessera.Tests;

[CollectionDefinition(Name)]
public sealed class BrokerAndDatabaseCollection
    : ICollectionFixture<PostgreSqlFixture>, ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "BrokerAndDatabase";
}
