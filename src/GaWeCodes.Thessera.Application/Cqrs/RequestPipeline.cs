using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// What a pipeline behaviour is handed: the way further in, and the way to stop.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class RequestPipeline<TResponse>
{
    private readonly RequestPipelineContinuation<TResponse> _continuation;
    private readonly Func<IReadOnlyList<Failure>, TResponse> _failed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestPipeline{TResponse}"/> class.
    /// </summary>
    /// <param name="continuation">The rest of the pipeline.</param>
    /// <param name="failed">Builds the correctly typed failed response from a list of failures.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="continuation"/> or <paramref name="failed"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The dispatcher constructs this. A behaviour receives one; it does not build one.
    /// </remarks>
    public RequestPipeline(RequestPipelineContinuation<TResponse> continuation, Func<IReadOnlyList<Failure>, TResponse> failed)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(failed);

        _continuation = continuation;
        _failed = failed;
    }

    /// <summary>
    /// Continues into the rest of the pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The response produced further in.</returns>
    /// <remarks>
    /// Call this exactly once. Skipping it stops the request; calling it twice runs the handler
    /// twice, which for a command means doing the work twice.
    /// </remarks>
    public Task<TResponse> NextAsync(CancellationToken cancellationToken) => _continuation(cancellationToken);

    /// <summary>
    /// Stops the request with one failure, without reaching the handler.
    /// </summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failed response of the right type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public TResponse Failed(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return _failed([failure]);
    }

    /// <summary>
    /// Stops the request with several failures, without reaching the handler.
    /// </summary>
    /// <param name="failures">The reasons, reported together.</param>
    /// <returns>A failed response of the right type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failures"/> is <see langword="null"/>.</exception>
    public TResponse Failed(IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return _failed(failures);
    }
}
