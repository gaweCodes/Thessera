using GaWeCodes.Thessera.Domain.Rules;

namespace DomainOnly;

public sealed class ReadingStore
{
    private readonly Dictionary<int, Reading> _readings = [];
    private int _nextId;

    public OperationResult Create(int value)
    {
        try
        {
            var reading = Reading.Record(new ReadingId(++_nextId), value);
            _readings.Add(reading.Id.Value, reading);

            return OperationResult.Completed(
                "Create",
                ReadingSnapshot.From(reading),
                [],
                DomainOnlyJson.ToJsonElements(reading.PullDomainEvents()));
        }
        catch (DomainValidationException exception)
        {
            _nextId--;
            return OperationResult.Failure("Create", exception.Message);
        }
    }

    public OperationResult List() =>
        OperationResult.Completed(
            "List",
            null,
            [.. _readings.Values.OrderBy(reading => reading.Id.Value).Select(ReadingSnapshot.From)],
            []);

    public OperationResult Update(int id, int value)
    {
        if (!_readings.TryGetValue(id, out var reading) || reading.IsRemoved)
        {
            return OperationResult.Failure("Update", "Reading not found.");
        }

        try
        {
            reading.ChangeValue(value);
            return OperationResult.Completed(
                "Update",
                ReadingSnapshot.From(reading),
                [],
                DomainOnlyJson.ToJsonElements(reading.PullDomainEvents()));
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.Failure("Update", exception.Message);
        }
    }

    public OperationResult Delete(int id)
    {
        if (!_readings.TryGetValue(id, out var reading) || reading.IsRemoved)
        {
            return OperationResult.Failure("Delete", "Reading not found.");
        }

        reading.Remove();
        var events = DomainOnlyJson.ToJsonElements(reading.PullDomainEvents());
        _readings.Remove(id);

        return OperationResult.Completed("Delete", ReadingSnapshot.From(reading), [], events);
    }
}
