using Hermes.Core.Profiles;
using Hermes.Core.Telemetry;
using Hermes.Host.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Host.Controllers;

/// <summary>CRUD endpoints for assistant profiles.</summary>
[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>List all profiles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Profile>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProfiles(CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.profiles.list");

        var profiles = new List<Profile>();
        await foreach (var p in _profileService.ListProfilesAsync(ct))
            profiles.Add(p);
        return Ok(profiles);
    }

    /// <summary>Create a new profile.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateProfileRequest req,
        CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.profiles.create");

        var profile = await _profileService.CreateProfileAsync(req.Name, req.Description, ct);
        span?.SetTag("profile.id", profile.Id);
        return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
    }

    /// <summary>Get a profile by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(string id, CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.profiles.get");
        span?.SetTag("profile.id", id);

        var profile = await _profileService.GetProfileAsync(id, ct);
        if (profile is null)
            return NotFound(new ErrorResponse($"Profile '{id}' not found"));
        return Ok(profile);
    }

    /// <summary>Update a profile's name and/or description.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        string id,
        [FromBody] UpdateProfileRequest req,
        CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.profiles.update");
        span?.SetTag("profile.id", id);

        var profile = await _profileService.UpdateProfileAsync(id, req.Name, req.Description, ct);
        return Ok(profile);
    }

    /// <summary>Delete a profile and all its sessions.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile(string id, CancellationToken ct)
    {
        using var span = TelemetryProvider.GetActivitySource().StartActivity("hermes.api.profiles.delete");
        span?.SetTag("profile.id", id);

        await _profileService.DeleteProfileAsync(id, ct);
        return NoContent();
    }
}
