namespace GaWeCodes.Thessera.Domain.Events;

/// <summary>
/// A convenient record base for domain events, giving value equality and a readable
/// <see cref="object.ToString"/> without any further work.
/// </summary>
/// <remarks>
/// Deriving from this type is optional: the family binds against <see cref="IDomainEvent"/>, and a
/// domain event that implements the interface directly is treated exactly the same.
/// </remarks>
public abstract record DomainEvent : IDomainEvent;
