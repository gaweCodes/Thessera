using GaWeCodes.Thessera.Domain.Rules;

namespace GaWeCodes.Thessera.Tests;

public sealed class RuleViolationTests
{
    [Fact]
    public void Construction_WithoutAMessage_Throws() =>
        Assert.Throws<ArgumentException>(() => new RuleViolation("code", "target", "  "));

    [Fact]
    public void Construction_WithoutACode_Throws() =>
        Assert.Throws<ArgumentException>(() => new RuleViolation("  ", "target", "message"));

    [Fact]
    public void Construction_WithoutATarget_IsAllowed()
    {
        var violation = new RuleViolation("code", null, "message");

        Assert.Equal("code", violation.Code);
        Assert.Null(violation.Target);
        Assert.Equal("message", violation.Message);
    }

    [Fact]
    public void DomainValidationException_WithoutViolations_Throws() =>
        Assert.Throws<ArgumentException>(() => new DomainValidationException(Array.Empty<RuleViolation>()));

    [Fact]
    public void BusinessRuleViolationException_WithoutViolations_Throws() =>
        Assert.Throws<ArgumentException>(() => new BusinessRuleViolationException(Array.Empty<RuleViolation>()));

    [Fact]
    public void DomainValidationException_WithANullViolation_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => new DomainValidationException([new RuleViolation("a", null, "a"), null!]));

    [Fact]
    public void DomainValidationException_FromAMessage_CarriesOneViolationWithTheFallbackCode()
    {
        var exception = new DomainValidationException("bad");

        var violation = Assert.Single(exception.Violations);
        Assert.Equal("bad", violation.Message);
        Assert.Equal(DomainValidationException.FallbackCode, violation.Code);
        Assert.Null(violation.Target);
    }

    [Fact]
    public void BusinessRuleViolationException_FromAMessage_CarriesOneViolationWithTheFallbackCode()
    {
        var exception = new BusinessRuleViolationException("nope");

        var violation = Assert.Single(exception.Violations);
        Assert.Equal("nope", violation.Message);
        Assert.Equal(BusinessRuleViolationException.FallbackCode, violation.Code);
    }

    [Fact]
    public void FallbackCodes_AreDistinct() =>
        Assert.NotEqual(DomainValidationException.FallbackCode, BusinessRuleViolationException.FallbackCode);

    [Fact]
    public void ParameterlessConstructors_CarryNoViolations()
    {
        Assert.Empty(new DomainValidationException().Violations);
        Assert.Empty(new BusinessRuleViolationException().Violations);
    }

    [Fact]
    public void DomainValidationException_WithSeveralViolations_JoinsTheMessages()
    {
        var exception = new DomainValidationException(
            [new RuleViolation("a", "name", "first"), new RuleViolation("b", "quantity", "second")]);

        Assert.Equal("first; second", exception.Message);
    }
}
