# HermesNET Test Framework Specification

**Status:** ✅ LOCKED for M1–M6  
**Date:** 2026-05-22  
**Owner:** Lambert (Tester)

## Executive Summary

HermesNET standardizes on **xUnit** as the unit and integration test framework for all milestones. This document locks test framework conventions, coverage tooling, and CI integration to enable consistent, measurable test execution across the development lifecycle.

---

## 1. Test Framework: xUnit

### Why xUnit?

- **Microsoft Standard** — Official recommendation for .NET ecosystem testing
- **Clean Syntax** — No magic base classes; composition over inheritance
- **Native Async Support** — First-class `async Task` test methods
- **Maintainability** — Minimal boilerplate, maximum readability across six milestones

### xUnit Features Used in HermesNET

| Feature | Usage | Example |
|---------|-------|---------|
| **Test Methods** | Public, parameterless, no base classes | `public async Task ChatClientFactory_CreateClient_ReturnsValidClient()` |
| **Fixtures** | Shared setup/teardown for tests | `IAsyncLifetime` for test data initialization |
| **Parameterized Tests** | `[Theory]` + `[InlineData]` for edge cases | YAML parser malformed input validation |
| **Async Tests** | `async Task` for integration tests | Database session store operations |
| **Test Collections** | Parallel test execution by default | No race conditions via isolated fixtures |

### Prohibited Patterns

- ❌ No base test classes (no `TestBase`, `IntegrationTestBase`, etc.)
- ❌ No setup/teardown methods (`[SetUp]`, `[TearDown]`)
- ❌ No test inheritance
- ❌ No MSTest or NUnit in HermesNET codebase

---

## 2. Code Coverage Tooling: Coverlet

### Coverage Measurement

**Tool:** Coverlet (via `Coverlet.Collector` NuGet package)

**Command:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

**Output Format:** OpenCover XML (CI-friendly, machine-parseable)

**Report Location:** `TestResults/*/coverage.opencover.xml`

### Coverage Targets

| Target | Threshold | Status | Enforcement |
|--------|-----------|--------|-------------|
| **Branch Coverage** (Hermes.Core) | ≥ 80% | **Hard Gate** | Build fails if < 80% |
| **Line Coverage** (Hermes.Core) | ≥ 75% | Informational | Reported in CI, not blocking |
| **Method Coverage** (Hermes.Core) | ≥ 85% | Informational | Reported in CI |

### M1 Coverage Scope

**In Scope (must reach 80% branch):**
- `Hermes.Core/Session/SessionStore.cs` — session persistence
- `Hermes.Core/Providers/ChatClientFactory.cs` — provider routing
- `Hermes.Core/Chat/HermesChatService.cs` — chat orchestration
- `Hermes.Core/Skills/SkillParser.cs` — YAML validation
- `Hermes.Core/Telemetry/HermesTelemetry.cs` — OTel instrumentation

**Out of Scope (M2+):**
- Hermes.Host (DI container, not unit-testable)
- Hermes.Cli (System.CommandLine integration, covered by smoke tests)

### Measuring Coverage Locally

```bash
# Run tests with coverage collection
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# View coverage report
# Open TestResults/coverage.opencover.xml in IDE or Codecov
```

---

## 3. GitHub Actions CI Integration

### CI Workflow File

**Location:** `.github/workflows/ci.yml`

**Triggers:**
- Every PR to `main`
- Every commit to `main`
- Manual dispatch

**Steps:**
1. Checkout code
2. Setup .NET 10 SDK
3. Restore NuGet packages
4. Build solution (`dotnet build`)
5. Run xUnit tests with Coverlet (`dotnet test --collect:"XPlat Code Coverage"`)
6. Publish coverage artifact
7. Check coverage threshold (fail if < 80% on Hermes.Core)
8. Report to GitHub Actions status

### CI Hard Gates

| Gate | Condition | Action |
|------|-----------|--------|
| **Build Succeeds** | `dotnet build` exit code = 0 | Fail if build fails |
| **Warnings = 0** | `TreatWarningsAsErrors=true` | Fail if warnings detected |
| **Coverage ≥ 80%** | Hermes.Core branch coverage | Fail if < 80% |
| **Tests Pass** | `dotnet test` exit code = 0 | Fail if any test fails |

### Coverage Report Publication

Coverage reports are published as CI artifacts for inspection:
- Artifact name: `coverage-reports`
- Contents: `TestResults/*/coverage.opencover.xml`
- Retention: 30 days (standard GitHub Actions default)

### Coverage Trend Dashboard

