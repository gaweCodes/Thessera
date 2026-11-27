namespace GaWeCodes.Thessera.Domain.Events;

/// <summary>
/// A domain event that is no longer raised and is read as <typeparamref name="TSuccessor"/> instead.
/// </summary>
/// <typeparam name="TSuccessor">The event that replaces this one.</typeparam>
/// <remarks>
/// The type stays, with its <see cref="Naming.EventNameAttribute"/>, so that stored occurrences can
/// still be deserialized; Thessera upcasts them before any reader sees them.
/// </remarks>
public interface IRetiredDomainEvent<out TSuccessor> : IDomainEvent
    where TSuccessor : class, IDomainEvent
{
    /// <summary>
    /// Converts this stored event into its successor without changing what it means.
    /// </summary>
    /// <returns>A new instance of exactly <typeparamref name="TSuccessor"/>.</returns>
    TSuccessor Upcast();
}
