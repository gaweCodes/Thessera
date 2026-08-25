namespace GaWeCodes.Thessera.Application.Cqrs;

public delegate Task<TResponse> RequestPipelineContinuation<TResponse>(CancellationToken cancellationToken);
