using Testcontainers.Kafka;

namespace GaWeCodes.Thessera.Tests;

public sealed class KafkaFixture : IAsyncLifetime
{
    private KafkaContainer? _container;

    public string BootstrapServers { get; private set; } = "localhost:9092";

    public bool Available { get; private set; }

    public string SkipReason { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new KafkaBuilder("apache/kafka:3.9.0").Build();
            await _container.StartAsync();
            BootstrapServers = _container.GetBootstrapAddress().Replace("PLAINTEXT://", string.Empty, StringComparison.Ordinal);
            Available = true;
        }
        catch (Exception exception)
        {
            ContainerRequirement.ThrowIfRequired("Kafka", exception);
            Available = false;
            SkipReason = $"Kafka Testcontainer could not be started (Docker required): {exception.Message}";
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
