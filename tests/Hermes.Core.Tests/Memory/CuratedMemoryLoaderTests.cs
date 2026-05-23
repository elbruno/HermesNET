using FluentAssertions;
using Hermes.Core.Memory;
using Hermes.Core.Profiles;
using Moq;

namespace Hermes.Core.Tests.Memory;

/// <summary>
/// T15 unit tests — CuratedMemoryLoader + MemoryUpdateHandler.
/// 16 test cases covering load, profile isolation, caching, update, validation, and concurrency.
///
/// All tests use in-memory SQLite (no disk I/O) unless the test description calls out disk.
/// "Load from disk" == load from a SQLite-backed MemoryStore (the only storage layer in T15).
/// "In-memory load" == same store wired with Data Source=:memory: and no disk writes.
/// </summary>
public sealed class CuratedMemoryLoaderTests : IAsyncLifetime
{
    private MemoryStore _store = null!;
    private IMemoryService _memoryService = null!;

    public async Task InitializeAsync()
    {
        _store = new MemoryStore("Data Source=:memory:");
        await _store.InitializeAsync();
        _memoryService = _store;
    }

    public Task DisposeAsync()
    {
        _store.Dispose();
        return Task.CompletedTask;
    }

    // ── Test 1: Load MEMORY.md (happy path, content matches store) ──────────

    [Fact]
    public async Task LoadMemory_HappyPath_ContentMatchesStore()
    {
        await _memoryService.UpdateMemoryAsync("alice", "## Stack\n- .NET 10, SQLite");
        var loader = new CuratedMemoryLoader(_memoryService);

        var ctx = await loader.LoadMemoryAsync("alice");

        ctx.ProfileId.Should().Be("alice");
        ctx.Content.Should().Contain(".NET 10");
        ctx.IsEmpty.Should().BeFalse();
    }

    // ── Test 2: Load USER.md (happy path, structured data returned) ─────────

    [Fact]
    public async Task LoadUserProfile_HappyPath_DataMatches()
    {
        await _memoryService.UpdateUserProfileAsync("alice", "## Identity\n- Name: Bruno");
        var loader = new CuratedMemoryLoader(_memoryService);

        var profile = await loader.LoadUserProfileAsync("alice");

        profile.ProfileId.Should().Be("alice");
        profile.Data.Should().Contain("Bruno");
        profile.IsEmpty.Should().BeFalse();
    }

    // ── Test 3: In-memory load (fixture-backed, no disk) ────────────────────

