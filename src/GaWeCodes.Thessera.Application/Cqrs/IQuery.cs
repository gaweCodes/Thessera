namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// A request that reads and changes nothing.
/// </summary>
/// <typeparam name="TResult">The value that is read.</typeparam>
public interface IQuery<TResult>;
