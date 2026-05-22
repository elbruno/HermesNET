# Test Conventions for HermesNET

**Status:** ✅ LOCKED for M1–M6  
**Date:** 2026-05-22  
**Owner:** Lambert (Tester)

---

## Executive Summary

All HermesNET tests follow consistent naming, structure, and assertion patterns to ensure readability, maintainability, and consistency across six milestones. This document is the style guide for writing xUnit tests in HermesNET.

---

## 1. Test Method Naming Convention

### Pattern

```
[MethodName]_[Scenario]_[ExpectedResult]
```

### Components

- **`[MethodName]`** — The method/class being tested (PascalCase)
  - Example: `ChatClientFactory`, `SessionStore`, `SkillParser`
- **`[Scenario]`** — The test setup or conditions (PascalCase)
  - Example: `CreateClient`, `InsertSession`, `ValidSkillYaml`, `MissingNameField`
- **`[ExpectedResult]`** — The assertion or expected outcome (PascalCase)
  - Example: `ReturnsValidClient`, `PersistsToSqlite`, `ThrowsArgumentNullException`

### Examples

| Test Name | Method | Scenario | Expected |
|-----------|--------|----------|----------|
| `ChatClientFactory_CreateOllamaClient_ReturnsValidClient` | ChatClientFactory.Create(...) | Ollama provider config | Returns IChatClient |
| `SessionStore_InsertSession_PersistsToSqlite` | SessionStore.InsertSession(...) | Valid session object | Session saved to DB |
| `SessionStore_GetSession_ThrowsArgumentNullException` | SessionStore.GetSession(null) | Null session ID | Throws ArgumentNullException |
| `HermesChatService_SendMessage_ReturnsResponseString` | HermesChatService.SendMessage(...) | Valid message | Response string returned |
| `SkillParser_ValidSkillYaml_ReturnsSkillDefinition` | SkillParser.Parse(...) | Valid YAML | SkillDefinition object |
| `SkillParser_MissingNameField_ThrowsSkillParseException` | SkillParser.Parse(...) | YAML missing `name` | Throws SkillParseException |

---

## 2. Test Class Organization

### Directory Structure

```
tests/Hermes.Core.Tests/
├── Integration/
│   ├── R1IntegrationDrift.cs              # E2E chat → provider → response
│   └── ChatClientFactoryTests.cs          # Provider factory integration
├── Session/
│   ├── SessionStoreTests.cs               # SQLite persistence
│   └── SessionFixtures.cs                 # Test data builders
├── Skills/
│   ├── SkillParserTests.cs                # YAML parsing
│   └── fixtures/                          # YAML test files
│       ├── malformed-empty.yml
│       ├── malformed-missing-name.yml
│       ├── valid-skill.yml
│       └── ... (other fixtures)
├── Telemetry/
│   └── HermesTelemetryTests.cs            # OTel instrumentation
└── Usings.cs                              # Shared imports
```

### Test Class Template

```csharp
namespace Hermes.Core.Tests.Session;

public class SessionStoreTests
{
    private readonly ISessionStore _sessionStore;
    private readonly ITestOutputHelper _output;

    public SessionStoreTests(ITestOutputHelper output)
    {
        _output = output;
        // Initialize test fixtures or dependencies
        _sessionStore = new SessionStore();
    }

    [Fact]
    public async Task SessionStore_InsertSession_PersistsToSqlite()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), ... };

        // Act
        var result = await _sessionStore.InsertSessionAsync(session);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SessionStore_GetSession_ThrowsArgumentNullException(string? sessionId)
    {
        // Act & Assert
        Func<Task> act = async () => await _sessionStore.GetSessionAsync(sessionId);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
```

---

## 3. Test Method Structure: AAA Pattern

Every test method follows **Arrange-Act-Assert (AAA)** pattern:

```csharp
[Fact]
public async Task ChatClientFactory_CreateClient_ReturnsValidClient()
{
    // Arrange
    var factory = new ChatClientFactory();
    var config = new ProviderConfig { Provider = "Ollama" };

    // Act
    var client = factory.CreateChatClient(config);

    // Assert
    client.Should().NotBeNull();
    client.Should().BeOfType<OllamaChatClient>();
}
```

