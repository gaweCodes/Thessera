using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// Handles one query.
/// </summary>
/// <typeparam name="TQuery">The query this handler is registered for.</typeparam>
/// <typeparam name="TResult">The value that is read.</typeparam>
/// <remarks>
/// Register exactly one handler per query — a runtime-dependent guarantee; see "What this package
/// promises" in the package README. A query handler is free to read from wherever suits it — a
/// read model, a projection, a dedicated context — and is not restricted to the repository the
/// write side uses.
/// </remarks>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Executes the query.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A successful result carrying the value, or a failed one — typically
    /// <see cref="Failure.NotFound(string, string)"/> when nothing matched.
    /// </returns>
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
