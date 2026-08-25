using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;

namespace GaWeCodes.Thessera.Core.Dispatching;

internal sealed class ExceptionToResultBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    public const string ValidationFailureCode = DomainValidationException.FallbackCode;

    public const string BusinessRuleFailureCode = BusinessRuleViolationException.FallbackCode;

    public async Task<TResponse> HandleAsync(TRequest request, RequestPipeline<TResponse> pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        try
        {
            return await pipeline.NextAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DomainValidationException exception)
        {
            return pipeline.Failed(Translate(exception.Violations, ValidationFailureCode, FailureCategory.Validation, exception.Message));
        }
        catch (BusinessRuleViolationException exception)
        {
            return pipeline.Failed(Translate(exception.Violations, BusinessRuleFailureCode, FailureCategory.BusinessRule, exception.Message));
        }
    }

    private static Failure[] Translate(
        IReadOnlyList<RuleViolation> violations,
        string fallbackCode,
        FailureCategory category,
        string fallbackMessage)
    {
        if (violations.Count == 0)
        {
            return [new Failure(fallbackCode, fallbackMessage, category)];
        }

        var failures = new Failure[violations.Count];
        for (var index = 0; index < violations.Count; index++)
        {
            var violation = violations[index];
            failures[index] = new Failure(violation.Code, violation.Message, category)
            {
                Target = violation.Target,
            };
        }

        return failures;
    }
}
