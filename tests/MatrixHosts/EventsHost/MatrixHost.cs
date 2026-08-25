using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using Microsoft.Extensions.Hosting;

namespace EventsHost;

public static class MatrixHost
{
    private const string WriteConnectionString = "Host=localhost;Database=matrix-events;Username=matrix;Password=matrix";

    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(MatrixHost).Assembly)
            .UseMartenEventStore(WriteConnectionString));

        return builder.Build();
    }
}

[EventName("matrix-events-probe-v1")]
public sealed record MatrixProbe(string Value) : DomainEvent;