Coverage results are reportable to Codecov (optional, post-M1):
```yaml
- name: Upload coverage to Codecov
  uses: codecov/codecov-action@v3
  with:
    files: ./TestResults/*/coverage.opencover.xml
    flags: hermes-core
```

---

## 4. Test Assertions: FluentAssertions

### Standard Assertion Library

**Package:** `FluentAssertions`

**Why:**
- Readable, chainable assertions
- Better error messages on failure
- Reduces cognitive load (reads like English)

### Example Assertions

```csharp
using FluentAssertions;

// Simple assertions
result.Should().NotBeNull();
result.Should().Be(expected);
sessionId.Should().NotBeEmpty();

// Collection assertions
sessions.Should().HaveCount(1000);
sessions.Should().AllSatisfy(s => s.CreatedAt.Should().BeBefore(DateTime.UtcNow));

// Exception assertions
Func<Task> act = async () => await sessionStore.GetSession(null);
await act.Should().ThrowAsync<ArgumentNullException>();

// String assertions
response.Should().Contain("hello");
error.Should().StartWith("ERR_");
```

---

## 5. Test Project Structure

### Directory Layout

```
tests/
├── Hermes.Core.Tests/
│   ├── Hermes.Core.Tests.csproj
│   ├── Usings.cs                          # Shared imports
│   ├── Integration/
│   │   ├── R1IntegrationDrift.cs          # E2E chat → provider → response
│   │   └── ChatClientFactoryTests.cs      # Provider factory integration
│   ├── Session/
│   │   ├── SessionStoreTests.cs           # SQLite persistence
│   │   └── SessionFixtures.cs             # Test data builders
│   ├── Skills/
│   │   ├── SkillParserTests.cs            # YAML validation
│   │   └── fixtures/
│   │       ├── malformed-empty.yml
│   │       ├── malformed-missing-name.yml
│   │       ├── malformed-missing-type.yml
│   │       ├── malformed-null-fields.yml
│   │       ├── malformed-invalid-yaml.yml
│   │       └── valid-skill.yml
│   ├── Telemetry/
│   │   └── HermesTelemetryTests.cs        # OTel span emission
│   └── bin/, obj/                         # Build artifacts
```

---

## 6. Test Naming Convention

All xUnit test methods follow this pattern:

```
[MethodName]_[Scenario]_[ExpectedResult]
```

### Examples

| Test Name | Method | Scenario | Expected |
|-----------|--------|----------|----------|
| `ChatClientFactory_CreateOllamaClient_ReturnsValidClient` | `ChatClientFactory.Create(...)` | Ollama provider configured | Returns `IChatClient` |
| `SessionStore_InsertSession_PersistsToSqlite` | `SessionStore.InsertSession(...)` | Valid session object | Session ID in database |
| `SessionStore_GetSession_ThrowsArgumentNullException` | `SessionStore.GetSession(null)` | Session ID is null | Throws `ArgumentNullException` |
| `SkillParser_ValidSkillYaml_ReturnsSkillDefinition` | `SkillParser.Parse(...)` | Valid YAML fixture | `SkillDefinition` object |
| `SkillParser_MissingNameField_ThrowsSkillParseException` | `SkillParser.Parse(...)` | YAML missing `name:` | Throws `SkillParseException` |

---

## 7. Test Execution

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~SessionStoreTests"
```

### Run Tests with Verbose Output

```bash
dotnet test --verbosity detailed
```

### Run Tests in Parallel (Default)

```bash
dotnet test -- RunConfiguration.MaxCpuCount=4
```

### Run Tests Sequentially

```bash
dotnet test -- RunConfiguration.MaxCpuCount=1
```

---

## 8. Continuous Integration Gates Summary

| Gate | Tool | Measurement | Target | Hard? |
|------|------|-------------|--------|-------|
| **Build** | `dotnet build` | Compilation success + 0 warnings | Pass + 0 warnings | ✅ Yes |
| **Unit Tests** | xUnit | Test discovery + execution | All tests pass | ✅ Yes |
| **Coverage** | Coverlet | Branch coverage (Hermes.Core) | ≥ 80% | ✅ Yes |
| **CLI Smoke** | Manual/CI script | `hermes chat --message "test"` | Response received | ✅ Yes |

---

## 9. Adoption Timeline

- **M1 (May 22–Jun 5):** xUnit + Coverlet locked, CI workflow enabled, 80% coverage gate enforced
- **M2–M6 (Jun 5–Aug 30):** Consistent xUnit usage across all task test cases
- **Post-M1:** Coverlet reports published to Codecov dashboard (optional)

---

## References

- [xUnit.net Documentation](https://xunit.net/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [HermesNET CI Workflow](./.github/workflows/ci.yml)
