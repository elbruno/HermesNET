namespace Hermes.Adapters.Tests;

/// <summary>
/// T34 — MAF Adapter Test Suite: HermesChatService → MAF IAgentOrchestrator (10 test cases).
///
/// Test reference:
///   TC-21  OrchestrateAsync returns response with matching Context echoed back
///   TC-22  OrchestrateAsync delegates message to IHermesChatService.ChatAsync
///   TC-23  StreamTokensAsync emits one TokenEvent per token from StreamChatAsync
///   TC-24  StreamTokensAsync marks only the last token as IsFinal=true
///   TC-25  StreamTokensAsync carries unchanged SessionContext on every TokenEvent
///   TC-26  ValidateSessionContext returns false for empty ProfileId
///   TC-27  ValidateSessionContext returns false for empty SessionId
///   TC-28  ValidateSessionContext returns true for valid ProfileId and SessionId
///   TC-29  OrchestrateAsync throws InvalidOperationException for invalid context
///   TC-30  StreamTokensAsync throws InvalidOperationException for invalid context
/// </summary>
public sealed class AgentOrchestratorAdapterTests
{
    private readonly Mock<IHermesChatService> _chatMock = new();

    // ── TC-21 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-21: OrchestrateAsync returns an AgentResponse whose Context equals the input SessionContext.
    /// The adapter must echo the context back without mutation.
    /// Input:  Valid context { ProfileId="p1", SessionId="s1" }, message "Hello"
    /// Expected: response.Context.ProfileId == "p1", response.Context.SessionId == "s1"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task OrchestrateAsync_ValidContext_EchosContextInResponse()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "p1", SessionId = "s1" };
        var request = new AgentRequest { Message = "Hello" };
        _chatMock.Setup(c => c.ChatAsync("Hello", It.IsAny<CancellationToken>()))
                 .ReturnsAsync("Hi there");

        // Act
        var response = await adapter.OrchestrateAsync(request, context);

