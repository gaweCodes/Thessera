using System.Collections.Concurrent;
using System.Diagnostics;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Dispatching;

internal sealed class RequestSender(IServiceProvider serviceProvider) : ISender
{
    private readonly record struct DispatcherKey(Type Request, Type Result);

    private static readonly ConcurrentDictionary<Type, CommandDispatcher> CommandDispatchers = new();
    private static readonly ConcurrentDictionary<DispatcherKey, object> CommandWithResultDispatchers = new();
    private static readonly ConcurrentDictionary<DispatcherKey, object> QueryDispatchers = new();

    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var dispatcher = CommandDispatchers.GetOrAdd(
            command.GetType(),
            static type => (CommandDispatcher)Activator.CreateInstance(
                typeof(CommandDispatcher<>).MakeGenericType(type))!);

        return TraceAsync(
            command.GetType(),
            TelemetryTags.RequestKindCommand,
            ct => dispatcher.DispatchAsync(command, serviceProvider, ct),
            cancellationToken);
    }

    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(command);

        var dispatcher = (CommandWithResultDispatcher<TResult>)CommandWithResultDispatchers.GetOrAdd(
            new DispatcherKey(command.GetType(), typeof(TResult)),
            static key => Activator.CreateInstance(
                typeof(CommandWithResultDispatcher<,>).MakeGenericType(key.Request, key.Result))!);

        return TraceAsync(
            command.GetType(),
            TelemetryTags.RequestKindCommand,
            ct => dispatcher.DispatchAsync(command, serviceProvider, ct),
            cancellationToken);
    }

    public Task<Result<TResult>> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(query);

        var dispatcher = (QueryDispatcher<TResult>)QueryDispatchers.GetOrAdd(
            new DispatcherKey(query.GetType(), typeof(TResult)),
            static key => Activator.CreateInstance(
                typeof(QueryDispatcher<,>).MakeGenericType(key.Request, key.Result))!);

        return TraceAsync(
            query.GetType(),
            TelemetryTags.RequestKindQuery,
            ct => dispatcher.DispatchAsync(query, serviceProvider, ct),
            cancellationToken);
    }

    private static async Task<TResponse> TraceAsync<TResponse>(
        Type requestType,
        string requestKind,
        Func<CancellationToken, Task<TResponse>> dispatchAsync,
        CancellationToken cancellationToken)
        where TResponse : Result
    {
        if (!ThesseraTelemetry.Source.HasListeners())
        {
            return await dispatchAsync(cancellationToken).ConfigureAwait(false);
        }

        var requestName = requestType.Name;
        using var activity = ThesseraTelemetry.Source.StartActivity(
            $"Send {requestName}",
            ActivityKind.Internal);

        activity?.SetTag(TelemetryTags.RequestName, requestName);
        activity?.SetTag(TelemetryTags.RequestKind, requestKind);

        try
        {
            var response = await dispatchAsync(cancellationToken).ConfigureAwait(false);

            if (activity is not null)
            {
                if (response.IsSuccess)
                {
                    activity.MarkSucceeded();
                }
                else
                {
                    activity.MarkFailed(
                        string.Join(",", response.Failures.Select(failure => failure.Category).Distinct()));
                }
            }

            return response;
        }
        catch (Exception exception)
        {
            activity?.MarkFaulted(exception);
            throw;
        }
    }

    private static RequestPipelineContinuation<TResponse> BuildPipeline<TRequest, TResponse>(
        TRequest request,
        RequestPipelineContinuation<TResponse> handler,
        Func<IReadOnlyList<Failure>, TResponse> failed,
        IServiceProvider services)
    {
        var registry = services.GetService<PipelineBehaviorRegistry>();
        var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        var ordered = registry is null
            ? behaviors
            : behaviors.OrderByDescending(behavior => registry.GetOrder(behavior.GetType()));

        var pipeline = handler;
        foreach (var behavior in ordered)
        {
            var current = behavior;
            var stage = new RequestPipeline<TResponse>(pipeline, failed);
            pipeline = cancellationToken => current.HandleAsync(request, stage, cancellationToken);
        }

        return pipeline;
    }

    private abstract class CommandDispatcher
    {
        public abstract Task<Result> DispatchAsync(ICommand command, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class CommandDispatcher<TCommand> : CommandDispatcher
        where TCommand : ICommand
    {
        public override Task<Result> DispatchAsync(ICommand command, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typedCommand = (TCommand)command;
            var handler = services.GetRequiredService<ICommandHandler<TCommand>>();
            var pipeline = BuildPipeline<TCommand, Result>(
                typedCommand,
                ct => handler.HandleAsync(typedCommand, ct),
                Result.Failed,
                services);
            return pipeline(cancellationToken);
        }
    }

    private abstract class CommandWithResultDispatcher<TResult>
        where TResult : notnull
    {
        public abstract Task<Result<TResult>> DispatchAsync(ICommand<TResult> command, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class CommandWithResultDispatcher<TCommand, TResult> : CommandWithResultDispatcher<TResult>
        where TCommand : ICommand<TResult>
        where TResult : notnull
    {
        public override Task<Result<TResult>> DispatchAsync(ICommand<TResult> command, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typedCommand = (TCommand)command;
            var handler = services.GetRequiredService<ICommandHandler<TCommand, TResult>>();
            var pipeline = BuildPipeline<TCommand, Result<TResult>>(
                typedCommand,
                ct => handler.HandleAsync(typedCommand, ct),
                Result<TResult>.Failed,
                services);
            return pipeline(cancellationToken);
        }
    }

    private abstract class QueryDispatcher<TResult>
        where TResult : notnull
    {
        public abstract Task<Result<TResult>> DispatchAsync(IQuery<TResult> query, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class QueryDispatcher<TQuery, TResult> : QueryDispatcher<TResult>
        where TQuery : IQuery<TResult>
        where TResult : notnull
    {
        public override Task<Result<TResult>> DispatchAsync(IQuery<TResult> query, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typedQuery = (TQuery)query;
            var handler = services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
            var pipeline = BuildPipeline<TQuery, Result<TResult>>(
                typedQuery,
                ct => handler.HandleAsync(typedQuery, ct),
                Result<TResult>.Failed,
                services);
            return pipeline(cancellationToken);
        }
    }
}
