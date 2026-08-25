namespace GaWeCodes.Thessera.Domain.Naming;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AggregateNameAttribute : Attribute
{
    public AggregateNameAttribute(string name)
    {
        Name = ContractName.Validate(name, nameof(name));
    }

    public string Name { get; }
}
