using Testcontainers.RabbitMq;

namespace GaWeCodes.Thessera.Tests;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public Uri ConnectionUri { get; private set; } = new("amqp://localhost");

    public bool Available { get; private set; }

    public string SkipReason { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
            await _container.StartAsync();
            ConnectionUri = new Uri(_container.GetConnectionString());
            Available = true;
        }
        catch (Exception exception)
        {
            ContainerRequirement.ThrowIfRequired("RabbitMQ", exception);
            Available = false;
            SkipReason = $"RabbitMQ Testcontainer could not be started (Docker required): {exception.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
