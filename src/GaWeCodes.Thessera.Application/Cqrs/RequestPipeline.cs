using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

public sealed class RequestPipeline<TResponse>
{
    private readonly RequestPipelineContinuation<TResponse> _continuation;
    private readonly Func<IReadOnlyList<Failure>, TResponse> _failed;

    public RequestPipeline(RequestPipelineContinuation<TResponse> continuation, Func<IReadOnlyList<Failure>, TResponse> failed)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(failed);

        _continuation = continuation;
        _failed = failed;
    }

    public Task<TResponse> NextAsync(CancellationToken cancellationToken) => _continuation(cancellationToken);

    public TResponse Failed(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return _failed([failure]);
    }

    public TResponse Failed(IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return _failed(failures);
    }
}
