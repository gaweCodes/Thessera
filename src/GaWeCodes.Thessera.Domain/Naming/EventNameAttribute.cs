namespace GaWeCodes.Thessera.Domain.Naming;

/// <summary>
/// Declares the persisted name of a domain event type — the name written into every stored event
/// and every envelope, and the name an incoming event is resolved back to a type by.
/// </summary>
/// <remarks>
/// Renaming the C# record costs nothing; changing this value orphans every event already stored
/// under the old name, because nothing can resolve it to a type any more. When an event needs to
/// change shape, keep the retired type and its name alongside the successor instead of renaming —
/// a version suffix such as <c>reading-recorded-v1</c> makes that possible from the start.
/// </remarks>
/// <seealso cref="AggregateNameAttribute"/>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventNameAttribute"/> class.
    /// </summary>
    /// <param name="name">
    /// The persisted name, in lower-case kebab-case: ASCII letters, digits and single hyphens, none
    /// at the start or the end — for example <c>reading-recorded-v1</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, blank, or not a valid contract name.
    /// </exception>
    public EventNameAttribute(string name)
    {
        Name = ContractName.Validate(name, nameof(name));
    }

    /// <summary>
    /// Gets the persisted name of the domain event.
    /// </summary>
    public string Name { get; }
}
