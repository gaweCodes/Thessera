using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

public interface ISender
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull;

    Task<Result<TResult>> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
