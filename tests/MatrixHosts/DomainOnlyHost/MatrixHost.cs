using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Rules;

namespace DomainOnlyHost;

public static class MatrixHost
{
    public static IReadOnlyCollection<IDomainEvent> Probe()
    {
        var reading = Reading.Record(ReadingId.New(), 72);
        reading.Correct(75);

        return reading.DomainEvents;
    }

    public static RuleViolation ProbeRejection()
    {
        try
        {
            Reading.Record(ReadingId.New(), 0);
        }
        catch (DomainValidationException violation)
        {
            return violation.Violations[0];
        }

        throw new InvalidOperationException(
            "Recording a reading with a non-positive value must be rejected by the domain.");
    }
}
