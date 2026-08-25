using System.Text.Json;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class PackageMatrixTests
{
    private const string Marten = "Marten";

    private const string EfCore = "Microsoft.EntityFrameworkCore";

    private const string RabbitMq = "RabbitMQ.Client";

    private const string Wolverine = "WolverineFx";

    private const string Npgsql = "Npgsql";

    private const string Kafka = "Confluent.Kafka";

    private static readonly string[] AllVendors = [Marten, EfCore, RabbitMq, Wolverine, Npgsql];

    public static TheoryData<string, string[]> ForbiddenVendorsPerHost =>
        new()
        {
            { "DomainOnlyHost", [Marten, EfCore, RabbitMq, Wolverine, Npgsql] },
            { "ContractsHost", [Marten, EfCore, RabbitMq, Wolverine, Npgsql] },
            { "BareHost", [Marten, EfCore, RabbitMq, Wolverine, Npgsql] },
            { "StateHost", [Marten, RabbitMq] },
            { "EventsHost", [EfCore, RabbitMq] },
            { "StateBusHost", [Marten] },
            { "EventsBusHost", [EfCore] },
            { "ForeignStoreHost", [Marten, RabbitMq, Npgsql] },
            { "ForeignBrokerHost", [Marten, RabbitMq, Npgsql] },
        };

    public static TheoryData<string, string[]> RequiredVendorsPerHost =>
        new()
        {
            { "StateHost", [EfCore, Npgsql, Wolverine] },
            { "EventsHost", [Marten, Npgsql, Wolverine] },
            { "StateBusHost", [EfCore, RabbitMq, Wolverine] },
            { "EventsBusHost", [Marten, RabbitMq, Wolverine] },
            { "ForeignStoreHost", [EfCore, Wolverine] },
            { "ForeignBrokerHost", [EfCore, Wolverine, Kafka] },
        };

    public static TheoryData<string> HostNames =>
    [
        "BareHost",
        "StateHost",
        "EventsHost",
        "StateBusHost",
        "EventsBusHost",
        "ForeignStoreHost",
        "ForeignBrokerHost",
    ];

    [Theory]
    [MemberData(nameof(ForbiddenVendorsPerHost))]
    public void AHostNeverRestoresAVendorItDidNotChoose(string host, string[] forbidden)
    {
        var restored = RestoredPackages(host);

        var leaked = forbidden
            .Where(vendor => restored.Any(package => package.StartsWith(vendor, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(leaked);
    }

    [Theory]
    [MemberData(nameof(RequiredVendorsPerHost))]
    public void AHostRestoresEveryVendorItDidChoose(string host, string[] required)
    {
        var restored = RestoredPackages(host);

        var missing = required
            .Where(vendor => !restored.Any(package => package.StartsWith(vendor, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void TheDetectorTellsAPresentVendorFromAnAbsentOne()
    {
        var withEverything = VendorsIn("EventsBusHost");
        var withNothing = VendorsIn("BareHost");

        Assert.Equal([Marten, RabbitMq, Wolverine, Npgsql], withEverything);
        Assert.Empty(withNothing);
    }

    [Theory]
    [MemberData(nameof(HostNames))]
    public void EveryPackageCombinationWiresItselfUp(string host)
    {
        using var built = BuildHost(host);

        Assert.NotNull(built.Services.GetRequiredService<ISender>());
    }

    [Fact]
    public void TheDomainStageModelsAnAggregateWithoutAnyRuntime()
    {
        var raised = DomainOnlyHost.MatrixHost.Probe();

        Assert.Equal(
            ["ReadingRecorded", "ReadingCorrected"],
            raised.Select(domainEvent => domainEvent.GetType().Name));
    }

    [Fact]
    public void TheDomainStageEnforcesItsOwnRulesWithoutAnyRuntime()
    {
        var violation = DomainOnlyHost.MatrixHost.ProbeRejection();

        Assert.Equal("reading.value.not-positive", violation.Code);
    }

    [Fact]
    public async Task TheContractsStageRunsAHandlerWithoutAnyRuntime()
    {
        var found = await ContractsHost.MatrixHost.ProbeAsync(TestContext.Current.CancellationToken);

        Assert.True(found.IsSuccess);
        Assert.Equal(72, found.Value);
    }

    [Fact]
    public async Task TheContractsStageTurnsAMissingAggregateIntoAFailure()
    {
        var missing = await ContractsHost.MatrixHost.ProbeMissingAsync(TestContext.Current.CancellationToken);

        Assert.True(missing.IsFailure);
        Assert.Equal(FailureCategory.NotFound, missing.Failures[0].Category);
    }

    [Fact]
    public async Task TheContractsStageProjectsIntoAReadModelWithoutAnyRuntime()
    {
        var written = await ContractsHost.MatrixHost.ProbeProjectionAsync(TestContext.Current.CancellationToken);

        Assert.Equal([72, 75], written.Select(summary => summary.Value));
        Assert.Equal([1L, 2L], written.Select(summary => summary.Version));
        Assert.Single(written.Select(summary => summary.AggregateId).Distinct());
    }

    private static IHost BuildHost(string host) => host switch
    {
        "BareHost" => BareHost.MatrixHost.Build(),
        "StateHost" => StateHost.MatrixHost.Build(),
        "EventsHost" => EventsHost.MatrixHost.Build(),
        "StateBusHost" => StateBusHost.MatrixHost.Build(),
        "EventsBusHost" => EventsBusHost.MatrixHost.Build(),
        "ForeignStoreHost" => ForeignStoreHost.MatrixHost.Build(),
        "ForeignBrokerHost" => ForeignBrokerHost.MatrixHost.Build(),
        _ => throw new ArgumentOutOfRangeException(nameof(host), host, "Unknown matrix host."),
    };

    private static string[] VendorsIn(string host)
    {
        var restored = RestoredPackages(host);

        return
        [
            .. AllVendors.Where(vendor =>
                restored.Any(package => package.StartsWith(vendor, StringComparison.Ordinal))),
        ];
    }

    private static string[] RestoredPackages(string host)
    {
        var assets = Path.Combine(
            ThesseraLayout.Root,
            "tests",
            "MatrixHosts",
            host,
            "obj",
            "project.assets.json");

        Assert.True(File.Exists(assets), $"'{assets}' does not exist; '{host}' has not been restored.");

        using var document = JsonDocument.Parse(File.ReadAllText(assets));

        return
        [
            .. document.RootElement
                .GetProperty("libraries")
                .EnumerateObject()
                .Select(library => library.Name),
        ];
    }
}
