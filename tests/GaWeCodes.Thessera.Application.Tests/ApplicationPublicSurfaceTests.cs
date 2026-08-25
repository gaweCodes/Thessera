using System.Reflection;
using GaWeCodes.Thessera.Application.Cqrs;

namespace GaWeCodes.Thessera.Tests;

public sealed class ApplicationPublicSurfaceTests
{
    private static readonly string[] PublishedApi =
    [
        "GaWeCodes.Thessera.Application.Cqrs.ICommand",
        "GaWeCodes.Thessera.Application.Cqrs.ICommand`1",
        "GaWeCodes.Thessera.Application.Cqrs.ICommandHandler`1",
        "GaWeCodes.Thessera.Application.Cqrs.ICommandHandler`2",
        "GaWeCodes.Thessera.Application.Cqrs.IPipelineBehavior`2",
        "GaWeCodes.Thessera.Application.Cqrs.IQuery`1",
        "GaWeCodes.Thessera.Application.Cqrs.IQueryHandler`2",
        "GaWeCodes.Thessera.Application.Cqrs.ISender",
        "GaWeCodes.Thessera.Application.Cqrs.RequestPipeline`1",
        "GaWeCodes.Thessera.Application.Cqrs.RequestPipelineContinuation`1",
        "GaWeCodes.Thessera.Application.DomainEvents.DomainEventMetadata",
        "GaWeCodes.Thessera.Application.DomainEvents.IProjectionHandler`1",
        "GaWeCodes.Thessera.Application.IntegrationEvents.IIntegrationEvent",
        "GaWeCodes.Thessera.Application.IntegrationEvents.IIntegrationEventMapper`1",
        "GaWeCodes.Thessera.Application.IntegrationEvents.IIntegrationEventPublisher",
        "GaWeCodes.Thessera.Application.IntegrationEvents.IIntegrationEventSink",
        "GaWeCodes.Thessera.Application.IntegrationEvents.IntegrationEventTopicAttribute",
        "GaWeCodes.Thessera.Application.Persistence.IRepository`2",
        "GaWeCodes.Thessera.Application.Persistence.IUnitOfWork",
        "GaWeCodes.Thessera.Application.ReadModels.IReadModelRebuilder`2",
        "GaWeCodes.Thessera.Application.Results.Failure",
        "GaWeCodes.Thessera.Application.Results.FailureCategory",
        "GaWeCodes.Thessera.Application.Results.Result",
        "GaWeCodes.Thessera.Application.Results.Result`1",
    ];

    [Fact]
    public void TheNamespaceLayoutAndVisibilityAreExactlyThePublishedApi()
    {
        var expected = PublishedApi.Order(StringComparer.Ordinal).ToArray();

        var actual = typeof(ISender).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheAssemblyExposesNoPublicField()
    {
        var fields = typeof(ISender).Assembly
            .GetExportedTypes()
            .Where(type => !type.IsEnum)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(field => !field.IsLiteral)
            .Select(field => $"{field.DeclaringType?.FullName}.{field.Name}")
            .ToArray();

        Assert.Empty(fields);
    }
}
