namespace GaWeCodes.Thessera.Tests;

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollectionDefinition : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
