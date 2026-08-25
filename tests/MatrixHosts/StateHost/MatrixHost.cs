using GaWeCodes.Thessera;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace StateHost;

public static class MatrixHost
{
    private const string WriteConnectionString = "Host=localhost;Database=matrix-state;Username=matrix;Password=matrix";

    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddThessera(options => options
            .AddDomainEventsFrom(typeof(MatrixHost).Assembly)
            .UseEfCoreStateStore<MatrixDbContext>(WriteConnectionString));

        return builder.Build();
    }
}

public sealed class MatrixDbContext(DbContextOptions<MatrixDbContext> options) : DbContext(options);

[EventName("matrix-state-probe-v1")]
public sealed record MatrixProbe(string Value) : DomainEvent;
