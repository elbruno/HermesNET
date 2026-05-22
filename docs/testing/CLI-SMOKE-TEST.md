# HermesNET CLI Smoke Test Specification

**Status:** ✅ LOCKED  
**Date:** 2026-05-22  
**Owner:** Lambert (Tester)  
**Applies to:** M1 T7 (CLI integration), M1 exit gate

---

## Executive Summary

The CLI smoke test validates end-to-end integration: user invokes `hermes chat` command → CLI routes to `IHermesChatService` → provider response received → output to stdout. This test is the primary user-facing acceptance criterion for M1.

---

## 1. CLI Smoke Test Command

### Basic Invocation

```bash
hermes chat --profile default --message "What is 2+2?"
```

### Alternative Messages (for variety)

```bash
hermes chat --profile default --message "hello"
hermes chat --profile default --message "Tell me a joke"
hermes chat --profile default --message "Test"
```

### Expected Output Format

```
Response: <provider response text>
Session ID: <UUID>
Turn ID: <number>
Duration: <milliseconds>ms
```

### Example Output

```
Response: 2+2 equals 4. It's a basic arithmetic operation.
Session ID: 550e8400-e29b-41d4-a716-446655440000
Turn ID: 1
Duration: 245ms
```

---

## 2. Success Criteria

The smoke test **PASSES** if all of the following are true:

| Criterion | Validation | Result |
|-----------|-----------|--------|
| **Command exits cleanly** | Exit code = 0 | ✅ Pass |
| **Response is non-empty** | Length > 0 characters | ✅ Pass |
| **Response is a string** | Not null, not truncated | ✅ Pass |
| **Session ID is valid UUID** | Matches UUID format | ✅ Pass |
| **Turn ID is integer** | ≥ 1 | ✅ Pass |
| **Duration is recorded** | > 0 ms | ✅ Pass |
| **No error output** | stderr is empty | ✅ Pass |

### Example Pass Output

```
Response: 2 + 2 = 4
Session ID: 550e8400-e29b-41d4-a716-446655440000
Turn ID: 1
Duration: 245ms

Exit code: 0
stderr: (empty)
```

---

## 3. Failure Modes

The smoke test **FAILS** if any of the following occur:

| Failure Mode | Example | Resolution |
|--------------|---------|------------|
| **Command not found** | `command not found: hermes` | Build/publish CLI project |
| **Missing required option** | `Error: --message required` | Provide `--message` option |
| **Provider unreachable** | `Error: Ollama connection failed` | Start Ollama, or use mock provider |
| **Empty response** | `Response:` (no text after) | Provider returned empty; escalate |
| **Invalid Session ID** | `Session ID: invalid-format` | Session save failed; check SQLite |
| **No Duration recorded** | `Duration:` (no ms) | OTel timing not captured; check Telemetry |
| **Non-zero exit code** | Exit code: 1 | Exception thrown; check logs |

---

## 4. Manual Execution (Local Testing)

### Prerequisites

- .NET 10 SDK installed
- Solution built: `dotnet build`
- Ollama running locally (or mock provider configured in `appsettings.json`)
- SQLite database initialized

### Steps

```bash
# 1. Navigate to project root
cd D:\elbruno\HermesNET

# 2. Build the solution
dotnet build

# 3. Run the CLI smoke test manually
dotnet run --project src/Hermes.Cli -- chat --profile default --message "hello"

# 4. Verify output format
# Expected: Response, Session ID, Turn ID, Duration all present
```

### Sample Manual Test Session

```bash
$ dotnet run --project src/Hermes.Cli -- chat --profile default --message "What is 2+2?"

Response: 2 + 2 equals 4. This is basic arithmetic.
Session ID: 550e8400-e29b-41d4-a716-446655440000
Turn ID: 1
Duration: 312ms
```

---

## 5. Automated CI Execution

### CI Smoke Test Job

**Location:** `.github/workflows/ci.yml`

**Step:**
```yaml
- name: Run CLI Smoke Test
  run: |
    # Start a mock provider or use Ollama
    # (Exact mechanism depends on T4 provider setup)
    
    # Run the CLI command
    dotnet run --project src/Hermes.Cli -- chat --profile default --message "test"
    
    # Capture exit code
    if [ $? -ne 0 ]; then
      echo "❌ CLI smoke test failed"
      exit 1
    fi
    echo "✅ CLI smoke test passed"
```

### Provider Strategy for CI

**Option A: Mock Provider** (Recommended for CI speed)
- Use `ChatClientFactory` with mock `IChatClient`
- Mock returns predetermined response: `"Test response from mock provider"`
- No external dependencies (Ollama not required)
- Fast execution: < 1 second

