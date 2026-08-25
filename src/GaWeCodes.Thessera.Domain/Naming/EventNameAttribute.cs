namespace GaWeCodes.Thessera.Domain.Naming;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventNameAttribute : Attribute
{
    public EventNameAttribute(string name)
    {
        Name = ContractName.Validate(name, nameof(name));
    }

    public string Name { get; }
}
