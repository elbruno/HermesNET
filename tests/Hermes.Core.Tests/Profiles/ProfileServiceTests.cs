using Hermes.Core.Profiles;

namespace Hermes.Core.Tests.Profiles;

/// <summary>
/// M2 — Unit tests for ProfileService (T13).
/// 
/// Covers:
///   - Profile CRUD round-trips (create, get, list, update, delete)
///   - Profile switching (atomic, persistent)
///   - Duplicate name validation (reject)
///   - Cascade cleanup (profile deletion clears current pointer)
///   - Error contracts: All missing-ID operations throw KeyNotFoundException
///   - State persistence across service restarts (same connection)
/// 
/// All tests use in-memory SQLite (Data Source=:memory:;Mode=Memory;Cache=Shared)
/// for per-test isolation and zero disk I/O.
/// 
/// Status: Scaffold ready; awaiting IProfileService implementation (Dallas T13).
/// </summary>
[Trait("Category", "Profiles")]
public sealed class ProfileServiceTests : IAsyncLifetime
{
    private ProfileService _profileService = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"hermes-test-{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _profileService = new ProfileService(_connectionString);
        await _profileService.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _profileService.Dispose();
        return Task.CompletedTask;
    }

    // ── Profile CRUD ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProfile_RoundTrip_ReturnsCorrectFields()
    {
        var profile = await _profileService.CreateProfileAsync("Work", "Work context");

        profile.Id.Should().NotBeNullOrEmpty();
        profile.Name.Should().Be("Work");
        profile.Description.Should().Be("Work context");
        profile.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
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
    public async Task ListProfiles_ReturnsAllInOrder()
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

    [Fact]
    public async Task UpdateProfile_ExistingId_ChangesNameAndDescription()
    {
        var original = await _profileService.CreateProfileAsync("OldName", "Old desc");
        var updated = await _profileService.UpdateProfileAsync(original.Id, "NewName", "New desc");

        updated.Name.Should().Be("NewName");
        updated.Description.Should().Be("New desc");
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    [Fact]
    public async Task UpdateProfile_MissingId_ThrowsKeyNotFoundException()
    {
        var act = () => _profileService.UpdateProfileAsync("ghost-id", "X", "Y");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost-id*");
    }

    [Fact]
    public async Task DeleteProfile_ExistingId_RemovesFromStore()
    {
        var profile = await _profileService.CreateProfileAsync("ToDelete");
        await _profileService.DeleteProfileAsync(profile.Id);

        var fetched = await _profileService.GetProfileAsync(profile.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfile_MissingId_ThrowsKeyNotFoundException()
    {
        var act = () => _profileService.DeleteProfileAsync("ghost-id");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost-id*");
    }

    [Fact]
    public async Task CreateProfile_DuplicateName_ThrowsInvalidOperationException()
    {
        await _profileService.CreateProfileAsync("Duplicate");
        var act = () => _profileService.CreateProfileAsync("Duplicate");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate*");
    }

    // ── Profile Switching ─────────────────────────────────────────────────────

    [Fact]
    public async Task SwitchProfile_ExistingId_SetsCurrent()
    {
        var p = await _profileService.CreateProfileAsync("Active");
        await _profileService.SwitchProfileAsync(p.Id);

        var current = await _profileService.GetCurrentProfileAsync();
        current.Should().NotBeNull();
        current!.Id.Should().Be(p.Id);
    }

    [Fact]
    public async Task SwitchProfile_MissingId_ThrowsKeyNotFoundException()
    {
        var act = () => _profileService.SwitchProfileAsync("ghost-id");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost-id*");
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

        var current = await _profileService.GetCurrentProfileAsync();
        current.Should().BeNull();
    }

    // ── State Persistence (same connection = simulates restart) ───────────────

    [Fact]
    public async Task CurrentProfile_PersistedAfterSwitch()
    {
        var p = await _profileService.CreateProfileAsync("Persist");
        await _profileService.SwitchProfileAsync(p.Id);

        // Create a new service instance on same connection (simulates restart)
        using var svc2 = new ProfileService(_connectionString);
        await svc2.InitializeAsync();

        var current = await svc2.GetCurrentProfileAsync();
        current.Should().NotBeNull();
        current!.Id.Should().Be(p.Id);
    }
}
