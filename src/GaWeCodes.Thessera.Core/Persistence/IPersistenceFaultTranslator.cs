using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Turns a driver exception into a failure, so that an ordinary write conflict reaches the caller as
/// a failed result instead of as a vendor exception.
/// </summary>
/// <remarks>
/// A unique-constraint violation and a concurrency conflict are answers a write can give, and the
/// caller usually knows what to do with them. Letting them escape as exceptions would put the name
/// of the database into the application layer.
/// <para>
/// Translators are tried in registration order, and the unit of work walks the whole exception chain
/// through them — EF Core and Marten wrap driver exceptions, so write yours against the exception it
/// actually understands rather than against whatever is outermost.
/// </para>
/// </remarks>
public interface IPersistenceFaultTranslator
{
    /// <summary>
    /// Recognises a fault this translator knows and describes it as a failure.
    /// </summary>
    /// <param name="exception">The exception to inspect — possibly an inner one.</param>
    /// <param name="failure">
    /// The failure when this method returns <see langword="true"/>; otherwise
    /// <see langword="null"/>. Use the shared codes in <see cref="PersistenceFailureCodes"/> where
    /// they fit, so callers can branch without knowing the store.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the fault was recognised. Return <see langword="false"/> for
    /// anything else: it is then offered to the next translator, and if none recognises it the
    /// exception keeps propagating, which is the right outcome for a genuine defect.
    /// </returns>
    bool TryTranslate(Exception exception, [NotNullWhen(true)] out Failure? failure);
}
