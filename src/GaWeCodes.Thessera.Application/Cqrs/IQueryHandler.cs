using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
