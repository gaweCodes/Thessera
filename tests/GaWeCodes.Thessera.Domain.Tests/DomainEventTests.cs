using System.Reflection;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainEventTests
{
    [Fact]
    public void TheContract_CarriesNoIdentityOfItsOwn()
    {
        Assert.Empty(typeof(IDomainEvent).GetProperties());
        Assert.Empty(typeof(IDomainEvent).GetMethods());
    }

    [Fact]
    public void TheBaseRecord_DeclaresNothingBeyondTheRecordMachinery()
    {
        var declared = typeof(DomainEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(declared);
    }

    [Theory]
    [InlineData("EventId")]
    [InlineData("OccurredAt")]
    public void NoDomainEventType_MintsItsOwnIdentity(string forbiddenMember)
    {
        var offenders = typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(typeof(IDomainEvent).IsAssignableFrom)
            .Where(type => type.GetProperty(forbiddenMember) is not null)
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Records_WithSameData_AreValueEqual()
    {
        var first = new TestDomainEvent(1);
        var second = new TestDomainEvent(1);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Records_WithDifferentData_AreNotEqual()
    {
        var first = new TestDomainEvent(1);
        var second = new TestDomainEvent(2);

        Assert.NotEqual(first, second);
    }
}
