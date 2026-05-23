namespace Hermes.Adapters.Tests;

/// <summary>
/// T34 — R2 Profile Isolation Contract Tests (5 tests).
///
/// These tests enforce that the adapter layer never strips or mutates the ProfileId carrier.
/// The R2 constraint requires that every session operation stays within the FK boundary
/// established by the profile — the adapter must be a transparent conduit for that boundary.
///
/// Test reference:
///   R2-01  Adapter preserves ProfileId across a full orchestration round-trip
///   R2-02  Two simultaneous contexts with different ProfileIds remain isolated
///   R2-03  StreamTokensAsync does not bleed ProfileId from one stream to another
///   R2-04  ValidateSessionContext rejects null context
///   R2-05  SessionContext is immutable — adapter cannot mutate ProfileId or SessionId
/// </summary>
public sealed class R2ProfileIsolationTests
{
    private readonly Mock<IHermesChatService> _chatMock = new();

    // ── R2-01 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// R2-01: ProfileId survives a complete orchestration round-trip without modification.
    ///
    /// The MAF adapter layer must not strip, shorten, or alter the ProfileId value at
    /// any point between request entry and response exit.
    ///
    /// Input:  SessionContext { ProfileId = "profile-r2-isolation-test", SessionId = "sess-001" }
    /// Expected: response.Context.ProfileId == "profile-r2-isolation-test" (exact, byte-for-byte)
    /// FK constraint: ProfileId is a hard FK in session context — loss = data corruption
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task OrchestrateAsync_ProfileIdPreserved_AcrossRoundTrip()
    {
        // Arrange
        const string expectedProfileId = "profile-r2-isolation-test";
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = expectedProfileId, SessionId = "sess-001" };
        var request = new AgentRequest { Message = "Test" };

        _chatMock.Setup(c => c.ChatAsync("Test", It.IsAny<CancellationToken>()))
                 .ReturnsAsync("OK");

        // Act
        var response = await adapter.OrchestrateAsync(request, context);

        // Assert — hard FK constraint: ProfileId must be identical at input and output
        response.Context.ProfileId.Should().Be(expectedProfileId,
            because: "ProfileId is a hard FK boundary; the adapter must never mutate it");
    }

    // ── R2-02 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// R2-02: Two contexts with different ProfileIds remain isolated through the adapter.
    ///
    /// Simulates two concurrent users with different profiles. The adapter must never
    /// let profileA's identity leak into profileB's response (and vice versa).
    ///
    /// Input:  ctxA = { ProfileId = "profile-alice", SessionId = "sa1" }
    ///         ctxB = { ProfileId = "profile-bob",   SessionId = "sb1" }
    /// Expected: responseA.Context.ProfileId == "profile-alice"
    ///           responseB.Context.ProfileId == "profile-bob"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task OrchestrateAsync_TwoProfiles_ContextsDoNotCrossContaminate()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var ctxA = new SessionContext { ProfileId = "profile-alice", SessionId = "sa1" };
        var ctxB = new SessionContext { ProfileId = "profile-bob",   SessionId = "sb1" };
        var requestA = new AgentRequest { Message = "Alice says hi" };
        var requestB = new AgentRequest { Message = "Bob says hi" };

        _chatMock.Setup(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync("OK");

        // Act
        var responseA = await adapter.OrchestrateAsync(requestA, ctxA);
        var responseB = await adapter.OrchestrateAsync(requestB, ctxB);

        // Assert
        responseA.Context.ProfileId.Should().Be("profile-alice");
        responseB.Context.ProfileId.Should().Be("profile-bob");
        responseA.Context.ProfileId.Should().NotBe(responseB.Context.ProfileId,
            because: "Profile isolation means two users must never see each other's ProfileId");
    }

    // ── R2-03 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// R2-03: Streaming tokens from two different profiles do not bleed ProfileIds.
    ///
    /// The streaming path is the highest-risk path for context leakage because tokens
    /// are emitted asynchronously. Each token event must carry the correct source profile.
    ///
    /// Input:  Two sequential streaming calls with distinct ProfileIds
    /// Expected: Every token from stream A carries "profile-alpha"
    ///           Every token from stream B carries "profile-beta"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task StreamTokensAsync_TwoProfiles_TokensCarryCorrectProfileId()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var ctxA = new SessionContext { ProfileId = "profile-alpha", SessionId = "sa" };
        var ctxB = new SessionContext { ProfileId = "profile-beta",  SessionId = "sb" };

        _chatMock.Setup(c => c.StreamChatAsync("MsgA", "profile-alpha", "sa", It.IsAny<CancellationToken>()))
                 .Returns(AsyncTokens("T1", "T2"));
        _chatMock.Setup(c => c.StreamChatAsync("MsgB", "profile-beta", "sb", It.IsAny<CancellationToken>()))
                 .Returns(AsyncTokens("T3", "T4"));

        // Act
        var eventsA = new List<TokenEvent>();
        await foreach (var ev in adapter.StreamTokensAsync(new AgentRequest { Message = "MsgA" }, ctxA))
            eventsA.Add(ev);

        var eventsB = new List<TokenEvent>();
        await foreach (var ev in adapter.StreamTokensAsync(new AgentRequest { Message = "MsgB" }, ctxB))
            eventsB.Add(ev);

        // Assert — no cross-contamination
        eventsA.Should().AllSatisfy(ev =>
            ev.Context.ProfileId.Should().Be("profile-alpha",
                because: "stream A tokens must always carry profile-alpha"));

        eventsB.Should().AllSatisfy(ev =>
            ev.Context.ProfileId.Should().Be("profile-beta",
                because: "stream B tokens must always carry profile-beta"));
    }

    // ── R2-04 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// R2-04: ValidateSessionContext returns false for a null context.
    ///
    /// Null represents a completely absent profile boundary — the adapter must reject it
    /// before any downstream call is made, to prevent null-dereference FK violations.
    ///
    /// Input:  null
    /// Expected: false (no exception thrown)
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ValidateSessionContext_NullContext_ReturnsFalse()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);

        // Act
        var result = adapter.ValidateSessionContext(null!);

        // Assert
        result.Should().BeFalse(because: "null context has no ProfileId and violates the R2 FK constraint");
    }

    // ── R2-05 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// R2-05: SessionContext is effectively immutable — the adapter response context
    /// is the same reference as the input (not a copy with potentially different values).
    ///
    /// Using reference equality verifies that the adapter does not create a new
    /// SessionContext with a different (possibly mutated or default) ProfileId.
    ///
    /// Input:  SessionContext { ProfileId = "profile-immutable", SessionId = "s-imm" }
    /// Expected: response.Context is the same object reference as the input context
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task OrchestrateAsync_SessionContextIsPassedByReference_NotCopied()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "profile-immutable", SessionId = "s-imm" };
        var request = new AgentRequest { Message = "Check immutability" };

        _chatMock.Setup(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync("Done");

        // Act
        var response = await adapter.OrchestrateAsync(request, context);

        // Assert — same object reference: adapter must not construct a new SessionContext
        response.Context.Should().BeSameAs(context,
            because: "the adapter must preserve the exact SessionContext reference to maintain R2 FK integrity");
    }

    // ── Async helpers ─────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<string> AsyncTokens(params string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Yield();
            yield return token;
        }
    }
}