### Section Guidelines

- **Arrange:** Set up test data, mocks, and fixtures
  - No business logic
  - Multiple lines OK for complex setups (use fixture classes)
- **Act:** Execute the method under test
  - Single line (or statement block for async)
  - No assertions in Act
- **Assert:** Validate expected behavior
  - Use FluentAssertions
  - Multiple assertions OK (if related)

---

## 4. Assertions: FluentAssertions Only

### Standard Assertions

```csharp
using FluentAssertions;

// Null/empty checks
result.Should().NotBeNull();
result.Should().BeNull();
response.Should().NotBeEmpty();

// Equality
actual.Should().Be(expected);
actual.Should().NotBe(unexpected);

// Exceptions
Func<Task> act = async () => await method();
await act.Should().ThrowAsync<ArgumentException>();

// Collections
sessions.Should().HaveCount(1000);
sessions.Should().BeEmpty();
sessions.Should().AllSatisfy(s => s.Id.Should().NotBeEmpty());
```

### Prohibited Assertions

❌ Do **NOT** use Assert.* (xUnit assertions):
```csharp
// ❌ WRONG
Assert.NotNull(result);
Assert.Equal(expected, actual);

// ✅ CORRECT
result.Should().NotBeNull();
actual.Should().Be(expected);
```

---

## 5. Test Fixtures for Setup/Teardown

### Using xUnit IAsyncLifetime

For tests requiring async initialization:

```csharp
public class SessionStoreTests : IAsyncLifetime
{
    private readonly ISessionStore _sessionStore;
    private SQLiteConnection? _connection;

    public async Task InitializeAsync()
    {
        // Initialize test database
        _connection = new SQLiteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessionStore = new SessionStore(_connection);
        await _sessionStore.InitializeSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup
        _connection?.Dispose();
    }

    [Fact]
    public async Task SessionStore_InsertSession_PersistsToSqlite()
    {
        // Test using initialized _sessionStore
    }
}
```

### Using Constructor Injection (Simpler)

For stateless test data:

```csharp
public class SkillParserTests
{
    private readonly SkillParser _parser;

    public SkillParserTests()
    {
        _parser = new SkillParser();
    }

    [Fact]
    public void SkillParser_ValidYaml_ReturnsSkillDefinition()
    {
        // Arrange
        var yaml = File.ReadAllText("fixtures/valid-skill.yml");

        // Act
        var result = _parser.Parse(yaml);

        // Assert
        result.Should().NotBeNull();
    }
}
```

### Using Fixture Classes (For Complex Shared State)

```csharp
public class SessionStoreFixture : IAsyncLifetime
{
    public ISessionStore SessionStore { get; private set; }
    private SQLiteConnection? _connection;

    public async Task InitializeAsync()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        SessionStore = new SessionStore(_connection);
        await SessionStore.InitializeSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        _connection?.Dispose();
    }
}

[Collection("SessionStore collection")]
public class SessionStoreTests : IClassFixture<SessionStoreFixture>
{
    private readonly SessionStoreFixture _fixture;

    public SessionStoreTests(SessionStoreFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SessionStore_InsertSession_PersistsToSqlite()
    {
        // Use _fixture.SessionStore
    }
}
```

---

## 6. Parameterized Tests with [Theory]

### Using [InlineData]

```csharp
[Theory]
[InlineData("", "empty string")]
[InlineData(null, "null")]
[InlineData("   ", "whitespace only")]
public void SkillParser_InvalidInput_ThrowsSkillParseException(string? input, string scenario)
{
    // Act & Assert
    Func<string> act = () => _parser.Parse(input);
    act.Should().Throw<SkillParseException>().WithMessage("*invalid*");
}
```

### Using [MemberData]