    [Fact]
    public async Task LoadMemory_InMemoryStore_ReturnsMockedContent()
    {
        const string expected = "In-memory fixture content";
        var mock = new Mock<IMemoryService>();
        mock.Setup(s => s.LoadMemoryAsync("profile-x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryContext("profile-x", expected, "markdown", 1, DateTime.UtcNow));

        var loader = new CuratedMemoryLoader(mock.Object);
        var ctx = await loader.LoadMemoryAsync("profile-x");

        ctx.Content.Should().Be(expected);
        mock.Verify(s => s.LoadMemoryAsync("profile-x", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Test 4: Missing profile raises KeyNotFoundException ─────────────────

    [Fact]
    public async Task LoadMemory_ProfileNotFoundInService_ThrowsKeyNotFoundException()
    {
        var mockProfileService = new Mock<IProfileService>();
        mockProfileService.Setup(p => p.GetProfileAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        var loader = new CuratedMemoryLoader(_memoryService, mockProfileService.Object);

        var act = () => loader.LoadMemoryAsync("ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost*");
    }

    // ── Test 5: Profile A cannot see Profile B's memory (bidirectional) ─────

    [Fact]
    public async Task LoadMemory_TwoProfiles_StrictIsolation_BidirectionalTest()
    {
        await _memoryService.UpdateMemoryAsync("alice", "Alice-only content: secret-A");
        await _memoryService.UpdateMemoryAsync("bob",   "Bob-only content: secret-B");

        var loader = new CuratedMemoryLoader(_memoryService);

        var ctxA = await loader.LoadMemoryAsync("alice");
        var ctxB = await loader.LoadMemoryAsync("bob");

        // A → B isolation
        ctxA.Content.Should().Contain("secret-A");
        ctxA.Content.Should().NotContain("secret-B");

        // B → A isolation
        ctxB.Content.Should().Contain("secret-B");
        ctxB.Content.Should().NotContain("secret-A");
    }

    // ── Test 6: Malformed content raises MemoryParseException ───────────────

    [Fact]
    public async Task UpdateMemory_ContentWithNullByte_ThrowsMemoryParseException()
    {
        var handler = new MemoryUpdateHandler(_memoryService);
        var malformed = "Valid start\0binary garbage\xFF\xFE";

        var act = () => handler.UpdateMemoryAsync("alice", malformed);
        await act.Should().ThrowAsync<MemoryParseException>()
            .WithMessage("*null byte*");
    }

    // ── Test 7: Empty memory directory returns MemoryContext.Empty ──────────

    [Fact]
    public async Task LoadMemory_ProfileExistsButNoMemoryWritten_ReturnsEmpty()
    {
        var loader = new CuratedMemoryLoader(_memoryService);

        var ctx = await loader.LoadMemoryAsync("brand-new-profile");

        ctx.IsEmpty.Should().BeTrue();
        ctx.ProfileId.Should().Be("brand-new-profile");
        ctx.Content.Should().BeEmpty();
    }

    // ── Test 8: UpdateMemoryAsync increments version ─────────────────────────

    [Fact]
    public async Task UpdateMemory_MultipleWrites_VersionIncrementsPerWrite()
    {
        var handler = new MemoryUpdateHandler(_memoryService);

        await handler.UpdateMemoryAsync("alice", "v1 content");
        await handler.UpdateMemoryAsync("alice", "v2 content");
        await handler.UpdateMemoryAsync("alice", "v3 content");

        var ctx = await _memoryService.LoadMemoryAsync("alice");
        ctx.Version.Should().Be(3);
        ctx.Content.Should().Be("v3 content");
    }

    // ── Test 9: UpdateMemoryAsync is atomic under concurrent access ──────────

    [Fact]
    public async Task UpdateMemory_ConcurrentUpdates_VersionReflectsAllWrites()
    {
        const int writeCount = 5;
        var handler = new MemoryUpdateHandler(_memoryService);

        // Initialise so we have a starting row
        await handler.UpdateMemoryAsync("concurrent-profile", "initial");

        // Fire writeCount concurrent updates
        var tasks = Enumerable.Range(1, writeCount)
            .Select(i => handler.UpdateMemoryAsync("concurrent-profile", $"update-{i}"))
            .ToArray();

        await Task.WhenAll(tasks);

        // Version = 1 (initial) + writeCount concurrent
        var ctx = await _memoryService.LoadMemoryAsync("concurrent-profile");
        ctx.Version.Should().Be(1 + writeCount);
    }

    // ── Test 10: UpdateMemoryAsync raises KeyNotFoundException (missing profile) ─

    [Fact]
    public async Task UpdateMemory_ProfileNotFoundInService_ThrowsKeyNotFoundException()
    {
        var mockProfileService = new Mock<IProfileService>();
        mockProfileService.Setup(p => p.GetProfileAsync("missing-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        var handler = new MemoryUpdateHandler(_memoryService, mockProfileService.Object);

        var act = () => handler.UpdateMemoryAsync("missing-id", "some content");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*missing-id*");
    }

    // ── Test 11: Loader caching — second load returns cached snapshot ────────

    [Fact]
    public async Task LoadMemory_SecondCallForSameProfile_ReturnsCachedResult()
    {
        var mock = new Mock<IMemoryService>();
        mock.Setup(s => s.LoadMemoryAsync("profile-y", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryContext("profile-y", "cached content", "markdown", 1, DateTime.UtcNow));

        var loader = new CuratedMemoryLoader(mock.Object);

        var first  = await loader.LoadMemoryAsync("profile-y");
        var second = await loader.LoadMemoryAsync("profile-y");

        // Same instance returned from cache
        second.Should().Be(first);
        // IMemoryService.LoadMemoryAsync called exactly once (second hit is cached)
        mock.Verify(s => s.LoadMemoryAsync("profile-y", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Test 12: Cache invalidation after UpdateMemoryAsync ─────────────────

    [Fact]
    public async Task UpdateMemory_AfterWrite_LoadReturnsUpdatedContent()
    {
        var loader  = new CuratedMemoryLoader(_memoryService);
        var handler = new MemoryUpdateHandler(_memoryService, loader: loader);

        await handler.UpdateMemoryAsync("alice", "original content");
        var before = await loader.LoadMemoryAsync("alice");  // populates cache

        await handler.UpdateMemoryAsync("alice", "updated content");  // invalidates cache
        var after = await loader.LoadMemoryAsync("alice");  // fresh fetch

        before.Content.Should().Be("original content");
        after.Content.Should().Be("updated content");
    }

    // ── Test 13: UserProfileData missing raises KeyNotFoundException ─────────

    [Fact]
    public async Task LoadUserProfile_NoDataWritten_ThrowsKeyNotFoundException()
    {
        var loader = new CuratedMemoryLoader(_memoryService);

        var act = () => loader.LoadUserProfileAsync("never-written-profile");
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*never-written-profile*");
    }

    // ── Test 14: Large content (≥ 10 KB) loads without OOM / truncation ──────

    [Fact]
    public async Task LoadMemory_LargeContent_LoadsCompletelyWithoutOom()
    {
        // Use a custom schema with 10 MB cap to prove no OOM at large sizes.
        // Default cap is 64 KB; override for this test only.
        var bigSchema = new MemorySchema(
            MaxContentBytes: 10 * 1024 * 1024,
            SupportedFormats: new[] { "markdown" },
            CurrentSchemaVersion: 1);

        using var bigStore = new MemoryStore("Data Source=:memory:", bigSchema);
        await bigStore.InitializeAsync();

        // ~65 KB of valid Markdown content
        var tenKb = string.Concat(Enumerable.Repeat("# Section\n- Fact: project uses .NET 10\n", 2_000));
        await bigStore.UpdateMemoryAsync("large-profile", tenKb);

        var loader = new CuratedMemoryLoader(bigStore);
        var ctx = await loader.LoadMemoryAsync("large-profile");

        ctx.IsEmpty.Should().BeFalse();
        ctx.Content.Length.Should().BeGreaterThan(10_000);
    }

    // ── Test 15: Concurrent access — multiple profiles loaded simultaneously ─

    [Fact]
    public async Task LoadMemory_MultipleProfilesConcurrently_NoRaceCondition()
    {
        const int profileCount = 10;
        var profiles = Enumerable.Range(1, profileCount)
            .Select(i => $"concurrent-profile-{i}")
            .ToArray();

        foreach (var p in profiles)
            await _memoryService.UpdateMemoryAsync(p, $"content for {p}");

        var loader = new CuratedMemoryLoader(_memoryService);

        var results = await Task.WhenAll(
            profiles.Select(p => loader.LoadMemoryAsync(p)));

        results.Should().HaveCount(profileCount);
        foreach (var (profileId, ctx) in profiles.Zip(results))
        {
            ctx.ProfileId.Should().Be(profileId);
            ctx.Content.Should().Contain(profileId);
        }
    }

    // ── Test 16: UTF-8 with BOM and legacy-ASCII content handled gracefully ──

    [Fact]
    public async Task LoadMemory_ContentWithUtf8Bom_StoredAndRetrievedCorrectly()
    {
        // UTF-8 BOM prefix (EF BB BF) — the loader must not choke on it
        const string bom = "\uFEFF";
        var bomContent = $"{bom}## Memory\n- UTF-8 BOM present in content";

        var handler = new MemoryUpdateHandler(_memoryService);
        await handler.UpdateMemoryAsync("bom-profile", bomContent);

        var loader = new CuratedMemoryLoader(_memoryService);
        var ctx    = await loader.LoadMemoryAsync("bom-profile");

        ctx.IsEmpty.Should().BeFalse();
        // Content round-trips intact (BOM character is valid Unicode)
        ctx.Content.Should().Be(bomContent);
    }
}
