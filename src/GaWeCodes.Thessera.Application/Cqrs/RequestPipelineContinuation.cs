namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// The rest of the pipeline, as something a behaviour can call.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="cancellationToken">Cancels the operation.</param>
/// <returns>The response produced further in — ultimately by the handler.</returns>
public delegate Task<TResponse> RequestPipelineContinuation<TResponse>(CancellationToken cancellationToken);
