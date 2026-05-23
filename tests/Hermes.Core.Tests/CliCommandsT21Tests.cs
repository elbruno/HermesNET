using Hermes.Core.Memory;
using Hermes.Core.Profiles;
using Hermes.Core.Skills;

namespace Hermes.Core.Tests;

/// <summary>
/// T21 — CLI Integration Tests for session, skill, and memory operations.
///
/// Covers service-level behaviors used by CLI commands:
///   - SessionCommand: create, list, switch, current
///   - SkillsCommand: list, show
///   - MemoryCommand: show (curated memory display)
///   - Error handling: missing IDs, missing profiles, invalid skills
/// </summary>
[Trait("Category", "CLI")]
public sealed class CliCommandsT21Tests : IAsyncLifetime
{
    private ProfileService _profileService = null!;
    private SessionService _sessionService = null!;
    private SkillRegistry _skillRegistry = null!;
    private MemoryStore _memoryStore = null!;
    private CuratedMemoryLoader _memoryLoader = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"hermes-cli-test-{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _profileService = new ProfileService(_connectionString);
        await _profileService.InitializeAsync();

        _sessionService = new SessionService(_connectionString, _profileService);
        await _sessionService.InitializeAsync();

        _skillRegistry = new SkillRegistry();

        _memoryStore = new MemoryStore(_connectionString);
        await _memoryStore.InitializeAsync();