```csharp
public static IEnumerable<object[]> InvalidSkillData =>
    new List<object[]>
    {
        new object[] { "fixtures/malformed-empty.yml", typeof(SkillParseException) },
        new object[] { "fixtures/malformed-missing-name.yml", typeof(SkillParseException) },
    };

[Theory]
[MemberData(nameof(InvalidSkillData))]
public void SkillParser_InvalidYamlFixture_ThrowsException(string filePath, Type exceptionType)
{
    // Act
    var yaml = File.ReadAllText(filePath);
    Func<SkillDefinition> act = () => _parser.Parse(yaml);

    // Assert
    act.Should().Throw(exceptionType);
}
```

---

## 7. Async Tests

### Async Test Pattern

All async tests use `async Task` (not `async void`):

```csharp
[Fact]
public async Task SessionStore_InsertSession_PersistsToSqlite()
{
    // Arrange
    var session = new ChatSession { Id = Guid.NewGuid() };

    // Act
    var result = await _sessionStore.InsertSessionAsync(session);

    // Assert
    result.Should().NotBeEmpty();
}
```

### Exception Testing with Async

```csharp
[Fact]
public async Task SessionStore_GetSession_ThrowsArgumentNullException()
{
    // Act & Assert
    Func<Task> act = async () => await _sessionStore.GetSessionAsync(null);
    await act.Should().ThrowAsync<ArgumentNullException>();
}
```

---

## 8. Test Data Builders

For complex test objects, use builder pattern:

```csharp
public class ChatSessionBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _profile = "default";
    private List<ChatMessage> _messages = new();

    public ChatSessionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ChatSessionBuilder WithProfile(string profile)
    {
        _profile = profile;
        return this;
    }

    public ChatSession Build() =>
        new()
        {
            Id = _id,
            Profile = _profile,
            Messages = _messages,
        };
}

// Usage
[Fact]
public async Task SessionStore_InsertSession_PersistsCorrectly()
{
    // Arrange
    var session = new ChatSessionBuilder()
        .WithId(Guid.NewGuid())
        .WithProfile("test-profile")
        .Build();

    // Act
    await _sessionStore.InsertSessionAsync(session);

    // Assert
    var retrieved = await _sessionStore.GetSessionAsync(session.Id.ToString());
    retrieved.Should().NotBeNull();
}
```

---

## 9. Test Output & Logging

### Using ITestOutputHelper

```csharp
public class SessionStoreTests
{
    private readonly ITestOutputHelper _output;

    public SessionStoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SessionStore_InsertSession_PersistsToSqlite()
    {
        _output.WriteLine("Starting session insert test");
        var session = new ChatSession { Id = Guid.NewGuid() };
        
        var result = await _sessionStore.InsertSessionAsync(session);
        
        _output.WriteLine($"Inserted session: {result}");
        result.Should().NotBeEmpty();
    }
}
```

### Do NOT Use Console.WriteLine

❌ **WRONG:**
```csharp
Console.WriteLine("Test output");  // Won't appear in test runner
```

✅ **CORRECT:**
```csharp
_output.WriteLine("Test output");  // Visible in test runner
```

---

## 10. Test File Naming

### File Name Convention

```
[ClassName]Tests.cs
```

### Examples

| Class | Test File |
|-------|-----------|
| `SessionStore` | `SessionStoreTests.cs` |
| `ChatClientFactory` | `ChatClientFactoryTests.cs` |
| `SkillParser` | `SkillParserTests.cs` |
| `HermesChatService` | `HermesChatServiceTests.cs` |

---

## 11. Shared Imports: Usings.cs

**Location:** `tests/Hermes.Core.Tests/Usings.cs`

```csharp
global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using FluentAssertions;
global using Hermes.Core;
global using Hermes.Core.Session;
global using Hermes.Core.Providers;
global using Hermes.Core.Skills;
global using Xunit;
```

### Benefits

- No repeated `using` declarations in every test file
- Consistent imports across all tests
- Easier to add shared dependencies

---

## 12. Prohibited Patterns

### ❌ Base Test Classes

```csharp
// WRONG
public abstract class TestBase
{
    protected ISessionStore SessionStore { get; set; }
}

public class SessionStoreTests : TestBase
{
    // ...
}
```

