using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class EfCoreFaultTranslator : IPersistenceFaultTranslator
{
    public bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            failure = Failure.Conflict(PersistenceFailureCodes.ConcurrencyConflict, exception.Message);
            return true;
        }

        failure = null;
        return false;
    }
}
