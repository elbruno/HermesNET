using System.Collections.Concurrent;

namespace Hermes.Core.Policy;

/// <summary>
/// Simple in-memory sliding-window rate limiter.
/// Tracks request counts per profile (and optionally per session) using a 1-hour window.
/// Thread-safe via <see cref="ConcurrentDictionary"/> + lock-free timestamp queues.
/// </summary>
public sealed class RateLimitPolicy
{
    private readonly int _maxRequestsPerHour;
    private readonly int _maxRequestsPerSession;

    // profileId → ordered timestamps of recent requests
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _profileWindows = new();

    // sessionId → ordered timestamps of recent requests
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sessionWindows = new();

    private readonly object _lock = new();

    public RateLimitPolicy(int maxRequestsPerHour = 1_000, int maxRequestsPerSession = 500)
    {
        _maxRequestsPerHour    = maxRequestsPerHour;
        _maxRequestsPerSession = maxRequestsPerSession;
    }

    // ── Evaluation ────────────────────────────────────────────────────────────

    public PolicyResult Evaluate(string profileId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return PolicyResult.Deny(
                "Rate limit check failed: profile ID is null or empty",
                new Dictionary<string, string> { ["rule"] = "empty-profile-id" });

        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromHours(1);

        lock (_lock)
        {
            // ── Profile window ────────────────────────────────────────────────
            var profileQueue = _profileWindows.GetOrAdd(profileId, _ => new Queue<DateTimeOffset>());
            Trim(profileQueue, now, window);

            if (profileQueue.Count >= _maxRequestsPerHour)
                return PolicyResult.Deny(
                    $"Rate limit exceeded for profile '{profileId}': {profileQueue.Count}/{_maxRequestsPerHour} requests in the last hour",
                    new Dictionary<string, string>
                    {
                        ["rule"]       = "profile-hourly-limit",
                        ["profile_id"] = profileId,
                        ["count"]      = profileQueue.Count.ToString(),
                        ["limit"]      = _maxRequestsPerHour.ToString(),
                    });

            // ── Session window ────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var sessionQueue = _sessionWindows.GetOrAdd(sessionId, _ => new Queue<DateTimeOffset>());
                Trim(sessionQueue, now, window);

                if (sessionQueue.Count >= _maxRequestsPerSession)
                    return PolicyResult.Deny(
                        $"Rate limit exceeded for session '{sessionId}': {sessionQueue.Count}/{_maxRequestsPerSession} requests in the last hour",
                        new Dictionary<string, string>
                        {
                            ["rule"]       = "session-hourly-limit",
                            ["profile_id"] = profileId,
                            ["session_id"] = sessionId,
                            ["count"]      = sessionQueue.Count.ToString(),
                            ["limit"]      = _maxRequestsPerSession.ToString(),
                        });

                sessionQueue.Enqueue(now);
            }

            profileQueue.Enqueue(now);
        }

        return PolicyResult.Allow(
            $"Rate limit OK for profile '{profileId}'",
            new Dictionary<string, string>
            {
                ["profile_id"] = profileId,
                ["session_id"] = sessionId ?? string.Empty,
            });
    }

    // ── Reset (for testing) ───────────────────────────────────────────────────

    public void Reset(string profileId)
    {
        _profileWindows.TryRemove(profileId, out _);
    }

    public void ResetSession(string sessionId)
    {
        _sessionWindows.TryRemove(sessionId, out _);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static void Trim(Queue<DateTimeOffset> queue, DateTimeOffset now, TimeSpan window)
    {
        while (queue.Count > 0 && now - queue.Peek() >= window)
            queue.Dequeue();
    }

    // ── Exposed for tests ─────────────────────────────────────────────────────

    public int MaxRequestsPerHour    => _maxRequestsPerHour;
    public int MaxRequestsPerSession => _maxRequestsPerSession;
}
