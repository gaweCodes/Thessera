namespace GaWeCodes.Thessera.Domain.Entities;

/// <summary>
/// The untyped half of a typed key: enough for the runtime to tell an assigned identity from an
/// unassigned one without knowing what the identity is made of.
/// </summary>
/// <remarks>
/// Wrapping an identity in its own type is what stops a <c>ReadingId</c> being passed where an
/// <c>OrderId</c> is expected. Implement this together with
/// <see cref="IEntityKey{TValue}"/> — typically as a <see langword="readonly record struct"/>.
/// </remarks>
public interface IEntityKey
{
    /// <summary>
    /// Gets a value indicating whether this key has never been given an identity.
    /// </summary>
    /// <value>
    /// <see langword="true"/> for the default value of the key type. An empty key should be
    /// refused before it becomes a stream key — it would otherwise look valid while being shared
    /// by every other unidentified aggregate of that type. That refusal is runtime-dependent; see
    /// "What this package promises" in the package README.
    /// </value>
    bool IsEmpty { get; }
}

/// <summary>
/// A typed key together with the value it wraps.
/// </summary>
/// <typeparam name="TValue">
/// The wrapped value. Only <see cref="Guid"/>, <see cref="string"/>, <see cref="int"/> and
/// <see cref="long"/> have a declared stream-key rendering, because the value is rendered into the
/// stream key and that text is persisted forever. Refusing any other type is runtime-dependent;
/// see "What this package promises" in the package README.
/// </typeparam>
public interface IEntityKey<out TValue> : IEntityKey
    where TValue : notnull
{
    /// <summary>
    /// Gets the wrapped value.
    /// </summary>
    /// <remarks>
    /// This is what a store column holds and what a typed key serializes to — a bare
    /// <c>uuid</c> or <c>bigint</c> rather than an object.
    /// </remarks>
    TValue Value { get; }
}
