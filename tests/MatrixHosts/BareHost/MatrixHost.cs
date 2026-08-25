using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using Microsoft.Extensions.Hosting;

namespace BareHost;

public static class MatrixHost
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options.AddDomainEventsFrom(typeof(MatrixHost).Assembly));

        return builder.Build();
    }
}

[EventName("matrix-bare-probe-v1")]
public sealed record MatrixProbe(string Value) : DomainEvent;
