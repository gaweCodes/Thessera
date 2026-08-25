using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Core.Persistence;

public interface IPersistenceFaultTranslator
{
    bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure);
}
