using System.Diagnostics;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using Microsoft.Extensions.Logging;

namespace GaWeCodes.Thessera.Core.Dispatching;

internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestPipeline<TResponse> pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var requestName = typeof(TRequest).Name;
        Log.RequestStarted(logger, requestName);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var response = await pipeline.NextAsync(cancellationToken).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);

            if (response.IsSuccess)
            {
                Log.RequestSucceeded(logger, requestName, elapsed.TotalMilliseconds);
            }
            else
            {
                var categories = string.Join(", ", response.Failures.Select(failure => failure.Category).Distinct());
                Log.RequestFailed(logger, requestName, categories, elapsed.TotalMilliseconds);
            }

            return response;
        }
        catch (Exception)
        {
            Log.RequestFaulted(logger, requestName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> RequestStartedMessage =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(1, nameof(RequestStarted)),
                "Handling {RequestName}");

        private static readonly Action<ILogger, string, double, Exception?> RequestSucceededMessage =
            LoggerMessage.Define<string, double>(
                LogLevel.Information,
                new EventId(2, nameof(RequestSucceeded)),
                "Handled {RequestName} successfully in {ElapsedMilliseconds:0.###} ms");

        private static readonly Action<ILogger, string, string, double, Exception?> RequestFailedMessage =
            LoggerMessage.Define<string, string, double>(
                LogLevel.Warning,
                new EventId(3, nameof(RequestFailed)),
                "Handled {RequestName} with failure categories [{FailureCategories}] in {ElapsedMilliseconds:0.###} ms");

        private static readonly Action<ILogger, string, double, Exception?> RequestFaultedMessage =
            LoggerMessage.Define<string, double>(
                LogLevel.Error,
                new EventId(4, nameof(RequestFaulted)),
                "Handling {RequestName} threw an unexpected exception after {ElapsedMilliseconds:0.###} ms");

        public static void RequestStarted(ILogger logger, string requestName) =>
            RequestStartedMessage(logger, requestName, null);

        public static void RequestSucceeded(ILogger logger, string requestName, double elapsedMilliseconds) =>
            RequestSucceededMessage(logger, requestName, elapsedMilliseconds, null);

        public static void RequestFailed(ILogger logger, string requestName, string failureCategories, double elapsedMilliseconds) =>
            RequestFailedMessage(logger, requestName, failureCategories, elapsedMilliseconds, null);

        public static void RequestFaulted(ILogger logger, string requestName, double elapsedMilliseconds) =>
            RequestFaultedMessage(logger, requestName, elapsedMilliseconds, null);
    }
}
