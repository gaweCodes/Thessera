// Linked into each consuming test project with <Compile Include>, not shared through a project
// reference. That is deliberate and required: an xUnit [CollectionDefinition] is discovered per
// assembly, so a definition sitting in a referenced assembly is invisible to the referencing one.
// GaWeCodes.Thessera.Tests.Containers holds the fixtures themselves, which a project reference does carry.
namespace GaWeCodes.Thessera.Tests;

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}

[CollectionDefinition(Name)]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "RabbitMQ";
}
