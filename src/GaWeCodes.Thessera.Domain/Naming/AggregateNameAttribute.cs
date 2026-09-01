namespace GaWeCodes.Thessera.Domain.Naming;

/// <summary>
/// Declares the persisted name of an aggregate type — the name that prefixes every one of its
/// stream keys and travels on every domain-event envelope it produces.
/// </summary>
/// <remarks>
/// The name is a persistence contract, not a label. Renaming the C# class costs nothing; changing
/// this value orphans every stream and every row already written under the old name. A type
/// missing this attribute being refused, rather than silently named after its CLR type, is
/// runtime-dependent; see "What this package promises" in the package README.
/// </remarks>
/// <seealso cref="EventNameAttribute"/>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AggregateNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateNameAttribute"/> class.
    /// </summary>
    /// <param name="name">
    /// The persisted name, in lower-case kebab-case: ASCII letters, digits and single hyphens, none
    /// at the start or the end — for example <c>reading</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, blank, or not a valid contract name.
    /// </exception>
    public AggregateNameAttribute(string name)
    {
        Name = ContractName.Validate(name, nameof(name));
    }

    /// <summary>
    /// Gets the persisted name of the aggregate.
    /// </summary>
    public string Name { get; }
}
