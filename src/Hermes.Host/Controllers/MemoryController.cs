using Hermes.Core.Memory;
using Hermes.Core.Telemetry;
using Hermes.Host.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Host.Controllers;

/// <summary>Read-only memory endpoints scoped to a profile (M2).</summary>
[ApiController]
[Route("api/profiles/{profileId}")]
public sealed class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;

    public MemoryController(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <summary>Get the curated memory snapshot (MEMORY.md) for a profile.</summary>
    [HttpGet("memory")]
    [ProducesResponseType(typeof(MemoryContext), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMemory(string profileId, CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.memory.get");
        span?.SetTag("profile.id", profileId);

        var context = await _memoryService.LoadMemoryAsync(profileId, ct);
        return Ok(context);
    }

    /// <summary>Get the user profile snapshot (USER.md) for a profile.</summary>
    [HttpGet("user-profile")]
    [ProducesResponseType(typeof(UserProfileData), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserProfile(string profileId, CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.user-profile.get");
        span?.SetTag("profile.id", profileId);

        var data = await _memoryService.LoadUserProfileAsync(profileId, ct);
        return Ok(data);
    }
}
