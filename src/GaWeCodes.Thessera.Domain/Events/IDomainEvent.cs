namespace GaWeCodes.Thessera.Domain.Events;

/// <summary>
/// A fact that has already happened inside the domain, raised by an aggregate and delivered in
/// process.
/// </summary>
/// <remarks>
/// A domain event never leaves the service. What leaves is an integration event, mapped from it by
/// an <c>IIntegrationEventMapper</c>. Every domain event type needs an <see cref="Naming.EventNameAttribute"/>:
/// the name in it is written into every persisted event and every envelope, so it is a persistence
/// contract rather than a detail of the CLR type.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Being a domain event is the whole meaning of the type, and aggregates, handlers, " +
        "mappers and stores bind against it as a generic constraint, which an attribute cannot serve.")]
public interface IDomainEvent;
