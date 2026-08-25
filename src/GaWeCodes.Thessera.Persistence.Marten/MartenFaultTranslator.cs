using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Persistence;
using JasperFx;

namespace GaWeCodes.Thessera.Persistence.Marten;

internal sealed class MartenFaultTranslator : IPersistenceFaultTranslator
{
    public bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure)
    {
        if (exception is ConcurrencyException)
        {
            failure = Failure.Conflict(PersistenceFailureCodes.ConcurrencyConflict, exception.Message);
            return true;
        }

        failure = null;
        return false;
    }
}
