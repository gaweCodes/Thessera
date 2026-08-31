namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// A cross-cutting step wrapped around every dispatched command and query.
/// </summary>
/// <typeparam name="TRequest">The request being handled.</typeparam>
/// <typeparam name="TResponse">The response type — a result, possibly carrying a value.</typeparam>
/// <remarks>
/// Register an open generic behaviour together with its order; a lower order runs further out.
/// Registration, the three built-in behaviours (logging, exception-to-result and unit-of-work, at
/// orders 0, 100 and 300) and their ordering are runtime-dependent; see "What this package
/// promises" in the package README.
/// </remarks>
/// <seealso cref="RequestPipeline{TResponse}"/>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>
    /// Runs around the rest of the pipeline.
    /// </summary>
    /// <param name="request">The request passing through.</param>
    /// <param name="pipeline">
    /// The rest of the pipeline. Call <see cref="RequestPipeline{TResponse}.NextAsync"/> to
    /// continue, or <c>Failed</c> to stop the request without reaching the handler.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The response, either from further in or one this behaviour produced itself.</returns>
    Task<TResponse> HandleAsync(TRequest request, RequestPipeline<TResponse> pipeline, CancellationToken cancellationToken);
}
