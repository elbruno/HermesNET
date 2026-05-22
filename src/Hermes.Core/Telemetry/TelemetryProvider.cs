using System.Diagnostics;

namespace Hermes.Core.Telemetry;

/// <summary>
/// Central OpenTelemetry instrumentation provider for Hermes.
/// Creates and manages activities (spans) for turn latency, provider calls, and session persistence.
/// </summary>
public class TelemetryProvider
{
    private static readonly ActivitySource ActivitySource = new("Hermes.Core", "1.0.0");

    /// <summary>
    /// Start a turn span (root span wrapping the entire chat request).
    /// </summary>
    public static Activity? StartTurnSpan(string turnId)
    {
        var activity = ActivitySource.StartActivity("hermes.chat.turn");
        activity?.SetTag("turn.id", turnId);
        return activity;
    }

    /// <summary>
    /// Start a provider call span (child span for ChatClient.CompleteAsync).
    /// </summary>
    public static Activity? StartProviderCallSpan(string providerName)
    {
        var activity = ActivitySource.StartActivity("hermes.provider.call");
        activity?.SetTag("provider.name", providerName);
        return activity;
    }

    /// <summary>
    /// Start a session persist span (async, not part of turn latency gate).
    /// </summary>
    public static Activity? StartSessionPersistSpan(string sessionId)
    {
        var activity = ActivitySource.StartActivity("hermes.session.persist");
        activity?.SetTag("session.id", sessionId);
        return activity;
    }

    /// <summary>
    /// Set provider latency (ms) on the current activity.
    /// </summary>
    public static void SetProviderLatency(Activity? activity, long durationMs)
    {
        activity?.SetTag("provider.latency_ms", durationMs);
    }

    /// <summary>
    /// Set message length (characters) on the current activity.
    /// </summary>
    public static void SetMessageLength(Activity? activity, int length)
    {
        activity?.SetTag("message.length", length);
    }

    /// <summary>
    /// Set response length (characters) on the current activity.
    /// </summary>
    public static void SetResponseLength(Activity? activity, int length)
    {
        activity?.SetTag("response.length", length);
    }

    /// <summary>
    /// Get the ActivitySource for manual span creation.
    /// </summary>
    public static ActivitySource GetActivitySource() => ActivitySource;
}
