using System.Diagnostics;
using System.Reflection;

namespace GaWeCodes.Thessera.Core.Telemetry;

internal static class ThesseraTelemetry
{
    public const string ActivitySourceName = "Thessera";

    public static ActivitySource Source { get; } = new(
        ActivitySourceName,
        typeof(ThesseraTelemetry).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static void MarkSucceeded(this Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.SetTag(TelemetryTags.Outcome, TelemetryTags.OutcomeSuccess);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    public static void MarkFailed(this Activity activity, string failureCategories)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.SetTag(TelemetryTags.Outcome, TelemetryTags.OutcomeFailure);
        activity.SetTag(TelemetryTags.FailureCategories, failureCategories);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    public static void MarkFaulted(this Activity activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(exception);

        activity.SetTag(TelemetryTags.Outcome, TelemetryTags.OutcomeFaulted);
        activity.SetTag(TelemetryTags.ExceptionType, exception.GetType().FullName);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
