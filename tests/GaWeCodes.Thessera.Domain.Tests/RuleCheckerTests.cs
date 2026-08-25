using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

public sealed class RuleCheckerTests
{
    [Fact]
    public void CheckBusinessRule_Broken_ThrowsWithMessage()
    {
        var rule = new FakeBusinessRule(isBroken: true, message: "nope");

        var ex = Assert.Throws<BusinessRuleViolationException>(() => RuleChecker.CheckBusinessRule(rule));
        Assert.Equal("nope", ex.Message);
    }

    [Fact]
    public void CheckBusinessRule_Satisfied_DoesNotThrow()
    {
        var rule = new FakeBusinessRule(isBroken: false);

        RuleChecker.CheckBusinessRule(rule);

        Assert.True(rule.Evaluated);
    }

    [Fact]
    public void CheckValidationRule_Invalid_ThrowsWithMessage()
    {
        var rule = new FakeValidationRule(isInvalid: true, message: "bad");

        var ex = Assert.Throws<DomainValidationException>(() => RuleChecker.CheckValidationRule(rule));
        Assert.Equal("bad", ex.Message);
    }

    [Fact]
    public void CheckValidationRule_Valid_DoesNotThrow()
    {
        var rule = new FakeValidationRule(isInvalid: false);

        RuleChecker.CheckValidationRule(rule);

        Assert.True(rule.Evaluated);
    }

    [Fact]
    public void CheckAllBusinessRules_EvaluatesEveryRuleAndCollectsAll()
    {
        var broken = new FakeBusinessRule(isBroken: true, message: "first", code: "a");
        var alsoBroken = new FakeBusinessRule(isBroken: true, message: "second", code: "b");

        var ex = Assert.Throws<BusinessRuleViolationException>(
            () => RuleChecker.CheckAllBusinessRules(broken, alsoBroken));

        Assert.True(alsoBroken.Evaluated);
        Assert.Equal(2, ex.Violations.Count);
        Assert.Equal("a", ex.Violations[0].Code);
        Assert.Equal("first", ex.Violations[0].Message);
        Assert.Equal("b", ex.Violations[1].Code);
        Assert.Equal("second", ex.Violations[1].Message);
        Assert.Contains("first", ex.Message, StringComparison.Ordinal);
        Assert.Contains("second", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckBusinessRule_Broken_CarriesTheRulesOwnCodeAndNoTarget()
    {
        var broken = new FakeBusinessRule(isBroken: true, message: "first", code: "recipe.already_published");

        var ex = Assert.Throws<BusinessRuleViolationException>(() => RuleChecker.CheckBusinessRule(broken));

        var violation = Assert.Single(ex.Violations);
        Assert.Equal("recipe.already_published", violation.Code);
        Assert.Null(violation.Target);
    }

    [Fact]
    public void CheckAllBusinessRules_AllSatisfied_DoesNotThrow()
    {
        var a = new FakeBusinessRule(isBroken: false);
        var b = new FakeBusinessRule(isBroken: false);

        RuleChecker.CheckAllBusinessRules(a, b);

        Assert.True(a.Evaluated);
        Assert.True(b.Evaluated);
    }

    [Fact]
    public void CheckAllValidationRules_EvaluatesEveryRuleAndCollectsAll()
    {
        var invalid = new FakeValidationRule(isInvalid: true, message: "first", code: "a", target: "name");
        var alsoInvalid = new FakeValidationRule(isInvalid: true, message: "second", code: "b", target: "quantity");

        var ex = Assert.Throws<DomainValidationException>(
            () => RuleChecker.CheckAllValidationRules(invalid, alsoInvalid));

        Assert.True(alsoInvalid.Evaluated);
        Assert.Equal(2, ex.Violations.Count);
        Assert.Equal("a", ex.Violations[0].Code);
        Assert.Equal("name", ex.Violations[0].Target);
        Assert.Equal("first", ex.Violations[0].Message);
        Assert.Equal("b", ex.Violations[1].Code);
        Assert.Equal("quantity", ex.Violations[1].Target);
        Assert.Equal("second", ex.Violations[1].Message);
    }

    [Fact]
    public void CheckAllValidationRules_OnlyOneInvalid_KeepsTheRuleMessageVerbatim()
    {
        var valid = new FakeValidationRule(isInvalid: false);
        var invalid = new FakeValidationRule(isInvalid: true, message: "bad", target: "name");

        var ex = Assert.Throws<DomainValidationException>(() => RuleChecker.CheckAllValidationRules(valid, invalid));

        Assert.Equal("bad", ex.Message);
        var violation = Assert.Single(ex.Violations);
        Assert.Equal("name", violation.Target);
    }

    [Fact]
    public void CheckAllValidationRules_AllValid_DoesNotThrow()
    {
        var a = new FakeValidationRule(isInvalid: false);
        var b = new FakeValidationRule(isInvalid: false);

        RuleChecker.CheckAllValidationRules(a, b);

        Assert.True(a.Evaluated);
        Assert.True(b.Evaluated);
    }

    [Fact]
    public void CheckBusinessRule_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RuleChecker.CheckBusinessRule(null!));
    }

    [Fact]
    public void CheckValidationRule_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RuleChecker.CheckValidationRule(null!));
    }

    [Fact]
    public void CheckAllBusinessRules_NullArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RuleChecker.CheckAllBusinessRules(null!));
    }

    [Fact]
    public void CheckAllValidationRules_NullArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RuleChecker.CheckAllValidationRules(null!));
    }

    [Fact]
    public void CheckAllBusinessRules_NullAmongRules_ThrowsAndDoesNotEvaluateLaterRules()
    {
        var later = new FakeBusinessRule(isBroken: true, message: "later");

        Assert.Throws<ArgumentNullException>(() => RuleChecker.CheckAllBusinessRules(null!, later));

        Assert.False(later.Evaluated);
    }

    [Fact]
    public void CheckAllValidationRules_NullAmongRules_ThrowsAndDoesNotEvaluateLaterRules()
    {
        var later = new FakeValidationRule(isInvalid: true, message: "later");

        Assert.Throws<ArgumentNullException>(() => RuleChecker.CheckAllValidationRules(null!, later));

        Assert.False(later.Evaluated);
    }

    [Fact]
    public void CheckAllBusinessRules_Empty_DoesNotThrow()
    {
        RuleChecker.CheckAllBusinessRules();
    }

    [Fact]
    public void CheckAllValidationRules_Empty_DoesNotThrow()
    {
        RuleChecker.CheckAllValidationRules();
    }
}
