using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// Handles one command that returns no value.
/// </summary>
/// <typeparam name="TCommand">The command this handler is registered for.</typeparam>
/// <remarks>
/// Register exactly one handler per command. The handler never commits — the unit of work does
/// that once per command. Both "exactly one handler" and "the commit also writes the outbox" are
/// runtime-dependent guarantees; see "What this package promises" in the package README.
/// </remarks>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A successful result, or a failed one carrying the reasons. Expected outcomes — not found, a
    /// conflict, a broken rule — belong in the result rather than in an exception.
    /// </returns>
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles one command that returns a value.
/// </summary>
/// <typeparam name="TCommand">The command this handler is registered for.</typeparam>
/// <typeparam name="TResult">The value handed back on success.</typeparam>
/// <remarks>
/// Register exactly one handler per command. The handler never commits — the unit of work does
/// that once per command. Both "exactly one handler" and "the commit also writes the outbox" are
/// runtime-dependent guarantees; see "What this package promises" in the package README.
/// </remarks>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Executes the command and produces its value.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A successful result carrying the value, or a failed one carrying the reasons. Both a value
    /// and a <see cref="Failure"/> convert implicitly, so a handler can simply return either.
    /// </returns>
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