**Option B: Ollama in Docker** (Alternative for integration confidence)
- Run Ollama as service container in GitHub Actions
- Pull small model (e.g., `tinyllama`)
- Slower (30–60 seconds per test), but validates real provider path

**M1 Recommendation:** Use mock provider in CI (Option A) for speed; Ollama tested manually/locally.

---

## 6. CLI Smoke Test Acceptance Criteria (T7)

**Task:** T7 - CLI Integration & Smoke Test  
**Owner:** Dallas  
**Timeline:** Week 1, Day 3 (May 24)

### T7 Deliverables

- [ ] CLI command `hermes chat --profile default --message "..."` works
- [ ] Response is routed through `IHermesChatService` → `IChatClient` → Ollama (or mock)
- [ ] Session is persisted to SQLite after chat
- [ ] Output includes: Response, Session ID, Turn ID, Duration
- [ ] Smoke test passes locally with `dotnet run`
- [ ] Smoke test passes in GitHub Actions CI

### T7 Acceptance Definition

**DONE when:**
1. `dotnet run --project src/Hermes.Cli -- chat --profile default --message "hello"` runs without errors
2. Output contains all four fields (Response, Session ID, Turn ID, Duration)
3. Session ID is a valid UUID
4. Session exists in SQLite database
5. Manual test passes on Linux/Windows/macOS
6. CI workflow includes smoke test step
7. CI smoke test passes on every PR

---

## 7. Smoke Test Regression Prevention

### Per-Milestone Re-validation

After each milestone (M2–M6), the smoke test is re-executed to ensure no regressions:

```bash
# Pre-release smoke test (M2 example)
dotnet build
dotnet run --project src/Hermes.Cli -- chat --profile default --message "M2 test"

# Success: Response received, session saved, no errors
```

### Automated Weekly Smoke Test (Post-M1)

```yaml
# .github/workflows/smoke-test.yml (new, post-M1)
name: Weekly Smoke Test
on:
  schedule:
    - cron: '0 9 * * 1'  # Every Monday at 9 AM UTC

jobs:
  smoke-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
      - run: dotnet build
      - run: dotnet run --project src/Hermes.Cli -- chat --profile default --message "Weekly smoke test"
```

---

## 8. CLI Output Specification (Detailed)

### Command Structure

```
hermes chat [options]

Options:
  --profile <profile>   Profile name (default: "default")
  --message <message>   Chat message to send (required)
  --help               Show help
```

### Response Output Format

```
Response: <string>
Session ID: <UUID>
Turn ID: <integer>
Duration: <milliseconds>ms
```

### Field Definitions

| Field | Type | Format | Example |
|-------|------|--------|---------|
| **Response** | string | Any text from provider | `"2 + 2 = 4"` |
| **Session ID** | UUID | `{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}` | `550e8400-e29b-41d4-a716-446655440000` |
| **Turn ID** | integer | ≥ 1 | `1`, `2`, `3` |
| **Duration** | milliseconds | `nnn` or `nnnn` | `245ms`, `1203ms` |

---

## 9. Error Message Specification

### Standard Error Messages

| Error Scenario | Error Message | Resolution |
|---|---|---|
| Missing `--message` option | `Error: --message is required` | Add `--message "text"` |
| Missing `--profile` option | (OK—uses default) | N/A |
| Provider not available | `Error: Unable to connect to provider (Ollama)` | Start Ollama or configure mock |
| Database write failed | `Error: Failed to persist session` | Check SQLite connection |
| Timeout (> 30 sec) | `Error: Request timed out` | Provider is slow; check network |
| Unexpected exception | `Error: <exception type>: <message>` | Check application logs |

---

## 10. M1 Success Definition

**M1 is DONE when:**

- ✅ CLI smoke test passes locally (manual invocation)
- ✅ CLI smoke test passes in GitHub Actions (automated)
- ✅ Response format is correct (all fields present)
- ✅ Session is persisted (validated in SQLite)
- ✅ Zero errors or warnings in CLI output
- ✅ Duration is recorded correctly

**Exit Blocker:** If smoke test fails on final M1 build, Ripley gates M1 completion.

---

## References

- [TEST-FRAMEWORK.md](./TEST-FRAMEWORK.md) — Test framework specs
- [M1-QUALITY-GATES.md](./M1-QUALITY-GATES.md) — M1 quality gates (smoke test is Gate 6)
- [M1-TASK-CRITERIA.md](./M1-TASK-CRITERIA.md) — T7 detailed acceptance criteria
