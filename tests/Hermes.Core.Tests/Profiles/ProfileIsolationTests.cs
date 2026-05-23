using Hermes.Core.Profiles;

namespace Hermes.Core.Tests.Profiles;

/// <summary>
/// M2 — Profile isolation regression tests (T13).
/// 
/// Validates that operations on Profile A do not leak state into Profile B:
///   - Profile A's sessions ≠ Profile B's sessions (per-profile namespace)
///   - Profile A's current session ≠ Profile B's current session
///   - Switching to Profile B does not affect Profile A's data
///   - Deleting Profile A does not delete Profile B's data
/// 
/// Regression goal: Prevent profile-state bleeding bugs from M1 forward.
/// 
/// Status: Scaffold ready; awaiting IProfileService + ISessionService (Dallas T13).
/// </summary>
[Trait("Category", "Profiles")]
public sealed class ProfileIsolationTests : IAsyncLifetime
{
    private ProfileService _profileService = null!;
    private SessionService _sessionService = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"hermes-test-{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _profileService = new ProfileService(_connectionString);
        await _profileService.InitializeAsync();

        _sessionService = new SessionService(_connectionString, _profileService);
        await _sessionService.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _sessionService.Dispose();
        _profileService.Dispose();
        return Task.CompletedTask;
    }

    // ── Session-Level Isolation ───────────────────────────────────────────────

    [Fact]
    public async Task ProfileA_SessionsDoNotAppearIn_ProfileB_SessionList()
    {
        var pA = await _profileService.CreateProfileAsync("IsoA");
        var pB = await _profileService.CreateProfileAsync("IsoB");

        await _sessionService.CreateSessionAsync(pA.Id, "A-session");
        await _sessionService.CreateSessionAsync(pB.Id, "B-session");

        var listA = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(pA.Id))
            listA.Add(s);

        var listB = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(pB.Id))
            listB.Add(s);

        listA.Should().HaveCount(1);
        listA.First().Name.Should().Be("A-session");

        listB.Should().HaveCount(1);
        listB.First().Name.Should().Be("B-session");
    }

    // ── Current Session Isolation ─────────────────────────────────────────────

    [Fact(Skip = "M3 feature: current_session_id is not yet per-profile scoped; single global AppState key used")]
    public async Task SwitchingProfile_DoesNotAffectOtherProfilesCurrentSession()
    {
        var pA = await _profileService.CreateProfileAsync("CurA");
        var pB = await _profileService.CreateProfileAsync("CurB");

        await _profileService.SwitchProfileAsync(pA.Id);
        var sA = await _sessionService.CreateSessionAsync(pA.Id, "Session-A");
        await _sessionService.SwitchSessionAsync(sA.Id);

        // Switch to profile B
        await _profileService.SwitchProfileAsync(pB.Id);
        var sB = await _sessionService.CreateSessionAsync(pB.Id, "Session-B");
        await _sessionService.SwitchSessionAsync(sB.Id);

        // B's current is sB; A's current should still be sA
        var currentB = await _sessionService.GetCurrentSessionAsync();
        currentB!.Id.Should().Be(sB.Id);

        // Switch back to A — verify sA is still the current
        await _profileService.SwitchProfileAsync(pA.Id);
        var currentA = await _sessionService.GetCurrentSessionAsync();
        currentA!.Id.Should().Be(sA.Id);
    }

    // ── Deletion Isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProfile_DoesNotAffectOtherProfiles()
    {
        var pA = await _profileService.CreateProfileAsync("DelA");
        var pB = await _profileService.CreateProfileAsync("DelB");

        await _profileService.DeleteProfileAsync(pA.Id);

        // Profile B should still exist and be fetchable
        var fetched = await _profileService.GetProfileAsync(pB.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("DelB");
    }

    [Fact]
    public async Task DeleteProfile_DoesNotAffectOtherProfilesSessions()
    {
        var pA = await _profileService.CreateProfileAsync("DelSessionA");
        var pB = await _profileService.CreateProfileAsync("DelSessionB");

        var sA = await _sessionService.CreateSessionAsync(pA.Id, "A-session");
        var sB = await _sessionService.CreateSessionAsync(pB.Id, "B-session");

        // Delete profile A (no cascade yet — M3 feature)
        await _profileService.DeleteProfileAsync(pA.Id);

        // Profile B's session should still exist
        var fetched = await _sessionService.GetSessionAsync(sB.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("B-session");
    }

    // ── State Mutation Isolation ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_DoesNotAffectOtherProfiles()
    {
        var pA = await _profileService.CreateProfileAsync("UpdateA", "original");
        var pB = await _profileService.CreateProfileAsync("UpdateB", "original");

        await _profileService.UpdateProfileAsync(pA.Id, "UpdateA-New", "modified");

        // Profile B should remain unchanged
        var fetchedB = await _profileService.GetProfileAsync(pB.Id);
        fetchedB.Should().NotBeNull();
        fetchedB!.Name.Should().Be("UpdateB");
        fetchedB.Description.Should().Be("original");
    }
}
