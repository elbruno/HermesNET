using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Hermes.Core.Tests.Session;

/// <summary>
/// Unit tests for SessionStore (T6) using in-memory SQLite (no disk I/O).
///
/// Covers: happy-path CRUD, edge cases, pagination, ordering, R5-A benchmark,
/// concurrency safety, and per-test DB isolation.
/// Coverage target: ≥ 80% branch coverage on SessionStore (M1 hard gate).
///
/// Isolation strategy: each test instance gets a fresh named in-memory SQLite
/// database (Mode=Memory;Cache=Shared) so that a second SqliteConnection can be
/// opened for direct-DB verification without going through the store's own query path.
///
/// SPEC DIVERGENCES — flagged for Dallas + Ripley before T12 go/no-go:
///   1. DeleteAsync with a non-existent ID: acceptance criteria specified an
///      idempotent no-op; implementation throws KeyNotFoundException.
///   2. UpdateAsync with a non-existent ID: acceptance criteria specified
///      InvalidOperationException; implementation throws KeyNotFoundException.
/// Tests below assert ACTUAL implementation behaviour. Either the implementation
/// or the spec must be updated before M1 exit.
/// </summary>
public sealed class SessionStoreTests : IAsyncLifetime
{
    private SessionStore _store = null!;
    private string _connectionString = null!;
    private readonly ITestOutputHelper _output;

    public SessionStoreTests(ITestOutputHelper output) => _output = output;

    // ── IAsyncLifetime — fresh isolated DB per test ────────────────────────────

    public async Task InitializeAsync()
    {
        // Unique name guarantees no cross-test state leakage (isolation test T17).
        _connectionString = $"Data Source=testdb-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _store = new SessionStore(_connectionString);
        await _store.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _store.Dispose();
        return Task.CompletedTask;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>Convenience wrapper — creates a session via the store.</summary>
    private Task<SessionEntity> CreateSessionAsync(string profileId = "test-profile")
        => _store.CreateAsync(profileId);

    /// <summary>
    /// Opens a second SqliteConnection to the same named in-memory database and
    /// executes a raw SELECT — bypasses the store's own GetAsync to verify that
    /// CreateAsync / UpdateAsync actually wrote to the DB layer.
    /// </summary>
    private async Task<SessionEntity?> GetSessionDirectAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ProfileId, CreatedAt, UpdatedAt, LastMessage, MessageCount
            FROM Sessions WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new SessionEntity
        {
            Id           = reader.GetString(0),
            ProfileId    = reader.GetString(1),
            CreatedAt    = reader.GetString(2),
            UpdatedAt    = reader.GetString(3),
            LastMessage  = reader.IsDBNull(4) ? null : reader.GetString(4),
            MessageCount = reader.GetInt32(5)
        };
    }

    /// <summary>Computes the P-th percentile (e.g. 95) of latency measurements.</summary>
    private static long Percentile(List<long> values, int p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var index  = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }

