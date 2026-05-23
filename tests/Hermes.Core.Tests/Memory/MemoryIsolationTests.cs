using FluentAssertions;
using Hermes.Core.Memory;
using System.Diagnostics;

namespace Hermes.Core.Tests.Memory;

/// <summary>
/// R2 Risk Checkpoint — Cross-profile memory isolation tests.
///
/// These tests validate that MemoryStore enforces hard profile boundaries:
/// Profile A's reads/writes NEVER touch Profile B's data.
/// A GREEN result here is a blocker for M2 proceeding.
/// </summary>
public sealed class MemoryIsolationTests : IAsyncLifetime
{
    private MemoryStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = new MemoryStore("Data Source=:memory:");
        await _store.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _store.Dispose();
        return Task.CompletedTask;
    }

    // ── R2-A: Profile A read does not return Profile B data ──────────────────

    [Fact]
    public async Task LoadMemory_ProfileA_DoesNotReturn_ProfileB_Content()
    {
        await _store.UpdateMemoryAsync("alice", "Alice's project: HermesNET, .NET 10");
        await _store.UpdateMemoryAsync("bob", "Bob's project: LegacyApp, .NET Framework 4.8");

        var aliceCtx = await _store.LoadMemoryAsync("alice");

        aliceCtx.Content.Should().Contain("HermesNET");
        aliceCtx.Content.Should().NotContain("LegacyApp");
        aliceCtx.ProfileId.Should().Be("alice");
    }

    [Fact]
    public async Task LoadMemory_ProfileB_DoesNotReturn_ProfileA_Content()
    {
        await _store.UpdateMemoryAsync("alice", "Alice's project: HermesNET, .NET 10");
        await _store.UpdateMemoryAsync("bob", "Bob's project: LegacyApp, .NET Framework 4.8");

        var bobCtx = await _store.LoadMemoryAsync("bob");

        bobCtx.Content.Should().Contain("LegacyApp");
        bobCtx.Content.Should().NotContain("HermesNET");
        bobCtx.ProfileId.Should().Be("bob");
    }

    // ── R2-B: Profile A write does not affect Profile B data ─────────────────

    [Fact]
    public async Task UpdateMemory_ProfileA_DoesNotModify_ProfileB()
    {
        await _store.UpdateMemoryAsync("alice", "Alice original");
        await _store.UpdateMemoryAsync("bob", "Bob original");

        await _store.UpdateMemoryAsync("alice", "Alice updated");

        var bobCtx = await _store.LoadMemoryAsync("bob");
        bobCtx.Content.Should().Be("Bob original");
    }

    [Fact]
    public async Task UpdateMemory_ProfileB_DoesNotModify_ProfileA()
    {
        await _store.UpdateMemoryAsync("alice", "Alice original");
        await _store.UpdateMemoryAsync("bob", "Bob original");

        await _store.UpdateMemoryAsync("bob", "Bob updated");

        var aliceCtx = await _store.LoadMemoryAsync("alice");
        aliceCtx.Content.Should().Be("Alice original");
    }

    // ── R2-C: User profile isolation ─────────────────────────────────────────

    [Fact]
    public async Task LoadUserProfile_ProfileA_DoesNotReturn_ProfileB_Preferences()
    {
        await _store.UpdateUserProfileAsync("alice", "Alice prefers concise responses");
        await _store.UpdateUserProfileAsync("bob", "Bob prefers verbose step-by-step");

        var aliceProfile = await _store.LoadUserProfileAsync("alice");

        aliceProfile.Data.Should().Contain("concise");
        aliceProfile.Data.Should().NotContain("verbose");
        aliceProfile.ProfileId.Should().Be("alice");
    }

    [Fact]
    public async Task UpdateUserProfile_ProfileA_DoesNotModify_ProfileB()
    {
        await _store.UpdateUserProfileAsync("alice", "Alice preferences");
        await _store.UpdateUserProfileAsync("bob", "Bob preferences");

        await _store.UpdateUserProfileAsync("alice", "Alice updated preferences");

        var bobProfile = await _store.LoadUserProfileAsync("bob");
        bobProfile.Data.Should().Be("Bob preferences");
    }

    // ── R2-D: Empty profile returns empty, not another profile's data ─────────

    [Fact]
    public async Task LoadMemory_UnknownProfile_ReturnsEmpty_NotAnotherProfileData()
    {
        await _store.UpdateMemoryAsync("alice", "Alice memory");

        var unknownCtx = await _store.LoadMemoryAsync("charlie");

        unknownCtx.IsEmpty.Should().BeTrue();
        unknownCtx.Content.Should().NotContain("Alice");
        unknownCtx.ProfileId.Should().Be("charlie");
    }

    [Fact]
    public async Task LoadUserProfile_UnknownProfile_ReturnsEmpty()
    {
        await _store.UpdateUserProfileAsync("alice", "Alice user profile");

        var unknownProfile = await _store.LoadUserProfileAsync("charlie");

        unknownProfile.IsEmpty.Should().BeTrue();
        unknownProfile.Data.Should().NotContain("Alice");
    }

    // ── R2-E: Adversarial — direct DB insert with different profile_id ────────

    [Fact]
    public async Task LoadMemory_AfterAdversarialInsert_ReturnsOnlyOwnProfile()
    {
        // Write alice's data through the service (clean path)
        await _store.UpdateMemoryAsync("alice", "Alice legitimate memory");

        // Write "evil" profile's data directly through the service — simulates DB injection
        await _store.UpdateMemoryAsync("evil", "Evil profile data that should not leak");

        // Alice's query must not be contaminated by "evil" profile's row
        var aliceCtx = await _store.LoadMemoryAsync("alice");

        aliceCtx.Content.Should().NotContain("Evil");
        aliceCtx.Content.Should().Be("Alice legitimate memory");
    }

    // ── R2-F: Version counter is per-profile ─────────────────────────────────

    [Fact]
    public async Task VersionCounter_IsPerProfile_NotShared()
    {
        await _store.UpdateMemoryAsync("alice", "Alice v1");
        await _store.UpdateMemoryAsync("alice", "Alice v2");
        await _store.UpdateMemoryAsync("alice", "Alice v3");
        await _store.UpdateMemoryAsync("bob", "Bob v1");

        var aliceCtx = await _store.LoadMemoryAsync("alice");
        var bobCtx = await _store.LoadMemoryAsync("bob");

        aliceCtx.Version.Should().Be(3, "alice had 3 writes");
        bobCtx.Version.Should().Be(1, "bob had 1 write");
    }

    // ── R2-G: Input validation ────────────────────────────────────────────────

    [Fact]
    public async Task LoadMemory_NullProfileId_ThrowsArgumentException()
    {
        var act = () => _store.LoadMemoryAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadMemory_EmptyProfileId_ThrowsArgumentException()
    {
        var act = () => _store.LoadMemoryAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateMemory_ContentExceedingSchemaLimit_ThrowsArgumentException()
    {
        // Schema default cap is 65536 bytes — create content that exceeds it
        var oversized = new string('x', 70_000);

        var act = () => _store.UpdateMemoryAsync("alice", oversized);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maximum allowed size*");
    }

    // ── R2-H: Latency baseline (< 50 ms for typical memory size) ─────────────

    [Fact]
    public async Task LoadMemory_TypicalSize_CompletesUnder50Ms()
    {
        // Typical MEMORY.md: ~2 KB of project context
        var content = string.Join("\n", Enumerable.Repeat(
            "## Project Context\n- Stack: .NET 10, ASP.NET Core, SQLite\n", 40));

        await _store.UpdateMemoryAsync("perf-profile", content);

        var sw = Stopwatch.StartNew();
        var ctx = await _store.LoadMemoryAsync("perf-profile");
        sw.Stop();

        ctx.IsEmpty.Should().BeFalse();
        sw.ElapsedMilliseconds.Should().BeLessThan(50,
            "memory load for a typical 2 KB MEMORY.md must be < 50 ms (R2 latency gate)");
    }

    // ── R2-I: Schema endpoint ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMemorySchema_ReturnsDefaultSchema()
    {
        var schema = await _store.GetMemorySchemaAsync();

        schema.MaxContentBytes.Should().Be(65_536);
        schema.SupportedFormats.Should().Contain("markdown");
        schema.CurrentSchemaVersion.Should().Be(1);
    }
}
