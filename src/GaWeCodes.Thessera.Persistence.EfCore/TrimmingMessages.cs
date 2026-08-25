
namespace GaWeCodes.Thessera.Persistence.EfCore;

internal static class TrimmingMessages
{
    internal const string ModelReflection =
        "The EF Core adapter reflects over aggregate state, its typed keys and its child collections to build the " +
        "model and to rehydrate an aggregate. Trimming removes the members that are reached only this way, so " +
        "mapping or rehydration fails at run time. EF Core itself is not fully compatible with trimming either. " +
        "Publish without PublishTrimmed and without PublishAot.";
}