### ✅ Use Composition Instead

```csharp
// CORRECT
public class SessionStoreTests : IAsyncLifetime
{
    private readonly ISessionStore _sessionStore;
    // ...
}
```

### ❌ SetUp/TearDown Methods

```csharp
// WRONG (MSTest/NUnit pattern)
[SetUp]
public void Setup() { }

[TearDown]
public void Teardown() { }
```

### ✅ Use Constructor or IAsyncLifetime

```csharp
// CORRECT (xUnit pattern)
public SessionStoreTests()
{
    _sessionStore = new SessionStore();
}

public async Task InitializeAsync() { }
public async Task DisposeAsync() { }
```

### ❌ Multiple Assertions (Unrelated)

```csharp
// WRONG
[Fact]
public async Task SessionStore_InsertSession_DoesEverything()
{
    var session = new ChatSession();
    await _sessionStore.InsertSessionAsync(session);
    
    // Testing multiple unrelated things
    var response = await GetChatResponse();
    response.Should().NotBeNull();  // Unrelated assertion
    
    var user = await GetUser();
    user.Should().NotBeNull();      // Unrelated assertion
}
```

### ✅ One Concept per Test

```csharp
// CORRECT
[Fact]
public async Task SessionStore_InsertSession_PersistsToSqlite()
{
    var session = new ChatSession();
    await _sessionStore.InsertSessionAsync(session);
    
    // Only assertions related to session persistence
    var retrieved = await _sessionStore.GetSessionAsync(session.Id.ToString());
    retrieved.Should().NotBeNull();
}

[Fact]
public async Task ChatService_SendMessage_ReturnsResponse()
{
    // Separate test for chat response
    var response = await _chatService.SendMessageAsync("hello");
    response.Should().NotBeNull();
}
```

---

## 13. Test Naming Checklist

Before committing a test, verify:

- [ ] Test method name follows `[Method]_[Scenario]_[Result]` pattern
- [ ] Test class name ends with `Tests`
- [ ] Test uses `[Fact]` or `[Theory]`
- [ ] Test follows AAA pattern (Arrange, Act, Assert)
- [ ] All assertions use FluentAssertions
- [ ] No `Assert.*()` calls (xUnit assertions)
- [ ] Async tests use `async Task` (not `async void`)
- [ ] No setup/teardown methods; use constructor or `IAsyncLifetime`
- [ ] No base test classes; use composition
- [ ] One concept per test (or related assertions only)

---

## 14. Example: Complete Test File

```csharp
namespace Hermes.Core.Tests.Session;

public class SessionStoreTests : IAsyncLifetime
{
    private readonly ISessionStore _sessionStore;
    private SQLiteConnection? _connection;
    private readonly ITestOutputHelper _output;

    public SessionStoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessionStore = new SessionStore(_connection);
        await _sessionStore.InitializeSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        _connection?.Dispose();
    }

    [Fact]
    public async Task SessionStore_InsertSession_PersistsToSqlite()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), Profile = "test" };

        // Act
        var result = await _sessionStore.InsertSessionAsync(session);

        // Assert
        result.Should().NotBeEmpty();
        _output.WriteLine($"Inserted session: {result}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SessionStore_GetSession_ThrowsArgumentException(string? sessionId)
    {
        // Act & Assert
        Func<Task> act = async () => await _sessionStore.GetSessionAsync(sessionId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SessionStore_GetSession_ReturnsPersistedSession()
    {
        // Arrange
        var session = new ChatSession { Id = Guid.NewGuid(), Profile = "test" };
        await _sessionStore.InsertSessionAsync(session);

        // Act
        var retrieved = await _sessionStore.GetSessionAsync(session.Id.ToString());

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Id.Should().Be(session.Id);
        retrieved.Profile.Should().Be("test");
    }
}
```

---

## References

- [xUnit.net Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [TEST-FRAMEWORK.md](./TEST-FRAMEWORK.md) — Framework choice & tooling
- [M1-QUALITY-GATES.md](./M1-QUALITY-GATES.md) — Coverage requirements
