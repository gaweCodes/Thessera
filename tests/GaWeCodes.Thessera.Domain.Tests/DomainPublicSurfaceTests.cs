using System.Reflection;
using GaWeCodes.Thessera.Domain;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainPublicSurfaceTests
{
    private static readonly string[] PublishedApi =
    [
        "GaWeCodes.Thessera.Domain.Aggregates.AggregateRoot`2",
        "GaWeCodes.Thessera.Domain.Aggregates.AggregateState`2",
        "GaWeCodes.Thessera.Domain.Aggregates.EventSourcedAggregateRoot`2",
        "GaWeCodes.Thessera.Domain.Aggregates.IAggregateRoot`1",
        "GaWeCodes.Thessera.Domain.Aggregates.IEventSourcedAggregateRoot`1",
        "GaWeCodes.Thessera.Domain.Aggregates.IStateOwner",
        "GaWeCodes.Thessera.Domain.Entities.Entity`2",
        "GaWeCodes.Thessera.Domain.Entities.EntityBase`1",
        "GaWeCodes.Thessera.Domain.Entities.EntityState`2",
        "GaWeCodes.Thessera.Domain.Entities.IChildOwner`2",
        "GaWeCodes.Thessera.Domain.Entities.IEntity`1",
        "GaWeCodes.Thessera.Domain.Entities.IEntityKey",
        "GaWeCodes.Thessera.Domain.Entities.IEntityKey`1",
        "GaWeCodes.Thessera.Domain.Events.DomainEvent",
        "GaWeCodes.Thessera.Domain.Events.IDomainEvent",
        "GaWeCodes.Thessera.Domain.Events.IDomainEventOwner",
        "GaWeCodes.Thessera.Domain.Events.IDomainEventRaiser",
        "GaWeCodes.Thessera.Domain.Events.IHasDomainEvents",
        "GaWeCodes.Thessera.Domain.IClock",
        "GaWeCodes.Thessera.Domain.Naming.AggregateNameAttribute",
        "GaWeCodes.Thessera.Domain.Naming.EventNameAttribute",
        "GaWeCodes.Thessera.Domain.Naming.NameSegment",
        "GaWeCodes.Thessera.Domain.Rules.BusinessRuleViolationException",
        "GaWeCodes.Thessera.Domain.Rules.DomainValidationException",
        "GaWeCodes.Thessera.Domain.Rules.IBusinessRule",
        "GaWeCodes.Thessera.Domain.Rules.IDomainValidationRule",
        "GaWeCodes.Thessera.Domain.Rules.RuleChecker",
        "GaWeCodes.Thessera.Domain.Rules.RuleViolation",
    ];

    [Fact]
    public void TheNamespaceLayoutAndVisibilityAreExactlyThePublishedApi()
    {
        var expected = PublishedApi.Order(StringComparer.Ordinal).ToArray();

        var actual = typeof(IClock).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheAssemblyExposesNoPublicField()
    {
        var fields = typeof(IClock).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(field => !field.IsLiteral)
            .Select(field => $"{field.DeclaringType?.FullName}.{field.Name}")
            .ToArray();

        Assert.Empty(fields);
    }
}
