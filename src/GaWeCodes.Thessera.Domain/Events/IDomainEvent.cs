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
public interface IDomainEvent;
