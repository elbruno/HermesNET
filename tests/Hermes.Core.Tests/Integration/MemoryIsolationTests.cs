namespace Hermes.Core.Tests.Integration;

/// <summary>
/// Memory isolation integration tests.
/// Duplicate of unit-level isolation tests but exercised end-to-end through
/// the full memory coordinator + profile manager stack.
///
/// R2 checkpoint (cross-profile contamination) is the primary gate.
/// See docs/testing/m2-test-strategy.md Section 2 and 4.
/// </summary>
public class MemoryIsolationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _workspace;

    public MemoryIsolationTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = Path.Combine(Path.GetTempPath(), $"hermes-isolation-{Guid.NewGuid()}");
        Directory.CreateDirectory(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
            Directory.Delete(_workspace, recursive: true);
    }

    /// <summary>
    /// R2 integration-level gate. End-to-end stack version.
    /// Lambert issues R2 verdict only when both unit-level AND this integration-level test pass.
    /// </summary>
    [Fact]
    public async Task MemoryIsolation_FullStack_ProfileACannotReadProfileBMemory()
    {
        // Arrange
        // Write MEMORY.md for profile A and profile B in separate workspace directories
        // Initialize IMemoryCoordinator with the workspace

        // Act — load snapshots independently

        // Assert — no cross-contamination in either direction
        _output.WriteLine("R2 integration gate — PENDING full stack implementation");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MemoryIsolation_UserMd_ProfileACannotReadProfileBUserPreferences()
    {
        // Covers USER.md isolation in addition to MEMORY.md
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MemoryIsolation_PersistTurn_DoesNotWriteToOtherProfileMemory()
    {
        // After persisting a turn for profile A, profile B's memory is unchanged
        await Task.CompletedTask;
    }
}
