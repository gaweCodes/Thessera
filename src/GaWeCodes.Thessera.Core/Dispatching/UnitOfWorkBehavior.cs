using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Persistence;

namespace GaWeCodes.Thessera.Core.Dispatching;

internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    IEnumerable<IPersistenceFaultTranslator> faultTranslators)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    private static readonly bool IsCommand =
        typeof(ICommand).IsAssignableFrom(typeof(TRequest))
        || Array.Exists(
            typeof(TRequest).GetInterfaces(),
            static @interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(ICommand<>));

    public async Task<TResponse> HandleAsync(TRequest request, RequestPipeline<TResponse> pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var response = await pipeline.NextAsync(cancellationToken).ConfigureAwait(false);

        if (!IsCommand || response.IsFailure)
        {
            return response;
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (TryTranslate(exception, out var failure))
        {
            return pipeline.Failed(failure);
        }

        return response;
    }

    private bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            foreach (var translator in faultTranslators)
            {
                if (translator.TryTranslate(candidate, out failure))
                {
                    return true;
                }
            }
        }

        failure = null;
        return false;
    }
}
