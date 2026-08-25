
namespace GaWeCodes.Thessera.Testing;

internal static class TrimmingMessages
{
    internal const string AssemblyScanning =
        "Thessera discovers handlers, domain events and aggregates by scanning assemblies at run time. "
        + "Trimming removes types that are reached only this way, so discovery silently finds nothing and the "
        + "first request fails with 'No service for type ICommandHandler<...> has been registered'. "
        + "Publish without PublishTrimmed.";

    internal const string DynamicGenerics =
        "Thessera builds dispatcher, projection and mapper types with MakeGenericType at run time. "
        + "Native AOT cannot create instantiations it has not seen statically. Publish without PublishAot.";

    internal const string TypedKeyReflection =
        "Typed entity keys are read through reflection over IEntityKey<TValue> and through an expression tree that " +
        "is compiled at run time. Trimming removes the value property that the accessor reads and ahead-of-time " +
        "compilation cannot build the generic accessor at all, so writing or reading an aggregate fails. Publish " +
        "without PublishTrimmed and without PublishAot.";
}
