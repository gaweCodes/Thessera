namespace GaWeCodes.Thessera.Application.Cqrs;

/// <summary>
/// A request that reads and changes nothing.
/// </summary>
/// <typeparam name="TResult">The value that is read.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "The interface carries TResult into the handler's generic constraint, which an " +
        "attribute cannot; being a query is otherwise the whole meaning of the type.")]
public interface IQuery<TResult>;