        _memoryLoader = new CuratedMemoryLoader(_memoryStore, _profileService);
    }

    public Task DisposeAsync()
    {
        _sessionService.Dispose();
        _profileService.Dispose();
        _memoryStore.Dispose();
        return Task.CompletedTask;
    }

    // ── Session CLI: create ───────────────────────────────────────────────────

    [Fact]
    public async Task Session_Create_WithValidProfile_CreatesSession()
    {
        // CLI: hermes session create "TestSession" --profile <profileId>
        var profile = await _profileService.CreateProfileAsync("TestProfile");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "TestSession");

        session.Should().NotBeNull();
        session.Name.Should().Be("TestSession");
        session.ProfileId.Should().Be(profile.Id);
        session.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Session_Create_WithCurrentProfile_UsesCurrentProfile()
    {
        // CLI: hermes session create "DefaultSession" (uses current profile)
        var profile = await _profileService.CreateProfileAsync("CurrentProfile");
        await _profileService.SwitchProfileAsync(profile.Id);

        var session = await _sessionService.CreateSessionAsync(profile.Id, "DefaultSession");

        var current = await _profileService.GetCurrentProfileAsync();
        session.ProfileId.Should().Be(current!.Id);
    }

    [Fact]
    public async Task Session_Create_WithInvalidProfile_ThrowsKeyNotFoundException()
    {
        // CLI: hermes session create "Session" --profile ghost-id
        var act = () => _sessionService.CreateSessionAsync("ghost-profile", "Orphan");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Session CLI: list ─────────────────────────────────────────────────────

    [Fact]
    public async Task Session_List_WithValidProfile_ReturnsAllSessions()
    {
        // CLI: hermes session list --profile <profileId>
        var profile = await _profileService.CreateProfileAsync("ListProfile");
        var session1 = await _sessionService.CreateSessionAsync(profile.Id, "Session1");
        var session2 = await _sessionService.CreateSessionAsync(profile.Id, "Session2");

        var sessions = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(profile.Id))
            sessions.Add(s);

        sessions.Should().HaveCount(2);
        sessions.Should().Contain(s => s.Id == session1.Id);
        sessions.Should().Contain(s => s.Id == session2.Id);
    }

    [Fact]
    public async Task Session_List_WithCurrentProfile_ListsOnlyCurrentProfileSessions()
    {
        // CLI: hermes session list (lists current profile)
        var profile1 = await _profileService.CreateProfileAsync("Profile1");
        var profile2 = await _profileService.CreateProfileAsync("Profile2");
        
        var session1 = await _sessionService.CreateSessionAsync(profile1.Id, "Session1");
        var session2 = await _sessionService.CreateSessionAsync(profile2.Id, "Session2");

        await _profileService.SwitchProfileAsync(profile1.Id);

        var sessions = new List<ProfileSession>();
        await foreach (var s in _sessionService.ListSessionsByProfileAsync(profile1.Id))
            sessions.Add(s);

        sessions.Should().ContainSingle();
        sessions.First().Id.Should().Be(session1.Id);
    }

    // ── Session CLI: switch ───────────────────────────────────────────────────

    [Fact]
    public async Task Session_Switch_WithValidId_SetsCurrent()
    {
        // CLI: hermes session switch <id>
        var profile = await _profileService.CreateProfileAsync("SwitchProfile");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "SwitchSession");

        await _sessionService.SwitchSessionAsync(session.Id);
        var current = await _sessionService.GetCurrentSessionAsync();

        current.Should().NotBeNull();
        current!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task Session_Switch_WithInvalidId_ThrowsKeyNotFoundException()
    {
        // CLI: hermes session switch ghost-id
        var act = () => _sessionService.SwitchSessionAsync("ghost-session");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Session_Current_WithActiveSession_ReturnsCurrent()
    {
        // CLI: hermes session current
        var profile = await _profileService.CreateProfileAsync("CurrentProfile");
        var session = await _sessionService.CreateSessionAsync(profile.Id, "CurrentSession");
        await _sessionService.SwitchSessionAsync(session.Id);

        var current = await _sessionService.GetCurrentSessionAsync();

        current.Should().NotBeNull();
        current!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task Session_Current_WithNoActiveSession_ReturnsNull()
    {
        // CLI: hermes session current (when no session is set)
        var current = await _sessionService.GetCurrentSessionAsync();

        current.Should().BeNull();
    }

    // ── Memory CLI: show ──────────────────────────────────────────────────────

    [Fact]
    public async Task Memory_Show_WithValidProfile_LoadsMemory()
    {
        // CLI: hermes memory show --profile <profileId>
        var profile = await _profileService.CreateProfileAsync("MemProfile");
        var content = "# Profile Memory\n\nTest memory content";
        await _memoryStore.UpdateMemoryAsync(profile.Id, content);

        var memory = await _memoryLoader.LoadMemoryAsync(profile.Id);

        memory.Should().NotBeNull();
        memory.Content.Should().Contain("Test memory content");
        memory.Version.Should().Be(1);
    }

    [Fact]
    public async Task Memory_Show_WithCurrentProfile_LoadsCurrentMemory()
    {
        // CLI: hermes memory show (uses current profile)
        var profile = await _profileService.CreateProfileAsync("CurrentMemProfile");
        await _profileService.SwitchProfileAsync(profile.Id);
        var content = "# Current Memory\n\nCurrent profile memory";
        await _memoryStore.UpdateMemoryAsync(profile.Id, content);

        var memory = await _memoryLoader.LoadMemoryAsync(profile.Id);

        memory.Content.Should().Contain("Current profile memory");
    }

    [Fact]
    public async Task Memory_Show_WithInvalidProfile_ThrowsKeyNotFoundException()
    {
        // CLI: hermes memory show --profile ghost-id
        var act = () => _memoryLoader.LoadMemoryAsync("ghost-profile");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Memory_Show_WithEmptyMemory_ReturnsEmpty()
    {
        // CLI: hermes memory show (with no memory set)
        var profile = await _profileService.CreateProfileAsync("EmptyMemProfile");

        var memory = await _memoryLoader.LoadMemoryAsync(profile.Id);

        memory.IsEmpty.Should().BeTrue();
    }

    // ── Skill CLI: list ───────────────────────────────────────────────────────

    [Fact]
    public async Task Skill_List_WithSkillsRegistered_ReturnsAllSkills()
    {
        // CLI: hermes skill list
        var skill1 = new SkillDescriptor
        {
            Name = "TestSkill1",
            Type = "capability",
            Description = "First test skill"
        };
        var skill2 = new SkillDescriptor
        {
            Name = "TestSkill2",
            Type = "memory",
            Description = "Second test skill"
        };

        await _skillRegistry.RegisterSkillAsync(skill1);
        await _skillRegistry.RegisterSkillAsync(skill2);

        var skills = await _skillRegistry.ListSkillsAsync();

        skills.Should().HaveCountGreaterThanOrEqualTo(2);
        skills.Should().Contain(s => s.Name == "TestSkill1");
        skills.Should().Contain(s => s.Name == "TestSkill2");
    }

    [Fact]
    public async Task Skill_List_WithNoSkills_ReturnsEmptyList()
    {
        // CLI: hermes skill list (with no skills)
        var registry = new SkillRegistry();
        var skills = await registry.ListSkillsAsync();

        skills.Should().BeEmpty();
    }

    // ── Skill CLI: show ───────────────────────────────────────────────────────

    [Fact]
    public async Task Skill_Show_WithValidName_ReturnsSkillDetails()
    {
        // CLI: hermes skill show <name>
        var descriptor = new SkillDescriptor
        {
            Name = "DisplaySkill",
            Type = "capability",
            Description = "A skill to display",
            Category = "Testing"
        };
        await _skillRegistry.RegisterSkillAsync(descriptor);

        var skill = await _skillRegistry.FindByNameAsync("DisplaySkill");

        skill.Should().NotBeNull();
        skill!.Name.Should().Be("DisplaySkill");
        skill.Type.Should().Be("capability");
        skill.Category.Should().Be("Testing");
    }

    [Fact]
    public async Task Skill_Show_WithInvalidName_ReturnsNull()
    {
        // CLI: hermes skill show <invalid-name>
        var skill = await _skillRegistry.FindByNameAsync("NonExistentSkill");

        skill.Should().BeNull();
    }

    [Fact]
    public async Task Skill_Show_WithMetadata_ReturnsMetadata()
    {
        // CLI: hermes skill show <name> (displays metadata)
        var metadata = new Dictionary<string, string>
        {
            { "version", "1.0" },
            { "author", "test" }
        };
        var descriptor = new SkillDescriptor
        {
            Name = "SkillWithMetadata",
            Type = "capability",
            Description = "Skill with metadata",
            Metadata = metadata
        };
        await _skillRegistry.RegisterSkillAsync(descriptor);

        var retrievedMetadata = await _skillRegistry.GetSkillMetadataAsync("SkillWithMetadata");

        retrievedMetadata.Should().HaveCount(2);
        retrievedMetadata.Should().Contain("version", "1.0");
        retrievedMetadata.Should().Contain("author", "test");
    }
}
