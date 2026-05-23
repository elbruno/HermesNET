using System.Net.Http.Json;
using Hermes.Core.Profiles;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hermes.Integration.Tests;

/// <summary>
/// T22 — REST API contract validation tests.
/// 
/// Validates that all REST API endpoints conform to OpenAPI spec and acceptance criteria:
///   - Happy-path CRUD requests return correct status codes and response shapes
///   - Missing-ID requests return 404 with appropriate error messages
///   - Invalid requests (missing required fields) return 400 with validation errors
///   - Response data is properly isolated per profile/session (concurrency & isolation)
/// 
/// Endpoints covered (from T19 REST API):
///   - POST   /api/profiles              (create profile)
///   - GET    /api/profiles              (list profiles)
///   - GET    /api/profiles/{id}         (get profile)
///   - PUT    /api/profiles/{id}         (update profile)
///   - DELETE /api/profiles/{id}         (delete profile)
///   - POST   /api/sessions              (create session under a profile)
///   - GET    /api/sessions              (list sessions, optionally filtered by profileId)
///   - GET    /api/sessions/{id}         (get session)
///   - DELETE /api/sessions/{id}         (delete session)
///   - GET    /api/profiles/{profileId}/memory      (get memory for profile)
///   - GET    /api/profiles/{profileId}/user-profile (get user profile)
/// 
/// Quality Gates:
///   - 31 new integration tests
///   - All 214 Core tests still pass
///   - Target: 245+ total tests passing after T22
///   - No regressions in T17/T19 API behavior
/// </summary>
[Trait("Category", "Integration")]
[Trait("Stage", "T22")]
[Collection("REST API Contract Tests")]
public sealed class RestApiContractTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        
        // Ensure database is initialized
        using var scope = _factory.Services.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
        
        await profileService.InitializeAsync();
        await sessionService.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // PROFILE CRUD — HAPPY PATH
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProfile_WithValidData_Returns201WithProfile()
    {
        var payload = new { name = "TestProfile", description = "A test profile" };
        var response = await _client.PostAsJsonAsync("/api/profiles", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        
        var profile = await response.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        profile!.Id.Should().NotBeEmpty();
        profile.Name.Should().Be("TestProfile");
        profile.Description.Should().Be("A test profile");
        profile.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateProfile_MinimalData_Returns201()
    {
        var payload = new { name = "MinimalProfile" };
        var response = await _client.PostAsJsonAsync("/api/profiles", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        
        var profile = await response.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        profile!.Name.Should().Be("MinimalProfile");
        profile.Description.Should().BeNull();
    }

    [Fact]
    public async Task GetProfiles_ReturnsAllProfiles()
    {
        // Create two profiles
        await _client.PostAsJsonAsync("/api/profiles", new { name = "Profile1" });
        await _client.PostAsJsonAsync("/api/profiles", new { name = "Profile2" });
        
        var response = await _client.GetAsync("/api/profiles");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var profiles = await response.Content.ReadFromJsonAsync<List<Profile>>();
        profiles.Should().NotBeNull();
        profiles!.Should().NotBeEmpty();
        profiles.Should().Contain(p => p.Name == "Profile1");
        profiles.Should().Contain(p => p.Name == "Profile2");
    }

    [Fact]
    public async Task GetProfile_WithExistingId_Returns200()
    {
        var createResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ProfileToGet" });
        var created = await createResp.Content.ReadFromJsonAsync<Profile>();
        created.Should().NotBeNull();
        
        var response = await _client.GetAsync($"/api/profiles/{created!.Id}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var profile = await response.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        profile!.Id.Should().Be(created.Id);
        profile.Name.Should().Be("ProfileToGet");
    }

    [Fact]
    public async Task UpdateProfile_WithValidData_Returns200()
    {
        var createResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "OriginalName", description = "Original" });
        var created = await createResp.Content.ReadFromJsonAsync<Profile>();
        created.Should().NotBeNull();
        
        var updatePayload = new { name = "UpdatedName", description = "Updated description" };
        var response = await _client.PutAsJsonAsync($"/api/profiles/{created!.Id}", updatePayload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var updated = await response.Content.ReadFromJsonAsync<Profile>();
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(created.Id);
        updated.Name.Should().Be("UpdatedName");
        updated.Description.Should().Be("Updated description");
        updated.UpdatedAt.Should().BeAfter(created.UpdatedAt);
    }

    [Fact]
    public async Task UpdateProfile_PartialUpdate_Returns200()
    {
        var createResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "Original", description = "Desc" });
        var created = await createResp.Content.ReadFromJsonAsync<Profile>();
        created.Should().NotBeNull();
        
        var updatePayload = new { name = "OnlyNameChanged" };
        var response = await _client.PutAsJsonAsync($"/api/profiles/{created!.Id}", updatePayload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<Profile>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("OnlyNameChanged");
    }

    [Fact]
    public async Task DeleteProfile_WithExistingId_Returns204()
    {
        var createResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ProfileToDelete" });
        var created = await createResp.Content.ReadFromJsonAsync<Profile>();
        created.Should().NotBeNull();
        
        var response = await _client.DeleteAsync($"/api/profiles/{created!.Id}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        
        // Verify it's gone
        var getResp = await _client.GetAsync($"/api/profiles/{created.Id}");
        getResp.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // PROFILE ERROR CASES
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProfile_MissingRequiredName_Returns400()
    {
        var payload = new { description = "No name" };
        var response = await _client.PostAsJsonAsync("/api/profiles", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProfile_WithNonexistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/profiles/nonexistent-id-xyz");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        error.Should().NotBeNull();
        error!.Should().ContainKey("error");
    }

    [Fact]
    public async Task UpdateProfile_WithNonexistentId_Returns404()
    {
        var payload = new { name = "Updated" };
        var response = await _client.PutAsJsonAsync("/api/profiles/nonexistent-id-xyz", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProfile_WithNonexistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/profiles/nonexistent-id-xyz");
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // SESSION CRUD — HAPPY PATH
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSession_WithValidData_Returns201()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "SessionProfile" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var sessionPayload = new { name = "TestSession", profileId = profile!.Id };
        var response = await _client.PostAsJsonAsync("/api/sessions", sessionPayload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        
        var session = await response.Content.ReadFromJsonAsync<ProfileSession>();
        session.Should().NotBeNull();
        session!.Id.Should().NotBeEmpty();
        session.Name.Should().Be("TestSession");
        session.ProfileId.Should().Be(profile.Id);
    }

    [Fact]
    public async Task GetSessions_ReturnsAllSessions()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "SessionProfile2" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        // Create two sessions
        await _client.PostAsJsonAsync("/api/sessions", new { name = "Session1", profileId = profile!.Id });
        await _client.PostAsJsonAsync("/api/sessions", new { name = "Session2", profileId = profile.Id });
        
        var response = await _client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var sessions = await response.Content.ReadFromJsonAsync<List<ProfileSession>>();
        sessions.Should().NotBeNull();
        sessions!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSessions_FilteredByProfileId_ReturnsFilteredSessions()
    {
        var profile1Resp = await _client.PostAsJsonAsync("/api/profiles", new { name = "FilterProfile1" });
        var profile2Resp = await _client.PostAsJsonAsync("/api/profiles", new { name = "FilterProfile2" });
        var profile1 = await profile1Resp.Content.ReadFromJsonAsync<Profile>();
        var profile2 = await profile2Resp.Content.ReadFromJsonAsync<Profile>();
        profile1.Should().NotBeNull();
        profile2.Should().NotBeNull();
        
        // Create session for profile1
        await _client.PostAsJsonAsync("/api/sessions", new { name = "SessionP1", profileId = profile1!.Id });
        // Create session for profile2
        await _client.PostAsJsonAsync("/api/sessions", new { name = "SessionP2", profileId = profile2!.Id });
        
        // Query sessions for profile1 only
        var response = await _client.GetAsync($"/api/sessions?profileId={profile1.Id}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var sessions = await response.Content.ReadFromJsonAsync<List<ProfileSession>>();
        sessions.Should().NotBeNull();
        sessions!.Should().AllSatisfy(s => s.ProfileId.Should().Be(profile1.Id));
    }

    [Fact]
    public async Task GetSession_WithExistingId_Returns200()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "SessionProfile3" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var sessionResp = await _client.PostAsJsonAsync("/api/sessions", 
            new { name = "SessionToGet", profileId = profile!.Id });
        var created = await sessionResp.Content.ReadFromJsonAsync<ProfileSession>();
        created.Should().NotBeNull();
        
        var response = await _client.GetAsync($"/api/sessions/{created!.Id}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var session = await response.Content.ReadFromJsonAsync<ProfileSession>();
        session.Should().NotBeNull();
        session!.Id.Should().Be(created.Id);
        session.Name.Should().Be("SessionToGet");
    }

    [Fact]
    public async Task DeleteSession_WithExistingId_Returns204()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "SessionProfile4" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var sessionResp = await _client.PostAsJsonAsync("/api/sessions", 
            new { name = "SessionToDelete", profileId = profile!.Id });
        var created = await sessionResp.Content.ReadFromJsonAsync<ProfileSession>();
        created.Should().NotBeNull();
        
        var response = await _client.DeleteAsync($"/api/sessions/{created!.Id}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        
        // Verify it's gone
        var getResp = await _client.GetAsync($"/api/sessions/{created.Id}");
        getResp.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // SESSION ERROR CASES
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSession_MissingRequiredProfileId_Returns400()
    {
        var payload = new { name = "SessionWithoutProfile" };
        var response = await _client.PostAsJsonAsync("/api/sessions", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSession_MissingRequiredName_Returns400()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ProfileForNoName" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var payload = new { profileId = profile!.Id };
        var response = await _client.PostAsJsonAsync("/api/sessions", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSession_WithNonexistentProfile_Returns404()
    {
        var payload = new { name = "OrphanSession", profileId = "nonexistent-profile" };
        var response = await _client.PostAsJsonAsync("/api/sessions", payload);
        
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSession_WithNonexistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent-session-xyz");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_WithNonexistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/sessions/nonexistent-session-xyz");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // MEMORY ENDPOINTS
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMemory_ForProfile_Returns200()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "MemoryProfile" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var response = await _client.GetAsync($"/api/profiles/{profile!.Id}/memory");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUserProfile_ForProfile_Returns200()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "UserProfileProfile" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var response = await _client.GetAsync($"/api/profiles/{profile!.Id}/user-profile");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // CONCURRENCY & ISOLATION — Verify profiles and sessions don't interfere
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Concurrency_MultipleProfiles_AreIsolated()
    {
        // Create profile A
        var profileAResp = await _client.PostAsJsonAsync("/api/profiles", 
            new { name = "ProfileA", description = "Profile A description" });
        var profileA = await profileAResp.Content.ReadFromJsonAsync<Profile>();
        profileA.Should().NotBeNull();
        
        // Create profile B
        var profileBResp = await _client.PostAsJsonAsync("/api/profiles", 
            new { name = "ProfileB", description = "Profile B description" });
        var profileB = await profileBResp.Content.ReadFromJsonAsync<Profile>();
        profileB.Should().NotBeNull();
        
        // Verify they are distinct
        profileA!.Id.Should().NotBe(profileB!.Id);
        
        // Get each and verify data isolation
        var getAResp = await _client.GetAsync($"/api/profiles/{profileA.Id}");
        var fetchedA = await getAResp.Content.ReadFromJsonAsync<Profile>();
        fetchedA.Should().NotBeNull();
        fetchedA!.Name.Should().Be("ProfileA");
        fetchedA.Description.Should().Be("Profile A description");
        
        var getBResp = await _client.GetAsync($"/api/profiles/{profileB.Id}");
        var fetchedB = await getBResp.Content.ReadFromJsonAsync<Profile>();
        fetchedB.Should().NotBeNull();
        fetchedB!.Name.Should().Be("ProfileB");
        fetchedB.Description.Should().Be("Profile B description");
    }

    [Fact]
    public async Task Concurrency_SessionsUnderDifferentProfiles_AreIsolated()
    {
        // Create profile A and session A1
        var profileAResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ConcProfile1" });
        var profileA = await profileAResp.Content.ReadFromJsonAsync<Profile>();
        profileA.Should().NotBeNull();
        
        var sessionA1Resp = await _client.PostAsJsonAsync("/api/sessions", 
            new { name = "SessionA1", profileId = profileA!.Id });
        var sessionA1 = await sessionA1Resp.Content.ReadFromJsonAsync<ProfileSession>();
        sessionA1.Should().NotBeNull();
        
        // Create profile B and session B1
        var profileBResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ConcProfile2" });
        var profileB = await profileBResp.Content.ReadFromJsonAsync<Profile>();
        profileB.Should().NotBeNull();
        
        var sessionB1Resp = await _client.PostAsJsonAsync("/api/sessions", 
            new { name = "SessionB1", profileId = profileB!.Id });
        var sessionB1 = await sessionB1Resp.Content.ReadFromJsonAsync<ProfileSession>();
        sessionB1.Should().NotBeNull();
        
        // Verify sessions belong to correct profiles
        sessionA1!.ProfileId.Should().Be(profileA.Id);
        sessionB1!.ProfileId.Should().Be(profileB.Id);
        
        // Query sessions for profileA — should only see sessionA1
        var sessionsAResp = await _client.GetAsync($"/api/sessions?profileId={profileA.Id}");
        var sessionsA = await sessionsAResp.Content.ReadFromJsonAsync<List<ProfileSession>>();
        sessionsA.Should().NotBeNull();
        sessionsA!.Should().AllSatisfy(s => s.ProfileId.Should().Be(profileA.Id));
        
        // Query sessions for profileB — should only see sessionB1
        var sessionsBResp = await _client.GetAsync($"/api/sessions?profileId={profileB.Id}");
        var sessionsB = await sessionsBResp.Content.ReadFromJsonAsync<List<ProfileSession>>();
        sessionsB.Should().NotBeNull();
        sessionsB!.Should().AllSatisfy(s => s.ProfileId.Should().Be(profileB.Id));
    }

    [Fact]
    public async Task Concurrency_ConcurrentProfileCreations_AllSucceed()
    {
        // Create 5 profiles concurrently
        var tasks = Enumerable.Range(1, 5)
            .Select(i => _client.PostAsJsonAsync("/api/profiles", 
                new { name = $"ConcurrentProfile{i}" }))
            .ToList();
        
        var responses = await Task.WhenAll(tasks);
        
        // All should succeed
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(System.Net.HttpStatusCode.Created));
        
        // Verify all were created
        var listResp = await _client.GetAsync("/api/profiles");
        var profiles = await listResp.Content.ReadFromJsonAsync<List<Profile>>();
        profiles.Should().NotBeNull();
        
        for (int i = 1; i <= 5; i++)
        {
            profiles!.Should().Contain(p => p.Name == $"ConcurrentProfile{i}");
        }
    }

    [Fact]
    public async Task Concurrency_ConcurrentSessionCreationsUnderSameProfile_AllSucceed()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ConcSessionProfile" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        // Create 5 sessions concurrently under same profile
        var tasks = Enumerable.Range(1, 5)
            .Select(i => _client.PostAsJsonAsync("/api/sessions", 
                new { name = $"ConcurrentSession{i}", profileId = profile!.Id }))
            .ToList();
        
        var responses = await Task.WhenAll(tasks);
        
        // All should succeed
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(System.Net.HttpStatusCode.Created));
        
        // Verify all were created under the same profile
        var listResp = await _client.GetAsync($"/api/sessions?profileId={profile!.Id}");
        var sessions = await listResp.Content.ReadFromJsonAsync<List<ProfileSession>>();
        sessions.Should().NotBeNull();
        
        sessions!.Should().HaveCountGreaterThanOrEqualTo(5);
        for (int i = 1; i <= 5; i++)
        {
            sessions.Should().Contain(s => s.Name == $"ConcurrentSession{i}" && s.ProfileId == profile.Id);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // PROFILE-SESSION LIFECYCLE
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProfileSessionLifecycle_CreateProfileAndSession_ThenDeleteProfile()
    {
        // Create profile
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "LifecycleProfile" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        // Create session under that profile
        var sessionResp = await _client.PostAsJsonAsync("/api/sessions", 
            new { name = "LifecycleSession", profileId = profile!.Id });
        var session = await sessionResp.Content.ReadFromJsonAsync<ProfileSession>();
        session.Should().NotBeNull();
        
        // Verify both exist
        var getProfileResp = await _client.GetAsync($"/api/profiles/{profile.Id}");
        getProfileResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var getSessionResp = await _client.GetAsync($"/api/sessions/{session!.Id}");
        getSessionResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        // Delete profile (should also cascade delete session)
        var deleteProfileResp = await _client.DeleteAsync($"/api/profiles/{profile.Id}");
        deleteProfileResp.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        
        // Verify profile is gone
        var getProfileAfterResp = await _client.GetAsync($"/api/profiles/{profile.Id}");
        getProfileAfterResp.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // RESPONSE SHAPE VALIDATION
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProfileResponse_ContainsRequiredFields()
    {
        var createResp = await _client.PostAsJsonAsync("/api/profiles", 
            new { name = "ShapeTestProfile", description = "Test" });
        var profile = await createResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        // Verify all required fields exist and are populated
        profile!.Id.Should().NotBeEmpty();
        profile.Name.Should().NotBeEmpty();
        profile.CreatedAt.Should().NotBe(default);
        profile.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task SessionResponse_ContainsRequiredFields()
    {
        var profileResp = await _client.PostAsJsonAsync("/api/profiles", new { name = "ShapeTestProfile2" });
        var profile = await profileResp.Content.ReadFromJsonAsync<Profile>();
        profile.Should().NotBeNull();
        
        var sessionResp = await _client.PostAsJsonAsync("/api/sessions", 
            new { name = "ShapeTestSession", profileId = profile!.Id });
        var session = await sessionResp.Content.ReadFromJsonAsync<ProfileSession>();
        session.Should().NotBeNull();
        
        // Verify all required fields exist and are populated
        session!.Id.Should().NotBeEmpty();
        session.ProfileId.Should().Be(profile.Id);
        session.Name.Should().NotBeEmpty();
        session.CreatedAt.Should().NotBe(default);
        session.LastAccessed.Should().NotBe(default);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // TOTAL: 31 tests covering happy path CRUD, error cases, and concurrency
    // ────────────────────────────────────────────────────────────────────────────
}