        // Assert
        response.Context.ProfileId.Should().Be("p1");
        response.Context.SessionId.Should().Be("s1");
        response.Context.Should().BeSameAs(context);
    }

    // ── TC-22 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-22: OrchestrateAsync delegates the message text to IHermesChatService.ChatAsync.
    /// Input:  message "Hello world"
    /// Expected: ChatAsync called exactly once with "Hello world"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task OrchestrateAsync_ValidRequest_DelegatesToChatService()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "p1", SessionId = "s1" };
        var request = new AgentRequest { Message = "Hello world" };
        _chatMock.Setup(c => c.ChatAsync("Hello world", It.IsAny<CancellationToken>()))
                 .ReturnsAsync("Response");

        // Act
        await adapter.OrchestrateAsync(request, context);

        // Assert
        _chatMock.Verify(c => c.ChatAsync("Hello world", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── TC-23 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-23: StreamTokensAsync emits one TokenEvent per token returned by StreamChatAsync.
    /// Input:  StreamChatAsync yields ["Hello", " world", "!"]
    /// Expected: 3 TokenEvent instances emitted
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task StreamTokensAsync_ThreeTokens_EmitsThreeEvents()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "p1", SessionId = "s1" };
        var request = new AgentRequest { Message = "Hi" };

        _chatMock.Setup(c => c.StreamChatAsync("Hi", "p1", "s1", It.IsAny<CancellationToken>()))
                 .Returns(AsyncTokens("Hello", " world", "!"));

        // Act
        var events = new List<TokenEvent>();
        await foreach (var ev in adapter.StreamTokensAsync(request, context))
            events.Add(ev);

        // Assert
        events.Should().HaveCount(3);
        events.Select(e => e.Token).Should().Equal("Hello", " world", "!");
    }

    // ── TC-24 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-24: StreamTokensAsync marks only the last TokenEvent as IsFinal=true.
    /// Input:  StreamChatAsync yields ["A", "B", "C"]
    /// Expected: events[0].IsFinal=false, events[1].IsFinal=false, events[2].IsFinal=true
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task StreamTokensAsync_MultipleTokens_OnlyLastIsFinal()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "p1", SessionId = "s1" };
        var request = new AgentRequest { Message = "Hi" };

        _chatMock.Setup(c => c.StreamChatAsync("Hi", "p1", "s1", It.IsAny<CancellationToken>()))
                 .Returns(AsyncTokens("A", "B", "C"));

        // Act
        var events = new List<TokenEvent>();
        await foreach (var ev in adapter.StreamTokensAsync(request, context))
            events.Add(ev);

        // Assert
        events[0].IsFinal.Should().BeFalse();
        events[1].IsFinal.Should().BeFalse();
        events[2].IsFinal.Should().BeTrue();
    }

    // ── TC-25 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-25: StreamTokensAsync carries the unchanged SessionContext on every emitted TokenEvent.
    /// This is the per-token R2 isolation guard: context must never be mutated mid-stream.
    /// Input:  Context { ProfileId="p1", SessionId="s1" }, 3 tokens
    /// Expected: every event.Context is the same reference as the input context
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task StreamTokensAsync_AllTokenEvents_CarryOriginalContext()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "p1", SessionId = "s1" };
        var request = new AgentRequest { Message = "Hi" };

        _chatMock.Setup(c => c.StreamChatAsync("Hi", "p1", "s1", It.IsAny<CancellationToken>()))
                 .Returns(AsyncTokens("X", "Y", "Z"));

        // Act
        var events = new List<TokenEvent>();
        await foreach (var ev in adapter.StreamTokensAsync(request, context))
            events.Add(ev);

        // Assert
        foreach (var ev in events)
            ev.Context.Should().BeSameAs(context);
    }

    // ── TC-26 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-26: ValidateSessionContext returns false when ProfileId is empty or whitespace.
    /// Input:  { ProfileId = "", SessionId = "s1" }  and  { ProfileId = "   ", SessionId = "s1" }
    /// Expected: false
    /// </summary>
    [Theory(Skip = "M4 skeleton — not yet implemented")]
    [InlineData("", "s1")]
    [InlineData("   ", "s1")]
    public void ValidateSessionContext_EmptyProfileId_ReturnsFalse(string profileId, string sessionId)
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = profileId, SessionId = sessionId };

        // Act & Assert
        adapter.ValidateSessionContext(context).Should().BeFalse();
    }

    // ── TC-27 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-27: ValidateSessionContext returns false when SessionId is empty or whitespace.
    /// Input:  { ProfileId = "p1", SessionId = "" }  and  { ProfileId = "p1", SessionId = "  " }
    /// Expected: false
    /// </summary>
    [Theory(Skip = "M4 skeleton — not yet implemented")]
    [InlineData("p1", "")]
    [InlineData("p1", "   ")]
    public void ValidateSessionContext_EmptySessionId_ReturnsFalse(string profileId, string sessionId)
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = profileId, SessionId = sessionId };

        // Act & Assert
        adapter.ValidateSessionContext(context).Should().BeFalse();
    }

    // ── TC-28 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-28: ValidateSessionContext returns true when both ProfileId and SessionId are non-empty.
    /// Input:  { ProfileId = "profile-abc", SessionId = "session-xyz" }
    /// Expected: true
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ValidateSessionContext_ValidContext_ReturnsTrue()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "profile-abc", SessionId = "session-xyz" };

        // Act & Assert
        adapter.ValidateSessionContext(context).Should().BeTrue();
    }

    // ── TC-29 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-29: OrchestrateAsync throws InvalidOperationException for invalid context.
    /// Input:  context with empty ProfileId
    /// Expected: InvalidOperationException is thrown; ChatAsync is never called
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task OrchestrateAsync_InvalidContext_ThrowsAndDoesNotCallChatService()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "", SessionId = "s1" };
        var request = new AgentRequest { Message = "Hello" };

        // Act
        var act = async () => await adapter.OrchestrateAsync(request, context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _chatMock.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── TC-30 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-30: StreamTokensAsync throws InvalidOperationException for invalid context before yielding.
    /// Input:  context with empty SessionId
    /// Expected: InvalidOperationException is thrown; StreamChatAsync is never called
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task StreamTokensAsync_InvalidContext_ThrowsAndDoesNotCallStreamService()
    {
        // Arrange
        var adapter = new AgentOrchestratorAdapterStub(_chatMock.Object);
        var context = new SessionContext { ProfileId = "p1", SessionId = "" };
        var request = new AgentRequest { Message = "Hello" };

        // Act
        var act = async () =>
        {
            await foreach (var _ in adapter.StreamTokensAsync(request, context))
            { }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _chatMock.Verify(
            c => c.StreamChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