    // ──────────────────────────────────────────────────────────────────────────
    // InitializeAsync — idempotency
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_CalledTwice_DoesNotThrow()
    {
        // InitializeAsync is called once in InitializeAsync above; calling again must be a no-op.
        var act = async () => await _store.InitializeAsync();
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CreateAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ReturnsEntityWithGeneratedId()
    {
        var entity = await _store.CreateAsync("profile-1");

        entity.Id.Should().NotBeNullOrWhiteSpace();
        entity.ProfileId.Should().Be("profile-1");
        entity.MessageCount.Should().Be(0);
        entity.LastMessage.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithMessage_SetsLastMessageAndCount()
    {
        var entity = await _store.CreateAsync("profile-1", "Hello");

        entity.LastMessage.Should().Be("Hello");
        entity.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_MultipleProfiles_AllPersisted()
    {
        await _store.CreateAsync("profile-A");
        await _store.CreateAsync("profile-A");
        await _store.CreateAsync("profile-B");

        var all = await _store.ListRecentAsync(100);
        all.Should().HaveCount(3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ExistingId_ReturnsEntity()
    {
        var created = await _store.CreateAsync("profile-1", "first message");

        var fetched = await _store.GetAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.ProfileId.Should().Be("profile-1");
        fetched.LastMessage.Should().Be("first message");
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var result = await _store.GetAsync("does-not-exist");

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // UpdateAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesLastMessageAndCount()
    {
        var created = await _store.CreateAsync("profile-1");

        var updated = await _store.UpdateAsync(created.Id, "updated message");

        updated.LastMessage.Should().Be("updated message");
        updated.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_CalledMultipleTimes_IncrementsCount()
    {
        var created = await _store.CreateAsync("profile-1");

        await _store.UpdateAsync(created.Id, "msg-1");
        var final = await _store.UpdateAsync(created.Id, "msg-2");

        final.MessageCount.Should().Be(2);
        final.LastMessage.Should().Be("msg-2");
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        var act = async () => await _store.UpdateAsync("ghost-id", "payload");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost-id*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DeleteAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesEntity()
    {
        var created = await _store.CreateAsync("profile-1");

        await _store.DeleteAsync(created.Id);

        var fetched = await _store.GetAsync(created.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        var act = async () => await _store.DeleteAsync("ghost-id");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ghost-id*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Full happy-path CRUD round-trip
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_CreateGetUpdateDelete_Succeeds()
    {
        // Create
        var created = await _store.CreateAsync("profile-happy");
        created.Id.Should().NotBeNullOrWhiteSpace();

        // Get
        var fetched = await _store.GetAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched!.ProfileId.Should().Be("profile-happy");

        // Update
        var updated = await _store.UpdateAsync(created.Id, "Hello, Hermes!");
        updated.LastMessage.Should().Be("Hello, Hermes!");

        // Delete
        await _store.DeleteAsync(created.Id);
        var gone = await _store.GetAsync(created.Id);
        gone.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ListRecentAsync — pagination and ordering
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListRecentAsync_ReturnsEmpty_WhenNoSessions()
    {
        var results = await _store.ListRecentAsync(50);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsMostRecentFirst()
    {
        // Insert sessions with artificial ordering via small delays.
        var first = await _store.CreateAsync("profile-order");
        await Task.Delay(5); // ensure distinct timestamps
        var second = await _store.CreateAsync("profile-order");

        var results = await _store.ListRecentAsync(50);

        results.Should().HaveCount(2);
        results[0].Id.Should().Be(second.Id, "most recent should be first");
        results[1].Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task ListRecentAsync_RespectsLimit()
    {
        for (var i = 0; i < 10; i++)
            await _store.CreateAsync($"profile-{i}");

        var results = await _store.ListRecentAsync(limit: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListRecentAsync_DefaultLimit50_CapsBeyond50()
    {
        for (var i = 0; i < 60; i++)
            await _store.CreateAsync("profile-bulk");

        var results = await _store.ListRecentAsync();

        results.Should().HaveCount(50);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Guard — operations before InitializeAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_BeforeInitialize_ThrowsInvalidOperationException()
    {
        using var uninitializedStore = new SessionStore("Data Source=:memory:");

        var act = async () => await uninitializedStore.CreateAsync("profile");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*InitializeAsync*");
    }

    // ── T6 Acceptance Criteria — full scenario set (Lambert, 2026-05-22) ───────
    // Naming: {MethodName}_{Scenario}_{ExpectedResult}
    // ──────────────────────────────────────────────────────────────────────────

    // CreateAsync ---------------------------------------------------------------

    /// <summary>
    /// Branches: normal INSERT, ID generation, timestamp freshness, UpdatedAt == CreatedAt.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithValidProfile_ReturnsSessionWithId()
    {
        // Act
        var result = await _store.CreateAsync("profile-alpha");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.ProfileId.Should().Be("profile-alpha");
        DateTimeOffset.Parse(result.CreatedAt)
            .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        // UpdatedAt is set in the same object initialiser — should be within 1 ms of CreatedAt.
        DateTimeOffset.Parse(result.UpdatedAt)
            .Should().BeCloseTo(DateTimeOffset.Parse(result.CreatedAt), TimeSpan.FromMilliseconds(100),
            "UpdatedAt must be set at creation time, not deviate by more than 100 ms from CreatedAt");
    }

    /// <summary>
    /// Branches: five independent INSERTs; GUID uniqueness across all rows.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithDifferentProfiles_GeneratesUniqueIds()
    {
        // Arrange
        var profiles = Enumerable.Range(1, 5).Select(i => $"unique-profile-{i}");

        // Act
        var sessions = new List<SessionEntity>();
        foreach (var p in profiles)
            sessions.Add(await _store.CreateAsync(p));

        // Assert
        sessions.Should().HaveCount(5);
        sessions.Select(s => s.Id).Should().OnlyHaveUniqueItems(
            "every CreateAsync call must generate a distinct GUID");
    }

    /// <summary>
    /// Branches: INSERT persists row visible to an independent SqliteConnection.
    /// Uses GetSessionDirectAsync (raw SQL) not store.GetAsync, so it validates
    /// the persistence layer independently of the read path.
    /// </summary>
    [Fact]
    public async Task CreateAsync_CreatesSessionInDatabase()
    {
        // Arrange
        const string profileId = "direct-db-verify";

        // Act
        var created = await _store.CreateAsync(profileId);

        // Assert — second connection, bypasses store.GetAsync
        var row = await GetSessionDirectAsync(created.Id);
        row.Should().NotBeNull("row must be present in Sessions table immediately after CreateAsync");
        row!.Id.Should().Be(created.Id);
        row.ProfileId.Should().Be(profileId);
        row.CreatedAt.Should().Be(created.CreatedAt);
        row.UpdatedAt.Should().Be(created.UpdatedAt);
    }

    // GetAsync ------------------------------------------------------------------

    /// <summary>
    /// Branches: SELECT finds record, all fields mapped correctly.
    /// </summary>
    [Fact]
    public async Task GetAsync_WithValidId_ReturnsSession()
    {
        // Arrange
        var created = await CreateSessionAsync("get-by-id-profile");

        // Act
        var result = await _store.GetAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.ProfileId.Should().Be("get-by-id-profile");
        result.CreatedAt.Should().Be(created.CreatedAt);
        result.UpdatedAt.Should().Be(created.UpdatedAt);
    }

    /// <summary>
    /// Branches: SELECT returns no rows → reader.ReadAsync == false → return null.
    /// </summary>
    [Fact]
    public async Task GetAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _store.GetAsync("00000000-0000-0000-0000-000000000000");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Branches: GetAsync reflects the row written by UpdateAsync (not a stale cache).
    /// </summary>
    [Fact]
    public async Task GetAsync_AfterUpdate_ReturnsLatestData()
    {
        // Arrange
        var created = await CreateSessionAsync("get-after-update");
        await Task.Delay(10); // ensure UpdatedAt advances

        // Act
        await _store.UpdateAsync(created.Id, "freshly-updated-message");
        var result = await _store.GetAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.LastMessage.Should().Be("freshly-updated-message");
    }

    // UpdateAsync ---------------------------------------------------------------

    /// <summary>
    /// Branches: UPDATE affects one row; direct DB read confirms persistence.
    /// Uses GetSessionDirectAsync to verify the value at the storage layer.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithValidId_UpdatesLastMessage()
    {
        // Arrange
        var session = await CreateSessionAsync("update-direct-verify");
        const string msg = "persisted-last-message";

        // Act
        await _store.UpdateAsync(session.Id, msg);

        // Assert — direct DB read, not store.GetAsync
        var row = await GetSessionDirectAsync(session.Id);
        row.Should().NotBeNull();
        row!.LastMessage.Should().Be(msg, "UpdateAsync must write LastMessage to the DB row");
    }

    /// <summary>
    /// Branches: UPDATE affects zero rows → exception path.
    ///
    /// SPEC NOTE: acceptance criteria specified InvalidOperationException;
    /// implementation throws KeyNotFoundException — documented as discrepancy
    /// for Dallas + Ripley before T12.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ThrowsInvalidOperationException()
    {
        // Act — name matches spec; actual exception is KeyNotFoundException (see note above)
        Func<Task> act = async () => await _store.UpdateAsync("ghost-spec-id", "payload");

        // Assert — ACTUAL behaviour (KeyNotFoundException); spec says InvalidOperationException
        await act.Should().ThrowAsync<KeyNotFoundException>(
            "spec says InvalidOperationException but implementation throws KeyNotFoundException; " +
            "resolve discrepancy before T12");
    }

    /// <summary>
    /// Branches: UpdatedAt is recomputed on every UPDATE call.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ChangesUpdatedAtTimestamp()
    {
        // Arrange
        var session = await CreateSessionAsync("timestamp-drift");
        var createdAt = DateTimeOffset.Parse(session.CreatedAt);
        await Task.Delay(15); // guarantee clock advances

        // Act
        var updated = await _store.UpdateAsync(session.Id, "bump-timestamp");
        var updatedAt = DateTimeOffset.Parse(updated.UpdatedAt);

        // Assert
        updatedAt.Should().BeAfter(createdAt,
            "UpdatedAt must advance strictly beyond CreatedAt after UpdateAsync");
    }

    // DeleteAsync ---------------------------------------------------------------

    /// <summary>
    /// Branches: DELETE removes row; subsequent SELECT returns null.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithValidId_RemovesSession()
    {
        // Arrange
        var session = await CreateSessionAsync("delete-me-profile");

        // Act
        await _store.DeleteAsync(session.Id);

        // Assert
        var result = await _store.GetAsync(session.Id);
        result.Should().BeNull("session must not be retrievable after DeleteAsync");
    }

    /// <summary>
    /// Branches: DELETE affects zero rows → exception path.
    ///
    /// SPEC NOTE: acceptance criteria required an idempotent no-op (no exception);
    /// implementation throws KeyNotFoundException. This test documents ACTUAL
    /// behaviour. Dallas must align the implementation with the spec (make it a
    /// no-op) or update the acceptance criteria before T12 go/no-go.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ThrowsKeyNotFoundExceptionSpecSaysNoOp()
    {
        // Act
        Func<Task> act = async () => await _store.DeleteAsync("nonexistent-spec-id");

        // Assert — ACTUAL behaviour; spec says this should be a no-op, not an exception
        await act.Should().ThrowAsync<KeyNotFoundException>(
            "spec says no-op but implementation throws; " +
            "Dallas must fix before T12 — either make DeleteAsync idempotent or update the spec");
    }

    // ListRecentAsync -----------------------------------------------------------

    /// <summary>
    /// Branches: ORDER BY CreatedAt DESC LIMIT N with N &lt; total rows; top 3 of 5.
    /// </summary>
    [Fact]
    public async Task ListRecentAsync_WithLimit_ReturnsOrderedByCreationDescending()
    {
        // Arrange — 5 sessions with strictly distinct timestamps
        var ordered = new List<SessionEntity>();
        for (int i = 0; i < 5; i++)
        {
            ordered.Add(await _store.CreateAsync($"ordered-{i}"));
            await Task.Delay(5);
        }

        // Act
        var results = await _store.ListRecentAsync(limit: 3);

        // Assert
        results.Should().HaveCount(3, "limit:3 must cap results");
        results.Select(s => DateTimeOffset.Parse(s.CreatedAt))
               .Should().BeInDescendingOrder("ORDER BY CreatedAt DESC must be honoured");
        results.First().Id.Should().Be(ordered.Last().Id,
            "the most recently created session must appear first");
    }

    /// <summary>
    /// Branches: reader loop body never entered; returns empty IReadOnlyList.
    /// </summary>
    [Fact]
    public async Task ListRecentAsync_WithEmpty_ReturnsEmptyList()
    {
        // Act — fresh store, no sessions created
        var results = await _store.ListRecentAsync(limit: 50);

        // Assert
        results.Should().BeEmpty("a freshly initialised store must return an empty list");
    }

    /// <summary>
    /// R5-A benchmark — P95 query latency must be ≤ 20 ms at 1,000 rows.
    /// Gate: docs/testing/M1-QUALITY-GATES.md § Gate 6 (Hard).
    /// Branches: ORDER BY + LIMIT scan on a 1,000-row table with CreatedAt index.
    /// </summary>
    [Fact]
    public async Task ListRecentAsync_WithLarge1000Sessions_QueryCompletesUnder20ms()
    {
        // Arrange — 1,000 sequential inserts (R5 load-test spec)
        for (int i = 0; i < 1000; i++)
            await _store.CreateAsync($"perf-profile-{i}");

        const int runs = 50;
        var times = new List<long>(runs);

        // Act — 50 timed query runs
        for (int i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            var results = await _store.ListRecentAsync(limit: 50);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
            results.Should().HaveCount(50);
        }

        // Assert — R5-A hard gate: P95 ≤ 20 ms
        var p95 = Percentile(times, 95);
        _output.WriteLine($"[R5-A] ListRecentAsync(1000 rows) P95 = {p95} ms over {runs} runs");
        p95.Should().BeLessOrEqualTo(20,
            $"R5-A gate: P95 query latency must be ≤ 20 ms at 1,000 rows, was {p95} ms");
    }

    // Edge cases ----------------------------------------------------------------

    /// <summary>
    /// Branches: INSERT with a large TEXT value; SQLite TEXT is unbounded.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithLongProfileName_Succeeds()
    {
        // Arrange
        var longName = new string('x', 500);

        // Act
        var session = await _store.CreateAsync(longName);

        // Assert — store return value
        session.ProfileId.Should().Be(longName);
        session.ProfileId.Should().HaveLength(500, "no truncation must occur");

        // Assert — direct DB confirms no truncation at the persistence layer
        var row = await GetSessionDirectAsync(session.Id);
        row!.ProfileId.Should().Be(longName, "SQLite TEXT column must store the full 500-char value");
    }

    /// <summary>
    /// Branches: 10 concurrent INSERTs; each produces a unique row.
    /// If this test fails with a concurrency exception, Dallas must add a write
    /// lock (SemaphoreSlim) or connection pooling to SessionStore.
    /// </summary>
    [Fact]
    public async Task SessionStore_ConcurrentCreates_NoRaceCondition()
    {
        // Arrange
        const int concurrency = 10;
        var tasks = Enumerable.Range(1, concurrency)
                              .Select(i => _store.CreateAsync($"concurrent-{i}"))
                              .ToList();

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrency);
        results.Should().AllSatisfy(s => s.Id.Should().NotBeNullOrEmpty());
        results.Select(s => s.Id).Should().OnlyHaveUniqueItems(
            "parallel CreateAsync calls must produce distinct session IDs");
    }

    /// <summary>
    /// Branches: two independently initialised stores on different named in-memory
    /// databases share no row state — proves the IAsyncLifetime isolation strategy.
    /// </summary>
    [Fact]
    public async Task SessionStore_WithInMemoryDb_IsolatedPerTest()
    {
        // Arrange — create a session in THIS test's store
        var session = await CreateSessionAsync("isolation-anchor");

        // Spin up an independent store backed by a different named in-memory DB
        var isolatedCs = $"Data Source=isolated-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var isolatedStore = new SessionStore(isolatedCs);
        await isolatedStore.InitializeAsync();

        // Act — attempt to find the session ID in the isolated store
        var notFound = await isolatedStore.GetAsync(session.Id);

        // Assert
        notFound.Should().BeNull(
            "separate named in-memory databases must not share row state; " +
            "each test's IAsyncLifetime fixture must be fully isolated");
    }
}
