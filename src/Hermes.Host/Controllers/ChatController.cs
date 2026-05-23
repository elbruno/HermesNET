using System.Text.Json;
using Hermes.Core.Profiles;
using Hermes.Core.Services;
using Hermes.Core.Telemetry;
using Hermes.Host.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Host.Controllers;

/// <summary>POST /api/chat — streams assistant tokens via Server-Sent Events.</summary>
[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly IHermesChatService _chatService;
    private readonly IProfileService _profileService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<ChatController> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChatController(
        IHermesChatService chatService,
        IProfileService profileService,
        ISessionService sessionService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _profileService = profileService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message and receives a streaming SSE response.
    /// Response content-type is text/event-stream.
    /// Events: "token" (string) and "done" ({"sessionId","turnId"}).
    /// </summary>
    /// <remarks>
    /// SSE stream format:
    ///
    ///     event: token
    ///     data: "Hello"
    ///
    ///     event: done
    ///     data: {"sessionId":"...","turnId":"..."}
    ///
    /// Returns 400 if required fields are missing or session not found.
    /// Returns 404 if profile not found.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task PostChat([FromBody] ChatRequest req, CancellationToken ct)
    {
        // Validate before touching response — once streaming starts we cannot change status code.
        if (string.IsNullOrWhiteSpace(req.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new ErrorResponse("message is required"), ct);
            return;
        }

        var profile = await _profileService.GetProfileAsync(req.ProfileId, ct);
        if (profile is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(new ErrorResponse($"Profile '{req.ProfileId}' not found"), ct);
            return;
        }

        var session = await _sessionService.GetSessionAsync(req.SessionId, ct);
        if (session is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new ErrorResponse($"Session '{req.SessionId}' not found"), ct);
            return;
        }

        var turnId = Guid.NewGuid().ToString();
        using var span = TelemetryProvider.StartTurnSpan(turnId);
        span?.SetTag("profile.id", req.ProfileId);
        span?.SetTag("session.id", req.SessionId);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var token in _chatService.StreamChatAsync(req.Message, req.ProfileId, req.SessionId, ct))
            {
                var tokenData = JsonSerializer.Serialize(token);
                await Response.WriteAsync($"event: token\ndata: {tokenData}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            var doneData = JsonSerializer.Serialize(
                new { sessionId = req.SessionId, turnId },
                JsonOpts);
            await Response.WriteAsync($"event: done\ndata: {doneData}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected — graceful stop, no error.
            _logger.LogDebug("SSE stream cancelled by client (turn={TurnId})", turnId);
        }
    }
}
