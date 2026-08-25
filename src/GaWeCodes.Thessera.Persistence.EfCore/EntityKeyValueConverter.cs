using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GaWeCodes.Thessera.Persistence.EfCore;

internal sealed class EntityKeyValueConverter<TKey, TValue>() : ValueConverter<TKey, TValue>(
    key => key.Value,
    value => EntityKeyActivator.Create<TKey, TValue>(value))
    where TKey : struct, IEntityKey<TValue>, IEquatable<TKey>
    where TValue : notnull
{
}
