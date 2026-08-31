using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// The entry point into the application layer: hands a command or a query to its one registered
/// handler, through the pipeline behaviours.
/// </summary>
/// <remarks>
/// This is glue rather than a product. If you are shopping for a mediator, it is not the reason to
/// choose this family — it exists so that the behaviours, the unit of work and the outbox line up
/// around a request without every caller having to arrange them. The outbox part is
/// runtime-dependent; see "What this package promises" in the package README.
/// </remarks>
public interface ISender
{
    /// <summary>
    /// Dispatches a command that returns no value.
    /// </summary>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The handler's result. On success the unit of work has committed; whether the domain events
    /// raised during the request are also in the outbox is runtime-dependent (see the package
    /// README).
    /// </returns>
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Dispatches a command that returns a value.
    /// </summary>
    /// <typeparam name="TResult">The value handed back on success.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The handler's result. On success the unit of work has committed; whether the domain events
    /// raised during the request are also in the outbox is runtime-dependent (see the package
    /// README).
    /// </returns>
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull;

    /// <summary>
    /// Dispatches a query.
    /// </summary>
    /// <typeparam name="TResult">The value that is read.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The handler's result. Nothing is committed for a query.</returns>
    Task<Result<TResult>> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
