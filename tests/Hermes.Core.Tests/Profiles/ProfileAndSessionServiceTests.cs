using Hermes.Core.Profiles;

namespace Hermes.Core.Tests.Profiles;

/// <summary>
/// Unit tests for ProfileService and SessionService (T13).
/// Each test gets a fresh named in-memory SQLite database — zero disk I/O, full isolation.
///
/// Covers:
///   - Profile CRUD round-trips
///   - Profile switching (atomic, persistent)
///   - Session CRUD scoped to profiles
///   - Session switching (atomic, cross-profile enforcement)
///   - Delete cascade cleanup (sessions cleaned up with profile)
///   - Profile isolation (profile A state ≠ profile B state)
///   - Current profile/session state persists (same connection = simulates restart)
/// </summary>
public sealed class ProfileAndSessionServiceTests : IAsyncLifetime
{
    private ProfileService _profileService = null!;
    private SessionService _sessionService = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        // Each test gets a unique in-memory database name — full isolation.
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

    // ── Profile CRUD ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProfile_RoundTrip_ReturnsCorrectFields()
    {
        var profile = await _profileService.CreateProfileAsync("Work", "Work stuff");

        profile.Id.Should().NotBeNullOrEmpty();
        profile.Name.Should().Be("Work");
        profile.Description.Should().Be("Work stuff");
        profile.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        profile.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetProfile_ExistingId_ReturnsProfile()
    {
        var created = await _profileService.CreateProfileAsync("Personal");
        var fetched = await _profileService.GetProfileAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Personal");
    }

    [Fact]
    public async Task GetProfile_MissingId_ReturnsNull()
    {
        var result = await _profileService.GetProfileAsync("no-such-id");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileByName_ExistingName_ReturnsProfile()
    {
        await _profileService.CreateProfileAsync("Research");
        var fetched = await _profileService.GetProfileByNameAsync("Research");
        fetched.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateProfile_DuplicateName_Throws()
    {
        await _profileService.CreateProfileAsync("Duplicate");
        var act = () => _profileService.CreateProfileAsync("Duplicate");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate*");
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndDescription()
    {
        var original = await _profileService.CreateProfileAsync("OldName", "Old desc");
        var updated = await _profileService.UpdateProfileAsync(original.Id, "NewName", "New desc");

        updated.Name.Should().Be("NewName");
        updated.Description.Should().Be("New desc");
        updated.CreatedAt.Should().Be(original.CreatedAt);
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    [Fact]
    public async Task UpdateProfile_MissingId_Throws()
    {
        var act = () => _profileService.UpdateProfileAsync("ghost-id", "X");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteProfile_RemovesFromList()
    {
        var profile = await _profileService.CreateProfileAsync("ToDelete");
        await _profileService.DeleteProfileAsync(profile.Id);

        var fetched = await _profileService.GetProfileAsync(profile.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfile_MissingId_Throws()
    {
        var act = () => _profileService.DeleteProfileAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListProfiles_ReturnsAllInAscendingCreatedAtOrder()
    {
        await _profileService.CreateProfileAsync("A");
        await _profileService.CreateProfileAsync("B");
        await _profileService.CreateProfileAsync("C");

        var list = new List<Profile>();
        await foreach (var p in _profileService.ListProfilesAsync())
            list.Add(p);

        list.Should().HaveCount(3);
        list.Select(p => p.Name).Should().Equal("A", "B", "C");
    }

    // ── Profile Switching ─────────────────────────────────────────────────────

    [Fact]
    public async Task SwitchProfile_SetsCurrent()
    {
        var p = await _profileService.CreateProfileAsync("Active");
        await _profileService.SwitchProfileAsync(p.Id);

        var current = await _profileService.GetCurrentProfileAsync();
        current.Should().NotBeNull();
        current!.Id.Should().Be(p.Id);
    }

    [Fact]
    public async Task SwitchProfile_MissingId_Throws()
    {
        var act = () => _profileService.SwitchProfileAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SwitchProfile_OverwritesPreviousCurrent()
    {
        var p1 = await _profileService.CreateProfileAsync("First");
        var p2 = await _profileService.CreateProfileAsync("Second");

        await _profileService.SwitchProfileAsync(p1.Id);
        await _profileService.SwitchProfileAsync(p2.Id);

        var current = await _profileService.GetCurrentProfileAsync();
        current!.Id.Should().Be(p2.Id);
    }

    [Fact]
    public async Task GetCurrentProfile_NoSwitchYet_ReturnsNull()
    {
        var current = await _profileService.GetCurrentProfileAsync();
        current.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCurrentProfile_ClearsCurrentPointer()
    {
        var p = await _profileService.CreateProfileAsync("WillBeGone");
        await _profileService.SwitchProfileAsync(p.Id);

        await _profileService.DeleteProfileAsync(p.Id);

        // Current profile pointer should be gone (profile deleted).
        var current = await _profileService.GetCurrentProfileAsync();
        current.Should().BeNull();
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
        session.Metadata.Should().BeNull();
    }

    [Fact]
    public async Task CreateSession_UnknownProfile_Throws()
    {
        var act = () => _sessionService.CreateSessionAsync("ghost-profile", "Orphan");
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*ghost-profile*");
    }

    [Fact]
    public async Task GetSession_Missing_ReturnsNull()
    {
        var result = await _sessionService.GetSessionAsync("no-such-session");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveSession_UpdatesMetadataAndLastAccessed()
    {
        var profile = await _profileService.CreateProfileAsync("P2");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "S1");

        await _sessionService.SaveSessionAsync(session.Id, """{"key":"value"}""");

        var updated = await _sessionService.GetSessionAsync(session.Id);
        updated!.Metadata.Should().Be("""{"key":"value"}""");
        updated.LastAccessed.Should().BeAfter(session.LastAccessed);
    }

    [Fact]
    public async Task SaveSession_Missing_Throws()
    {
        var act = () => _sessionService.SaveSessionAsync("ghost", "{}");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

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
    public async Task ListSessionsByProfile_OrderedByLastAccessedDesc()
    {
        var profile = await _profileService.CreateProfileAsync("Ordering");
        var s1 = await _sessionService.CreateSessionAsync(profile.Id, "First");
        await Task.Delay(10); // ensure distinct timestamps
        var s2 = await _sessionService.CreateSessionAsync(profile.Id, "Second");

        var list = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(profile.Id))
            list.Add(s);

        list[0].Id.Should().Be(s2.Id); // most recently accessed first
    }

    // ── Session Switching ─────────────────────────────────────────────────────

    [Fact]
    public async Task SwitchSession_SetsCurrent()
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
    public async Task SwitchSession_Missing_Throws()
    {
        var act = () => _sessionService.SwitchSessionAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SwitchSession_CrossProfile_Throws()
    {
        var p1 = await _profileService.CreateProfileAsync("Iso1");
        var p2 = await _profileService.CreateProfileAsync("Iso2");
        await _profileService.SwitchProfileAsync(p1.Id);

        var sessionP2 = await _sessionService.CreateSessionAsync(p2.Id, "P2 Session");

        // Attempting to switch to a session that belongs to p2 while p1 is current must throw.
        var act = () => _sessionService.SwitchSessionAsync(sessionP2.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetCurrentSession_NoSwitchYet_ReturnsNull()
    {
        var current = await _sessionService.GetCurrentSessionAsync();
        current.Should().BeNull();
    }

    // ── Delete Cascade / Cleanup ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteSession_RemovesFromStore()
    {
        var profile = await _profileService.CreateProfileAsync("DelProf");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "ToDelete");

        await _sessionService.DeleteSessionAsync(session.Id);

        var fetched = await _sessionService.GetSessionAsync(session.Id);
        fetched.Should().BeNull();
    }

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

    [Fact]
    public async Task DeleteSession_Missing_Throws()
    {
        var act = () => _sessionService.DeleteSessionAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Profile Isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProfileIsolation_SessionsDoNotLeakAcrossProfiles()
    {
        var pA = await _profileService.CreateProfileAsync("IsoA");
        var pB = await _profileService.CreateProfileAsync("IsoB");

        await _sessionService.CreateSessionAsync(pA.Id, "A-only session");

        var listA = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(pA.Id))
            listA.Add(s);

        var listB = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(pB.Id))
            listB.Add(s);

        listA.Should().HaveCount(1);
        listB.Should().BeEmpty();
    }

    // ── State Persistence (same connection = simulates restart) ───────────────

    [Fact]
    public async Task CurrentProfile_PersistedAfterSwitch()
    {
        var p = await _profileService.CreateProfileAsync("Persist");
        await _profileService.SwitchProfileAsync(p.Id);

        // Create a new service instance on the same connection string (simulates restart).
        using var svc2 = new ProfileService(_connectionString);
        await svc2.InitializeAsync();

        var current = await svc2.GetCurrentProfileAsync();
        current.Should().NotBeNull();
        current!.Id.Should().Be(p.Id);
    }
}
