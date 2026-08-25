namespace GaWeCodes.Thessera.Domain.Naming;

internal static class ContractName
{
    public static string Validate(string name, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameterName);

        return NameSegment.IsValid(name)
            ? name
            : throw new ArgumentException(
                $"'{name}' is not a valid contract name. A persisted name is lower-case kebab-case " +
                "(letters, digits and single hyphens, for example 'widget-created-v1'), so that it stays " +
                "readable in the database and independent of the CLR type it happens to be written on.",
                parameterName);
    }
}
