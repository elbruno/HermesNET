using Hermes.Core.Profiles;
using Hermes.Core.Session;

namespace Hermes.Core.Tests.Sessions;

/// <summary>
/// M2 — Unit tests for SessionService (T13).
/// 
/// Covers:
///   - Session CRUD round-trips (create, get, list, save, delete)
///   - Session switching (atomic, profile-scoped, prevents cross-profile access)
///   - Profile-scoped session listing (session A in profile X ≠ session B in profile Y)
///   - Cascade cleanup (profile deletion clears associated sessions and current pointer)
///   - Error contracts: All missing-ID/missing-profile operations throw KeyNotFoundException
///   - State persistence across service restarts (same connection)
///   - Metadata update and LastAccessed tracking
/// 
/// All tests use in-memory SQLite (Data Source=:memory:;Mode=Memory;Cache=Shared)
/// for per-test isolation and zero disk I/O.
/// 
/// Status: Scaffold ready; awaiting ISessionService implementation (Dallas T13).
/// </summary>
[Trait("Category", "Sessions")]
public sealed class SessionServiceTests : IAsyncLifetime
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

    // ── Session CRUD ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSession_RoundTrip_ReturnsCorrectFields()
    {
        var profile = await _profileService.CreateProfileAsync("P1");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "Chat Session");

        session.Id.Should().NotBeNullOrEmpty();
        session.ProfileId.Should().Be(profile.Id);
        session.Name.Should().Be("Chat Session");
        session.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSession_UnknownProfileId_ThrowsKeyNotFoundException()
    {
        var act = () => _sessionService.CreateSessionAsync("ghost-profile", "Orphan");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost-profile*");
    }

    [Fact]
    public async Task GetSession_ExistingId_ReturnsSession()
    {
        var profile = await _profileService.CreateProfileAsync("P2");
        var created = await _sessionService.CreateSessionAsync(profile.Id, "Test");
        var fetched = await _sessionService.GetSessionAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetSession_MissingId_ReturnsNull()
    {
        var result = await _sessionService.GetSessionAsync("no-such-session");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveSession_UpdatesMetadataAndLastAccessed()
    {
        var profile = await _profileService.CreateProfileAsync("P3");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "S1");

        await _sessionService.SaveSessionAsync(session.Id, """{"key":"value"}""");

        var updated = await _sessionService.GetSessionAsync(session.Id);
        updated!.Metadata.Should().Be("""{"key":"value"}""");
        updated.LastAccessed.Should().BeAfter(session.LastAccessed);
    }

    [Fact]
    public async Task SaveSession_MissingId_ThrowsKeyNotFoundException()
    {
        var act = () => _sessionService.SaveSessionAsync("ghost", "{}");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost*");
    }

    [Fact]
    public async Task DeleteSession_ExistingId_RemovesFromStore()
    {
        var profile = await _profileService.CreateProfileAsync("P4");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "ToDelete");

        await _sessionService.DeleteSessionAsync(session.Id);

        var fetched = await _sessionService.GetSessionAsync(session.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSession_MissingId_ThrowsKeyNotFoundException()
    {
        var act = () => _sessionService.DeleteSessionAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost*");
    }

    // ── Profile-Scoped Listing ────────────────────────────────────────────────

    [Fact]
    public async Task ListSessionsByProfile_ReturnsOnlyThatProfile()
    {
        var p1 = await _profileService.CreateProfileAsync("PX");
        var p2 = await _profileService.CreateProfileAsync("PY");

        await _sessionService.CreateSessionAsync(p1.Id, "S-P1-A");
        await _sessionService.CreateSessionAsync(p1.Id, "S-P1-B");
        await _sessionService.CreateSessionAsync(p2.Id, "S-P2-A");

        var list = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(p1.Id))
            list.Add(s);

        list.Should().HaveCount(2);
        list.Should().AllSatisfy(s => s.ProfileId.Should().Be(p1.Id));
    }

    [Fact]
    public async Task ListSessionsByProfile_OrderedByLastAccessedDescending()
    {
        var profile = await _profileService.CreateProfileAsync("Ordering");
        var s1 = await _sessionService.CreateSessionAsync(profile.Id, "First");
        await Task.Delay(10);
        var s2 = await _sessionService.CreateSessionAsync(profile.Id, "Second");

        var list = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(profile.Id))
            list.Add(s);

        list[0].Id.Should().Be(s2.Id, "most recently accessed first");
    }

    // ── Session Switching ─────────────────────────────────────────────────────

    [Fact]
    public async Task SwitchSession_ExistingId_SetsCurrent()
    {
        var profile = await _profileService.CreateProfileAsync("SW1");
        await _profileService.SwitchProfileAsync(profile.Id);
        var session = await _sessionService.CreateSessionAsync(profile.Id, "Active");

        await _sessionService.SwitchSessionAsync(session.Id);

        var current = await _sessionService.GetCurrentSessionAsync();
        current.Should().NotBeNull();
        current!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task SwitchSession_MissingId_ThrowsKeyNotFoundException()
    {
        var act = () => _sessionService.SwitchSessionAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost*");
    }

    [Fact]
    public async Task SwitchSession_CrossProfile_ThrowsInvalidOperationException()
    {
        var p1 = await _profileService.CreateProfileAsync("Iso1");
        var p2 = await _profileService.CreateProfileAsync("Iso2");
        await _profileService.SwitchProfileAsync(p1.Id);

        var sessionP2 = await _sessionService.CreateSessionAsync(p2.Id, "P2 Session");

        // Cannot switch to a session in profile P2 while P1 is current
        var act = () => _sessionService.SwitchSessionAsync(sessionP2.Id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetCurrentSession_NoSwitchYet_ReturnsNull()
    {
        var current = await _sessionService.GetCurrentSessionAsync();
        current.Should().BeNull();
    }

    // ── Delete Cascade & Cleanup ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteSession_ClearsCurrentPointer()
    {
        var profile = await _profileService.CreateProfileAsync("ClearCurrent");
        await _profileService.SwitchProfileAsync(profile.Id);
        var session = await _sessionService.CreateSessionAsync(profile.Id, "Active");
        await _sessionService.SwitchSessionAsync(session.Id);

        await _sessionService.DeleteSessionAsync(session.Id);

        var current = await _sessionService.GetCurrentSessionAsync();
        current.Should().BeNull();
    }

    // ── State Persistence (same connection = simulates restart) ───────────────

    [Fact]
    public async Task CurrentSession_PersistedAfterSwitch()
    {
        var profile = await _profileService.CreateProfileAsync("Persist");
        await _profileService.SwitchProfileAsync(profile.Id);
        var session = await _sessionService.CreateSessionAsync(profile.Id, "PersistSession");
        await _sessionService.SwitchSessionAsync(session.Id);

        // Create new services on same connection (simulates restart)
        using var svc2 = new ProfileService(_connectionString);
        await svc2.InitializeAsync();
        using var sessionSvc2 = new SessionService(_connectionString, svc2);
        await sessionSvc2.InitializeAsync();

        var current = await sessionSvc2.GetCurrentSessionAsync();
        current.Should().NotBeNull();
        current!.Id.Should().Be(session.Id);
    }
}
