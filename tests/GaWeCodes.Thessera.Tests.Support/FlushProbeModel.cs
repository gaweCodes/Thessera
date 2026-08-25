using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;
public sealed class FlushDeliverySignal
{
    private readonly TaskCompletionSource<(IDomainEvent Event, DomainEventMetadata Metadata)> _delivered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<(IDomainEvent Event, DomainEventMetadata Metadata)> Delivered => _delivered.Task;

    public void MarkDelivered(IDomainEvent domainEvent, DomainEventMetadata metadata) =>
        _delivered.TrySetResult((domainEvent, metadata));
}

public sealed record CreateFlushCounter(Guid Id) : ICommand;

public sealed class CreateFlushCounterHandler(IRepository<FlushCounter, FlushCounterId> repository)
    : ICommandHandler<CreateFlushCounter>
{
    public async Task<Result> HandleAsync(CreateFlushCounter command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await repository.AddAsync(FlushCounter.Create(new FlushCounterId(command.Id)), cancellationToken);
        return Result.Success();
    }
}

public sealed class FlushCounterProjection(FlushDeliverySignal signal) : IProjectionHandler<FlushCounterCreated>
{
    public Task HandleAsync(FlushCounterCreated domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken)
    {
        signal.MarkDelivered(domainEvent, metadata);
        return Task.CompletedTask;
    }
}

public readonly record struct FlushCounterId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("flush-counter-created-v1")]
public sealed record FlushCounterCreated(FlushCounterId CounterId) : DomainEvent;

public sealed record FlushCounterState(FlushCounterId Id) : AggregateState<FlushCounterState, FlushCounterId>
{
    public static FlushCounterState Empty => new(new FlushCounterId(Guid.Empty));

    public override FlushCounterState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        FlushCounterCreated created => this with { Id = created.CounterId },
        _ => this,
    };
}

[AggregateName("flush-counter")]
public sealed class FlushCounter : EventSourcedAggregateRoot<FlushCounterId, FlushCounterState>
{
    private FlushCounter() : base(FlushCounterState.Empty)
    {
    }

    public static FlushCounter Create(FlushCounterId id)
    {
        var counter = new FlushCounter();
        counter.RaiseEvent(new FlushCounterCreated(id));
        return counter;
    }
}

public sealed record StartFlushProbe(Guid Id) : ICommand;

public sealed class StartFlushProbeHandler(IRepository<FlushProbe, FlushProbeId> repository)
    : ICommandHandler<StartFlushProbe>
{
    public async Task<Result> HandleAsync(StartFlushProbe command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await repository.AddAsync(FlushProbe.Create(new FlushProbeId(command.Id)), cancellationToken);
        return Result.Success();
    }
}

public sealed class FlushProbeProjection(FlushDeliverySignal signal) : IProjectionHandler<FlushProbeStarted>
{
    public Task HandleAsync(FlushProbeStarted domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken)
    {
        signal.MarkDelivered(domainEvent, metadata);
        return Task.CompletedTask;
    }
}

[EventName("flush-probe-started-v1")]
public sealed record FlushProbeStarted(FlushProbeId ProbeId) : DomainEvent;

[EventName("flush-probe-renamed-v1")]
public sealed record FlushProbeRenamed(FlushProbeId ProbeId, string Name) : DomainEvent;

public sealed record RenameFlushProbe(Guid Id, string Name) : ICommand;

public sealed class RenameFlushProbeHandler(IRepository<FlushProbe, FlushProbeId> repository)
    : ICommandHandler<RenameFlushProbe>
{
    public async Task<Result> HandleAsync(RenameFlushProbe command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var probe = await repository.GetByIdAsync(new FlushProbeId(command.Id), cancellationToken);
        if (probe is null)
        {
            return Failure.NotFound("probe.not_found", "No probe with that id exists.");
        }

        probe.Rename(command.Name);
        return Result.Success();
    }
}

public readonly record struct FlushProbeId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record FlushProbeState(FlushProbeId Id, string Name) : AggregateState<FlushProbeState, FlushProbeId>
{
    public static FlushProbeState Empty => new(default, string.Empty);

    public override FlushProbeState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        FlushProbeStarted started => this with { Id = started.ProbeId, Name = "probe" },
        FlushProbeRenamed renamed => this with { Name = renamed.Name },
        _ => this,
    };
}

[AggregateName("flush-probe")]
public sealed class FlushProbe : AggregateRoot<FlushProbeId, FlushProbeState>
{
    private FlushProbe() : base(FlushProbeState.Empty)
    {
    }

    public string Name => State.Name;

    public static FlushProbe Create(FlushProbeId id)
    {
        var probe = new FlushProbe();
        probe.RaiseEvent(new FlushProbeStarted(id));
        return probe;
    }

    public void Rename(string name) => RaiseEvent(new FlushProbeRenamed(Id, name));
}
