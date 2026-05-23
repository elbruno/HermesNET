using Hermes.Core.Profiles;
using Hermes.Core.Telemetry;
using Hermes.Host.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Host.Controllers;

/// <summary>CRUD endpoints for chat sessions.</summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IProfileService _profileService;

    public SessionsController(ISessionService sessionService, IProfileService profileService)
    {
        _sessionService = sessionService;
        _profileService = profileService;
    }

    /// <summary>List all sessions, optionally filtered by profileId.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProfileSession>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSessions(
        [FromQuery] string? profileId,
        CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.sessions.list");
        span?.SetTag("profile.id", profileId ?? "");

        var sessions = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsAsync(profileId, ct))
            sessions.Add(s);
        return Ok(sessions);
    }

    /// <summary>Create a new session under the specified profile.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProfileSession), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest req,
        CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.sessions.create");

        var profile = await _profileService.GetProfileAsync(req.ProfileId, ct);
        if (profile is null)
            return NotFound(new ErrorResponse($"Profile '{req.ProfileId}' not found"));

        var session = await _sessionService.CreateSessionAsync(req.ProfileId, req.Name, ct);
        span?.SetTag("session.id", session.Id);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
    }

    /// <summary>Get a session by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProfileSession), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(string id, CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.sessions.get");
        span?.SetTag("session.id", id);

        var session = await _sessionService.GetSessionAsync(id, ct);
        if (session is null)
            return NotFound(new ErrorResponse($"Session '{id}' not found"));
        return Ok(session);
    }

    /// <summary>Delete a session by ID.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(string id, CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.sessions.delete");
        span?.SetTag("session.id", id);

        await _sessionService.DeleteSessionAsync(id, ct);
        return NoContent();
    }
}
