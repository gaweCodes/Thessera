namespace GaWeCodes.Thessera.Application.Cqrs;

public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestPipeline<TResponse> pipeline, CancellationToken cancellationToken);
}
