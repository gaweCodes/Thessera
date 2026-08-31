namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// A request that changes something and returns no value.
/// </summary>
/// <remarks>
/// Being a command is what makes the difference at run time: the unit of work commits for commands
/// and not for queries, so a request that writes must be one of these rather than an
/// <see cref="IQuery{TResult}"/>.
/// </remarks>
public interface ICommand;

/// <summary>
/// A request that changes something and returns a value — typically the identity of what it
/// created.
/// </summary>
/// <typeparam name="TResult">The value handed back on success.</typeparam>
/// <remarks>
/// Return only what the caller needs. Returning the whole aggregate creates
/// unnecessary coupling to its structure.
/// </remarks>
public interface ICommand<TResult>;
