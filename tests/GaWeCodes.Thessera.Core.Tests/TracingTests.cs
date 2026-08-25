using System.Diagnostics;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class TracingTests
{
    private const string ActivitySourceName = "Thessera";

    [Fact]
    public async Task ASucceedingCommand_ProducesOneSpanNamedAfterTheRequest()
    {
        using var recorder = new SpanRecorder("Send TracedCommand");

        var result = await SendAsync(new TracedCommand(Outcome.Success));

        Assert.True(result.IsSuccess);
        var span = Assert.Single(recorder.Spans);
        Assert.Equal("Send TracedCommand", span.DisplayName);
        Assert.Equal("TracedCommand", span.GetTagItem("thessera.request.name"));
        Assert.Equal("command", span.GetTagItem("thessera.request.kind"));
        Assert.Equal("success", span.GetTagItem("thessera.outcome"));
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
    }

    [Fact]
    public async Task AQuery_IsTaggedAsAQueryNotACommand()
    {
        using var recorder = new SpanRecorder("Send TracedQuery");

        var result = await SendQueryAsync();

        Assert.True(result.IsSuccess);
        var span = Assert.Single(recorder.Spans);
        Assert.Equal("Send TracedQuery", span.DisplayName);
        Assert.Equal("query", span.GetTagItem("thessera.request.kind"));
    }

    [Fact]
    public async Task AnExpectedDomainFailure_IsNotAnErrorSpanButNamesItsCategories()
    {
        using var recorder = new SpanRecorder("Send TracedCommand");

        var result = await SendAsync(new TracedCommand(Outcome.BusinessRule));

        Assert.True(result.IsFailure);
        var span = Assert.Single(recorder.Spans);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal("failure", span.GetTagItem("thessera.outcome"));
        Assert.Equal(
            FailureCategory.BusinessRule.ToString(),
            span.GetTagItem("thessera.failure.categories"));
    }

    [Fact]
    public async Task AnUnexpectedException_MarksTheSpanAsErrorAndNamesTheExceptionType()
    {
        using var recorder = new SpanRecorder("Send TracedCommand");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendAsync(new TracedCommand(Outcome.Throw)));

        var span = Assert.Single(recorder.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("faulted", span.GetTagItem("thessera.outcome"));
        Assert.Equal(typeof(InvalidOperationException).FullName, span.GetTagItem("thessera.exception.type"));
    }

    [Fact]
    public async Task WithoutAListener_TheGuardStillThrowsSynchronously()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddThessera(_ => { });

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = sender.SendAsync((ICommand)null!, CancellationToken.None);
        });
    }

    [Fact]
    public async Task EachProjectionHandler_GetsItsOwnSpanNamingTheHandlerAndTheAggregate()
    {
        using var recorder = new SpanRecorder(
            "Project TracedFirstProjection",
            "Project TracedSecondProjection");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IProjectionHandler<TracedDomainEvent>, TracedFirstProjection>();
        services.AddScoped<IProjectionHandler<TracedDomainEvent>, TracedSecondProjection>();
        services.AddThessera(_ => { });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ProjectionRunner>();

        var metadata = new DomainEventMetadata(
            Guid.NewGuid(),
            "probe",
            "probe-1",
            7,
            DateTimeOffset.UnixEpoch);

        await runner.RunAsync(new TracedDomainEvent(), metadata, CancellationToken.None);

        Assert.Equal(
            ["Project TracedFirstProjection", "Project TracedSecondProjection"],
            recorder.Spans.Select(span => span.DisplayName).Order(StringComparer.Ordinal));

        var projection = recorder.Spans.Single(span => span.DisplayName == "Project TracedFirstProjection");
        Assert.Equal(typeof(TracedFirstProjection).FullName, projection.GetTagItem("thessera.projection.handler"));
        Assert.Equal("probe", projection.GetTagItem("thessera.aggregate.name"));
        Assert.Equal("probe-1", projection.GetTagItem("thessera.aggregate.id"));
        Assert.Equal(7L, projection.GetTagItem("thessera.aggregate.version"));
    }

    [Fact]
    public async Task AProjectionSpan_IsNotNestedInsideThePublishSpan()
    {
        using var recorder = new SpanRecorder("Publish TracedDomainEvent", "Project TracedFirstProjection");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IProjectionHandler<TracedDomainEvent>, TracedFirstProjection>();
        services.AddThessera(_ => { });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var runner = scope.ServiceProvider.GetRequiredService<ProjectionRunner>();

        var metadata = new DomainEventMetadata(Guid.NewGuid(), "probe", "probe-1", 1, DateTimeOffset.UnixEpoch);
        await publisher.PublishAsync(new TracedDomainEvent(), metadata, new CountingSink(), CancellationToken.None);
        await runner.RunAsync(new TracedDomainEvent(), metadata, CancellationToken.None);

        var publish = recorder.Spans.Single(span => span.DisplayName == "Publish TracedDomainEvent");
        var projection = recorder.Spans.Single(span => span.DisplayName == "Project TracedFirstProjection");

        Assert.NotEqual(publish.SpanId, projection.ParentSpanId);
        Assert.Equal(0, publish.GetTagItem("thessera.integration_events.published"));
    }

    [Fact]
    public async Task ThePublishSpan_CountsThePublishedIntegrationEvents()
    {
        using var recorder = new SpanRecorder("Publish TracedDomainEvent");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IIntegrationEventMapper<TracedDomainEvent>, TracedMapper>();
        services.AddThessera(_ => { });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var sink = new CountingSink();
        var metadata = new DomainEventMetadata(Guid.NewGuid(), "probe", "probe-1", 1, DateTimeOffset.UnixEpoch);
        await publisher.PublishAsync(new TracedDomainEvent(), metadata, sink, CancellationToken.None);

        Assert.Equal(2, sink.Count);
        var publish = Assert.Single(recorder.Spans);
        Assert.Equal(2, publish.GetTagItem("thessera.integration_events.published"));
    }

    private static async Task<Result> SendAsync(TracedCommand command)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddScoped<ICommandHandler<TracedCommand>, TracedCommandHandler>();
        services.AddThessera(_ => { });

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        return await sender.SendAsync(command, CancellationToken.None);
    }

    private static async Task<Result<int>> SendQueryAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddScoped<IQueryHandler<TracedQuery, int>, TracedQueryHandler>();
        services.AddThessera(_ => { });

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        return await sender.SendAsync(new TracedQuery(), CancellationToken.None);
    }

    private enum Outcome
    {
        Success,
        BusinessRule,
        Throw,
    }

    private sealed record TracedCommand(Outcome Outcome) : ICommand;

    private sealed record TracedQuery : IQuery<int>;

    [EventName("traced-probe-v1")]
    private sealed record TracedDomainEvent : IDomainEvent;

    private sealed record TracedIntegrationEvent : IIntegrationEvent
    {
        public Guid EventId => Guid.Empty;

        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
    }

    private sealed class TracedCommandHandler : ICommandHandler<TracedCommand>
    {
        public Task<Result> HandleAsync(TracedCommand command, CancellationToken cancellationToken) => command.Outcome switch
        {
            Outcome.Success => Task.FromResult(Result.Success()),
            Outcome.BusinessRule => throw new BusinessRuleViolationException("Probe rule broken."),
            _ => throw new InvalidOperationException("Probe failure."),
        };
    }

    private sealed class TracedQueryHandler : IQueryHandler<TracedQuery, int>
    {
        public Task<Result<int>> HandleAsync(TracedQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result<int>.Success(1));
    }

    private sealed class TracedFirstProjection : IProjectionHandler<TracedDomainEvent>
    {
        public Task HandleAsync(TracedDomainEvent domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TracedSecondProjection : IProjectionHandler<TracedDomainEvent>
    {
        public Task HandleAsync(TracedDomainEvent domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TracedMapper : IIntegrationEventMapper<TracedDomainEvent>
    {
        public IReadOnlyCollection<IIntegrationEvent> Map(TracedDomainEvent domainEvent, DomainEventMetadata metadata) =>
            [new TracedIntegrationEvent(), new TracedIntegrationEvent()];
    }

    private sealed class CountingSink : IIntegrationEventSink
    {
        public int Count { get; private set; }

        public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SpanRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _spans = [];

        public SpanRecorder(params string[] displayNames)
        {
            var accepted = new HashSet<string>(displayNames, StringComparer.Ordinal);

            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    if (!accepted.Contains(activity.DisplayName))
                    {
                        return;
                    }

                    lock (_spans)
                    {
                        _spans.Add(activity);
                    }
                },
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Spans
        {
            get
            {
                lock (_spans)
                {
                    return [.. _spans];
                }
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
