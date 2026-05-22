# M1 Task Acceptance Criteria Summary

**Status:** ✅ LOCKED  
**Date:** 2026-05-22  
**Owner:** Lambert (Tester)  
**Critical Path:** T1 → T2(R1) → T3 → T6 → T8/T9(R5) → T12(Go/No-Go)

---

## Overview

All 12 M1 tasks have explicit, measurable acceptance criteria below. A task is **DONE** only when all acceptance criteria are met and verified.

---

## T1 — Project Setup & Solution Scaffold

**Owner:** Ripley  
**Timeline:** Day 1 (May 22)  
**Blocker:** Must complete before T2–T11 start

### Acceptance Criteria

- [ ] **Solution Structure**
  - Root: `HermesNET.slnx` loads in Visual Studio 2025
  - Three projects: `Hermes.Core`, `Hermes.Host`, `Hermes.Cli`
  - All projects reference correct .NET 10

- [ ] **Build Configuration**
  - `Directory.Build.props` exists with:
    - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
    - `<LangVersion>13</LangVersion>`
    - `<Nullable>enable</Nullable>`
  - `Directory.Packages.props` exists with centralized xUnit, Coverlet, MAF versions
  - `global.json` enforces .NET 10.0 minimum

- [ ] **NuGet Packages** (verified in `Directory.Packages.props`)
  - xUnit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio
  - Coverlet.Collector
  - Microsoft.Extensions.AI.Abstractions, Microsoft.Extensions.DependencyInjection
  - System.CommandLine
  - OpenTelemetry + OTLP exporter
  - FluentAssertions

- [ ] **Build Verification**
  - `dotnet build` succeeds, zero warnings
  - Runs on Windows, Linux, macOS
  - `dotnet restore` succeeds without errors

**Done Definition:**
```
dotnet build → Success, 0 warnings, exit code 0
All projects load in VS 2025 → Success
dotnet test → Discovers 0 test projects initially → Success
```

---

## T2 — Provider Abstraction & IChatClient Integration (R1 Spike)

**Owner:** Dallas  
**Timeline:** Days 2–4 (May 23–25)  
**Blocker:** R1 (integration test passes + Ripley approves architecture)  
**Depends on:** T1

### Acceptance Criteria

- [ ] **IChatClient Factory**
  - `ChatClientFactory.cs` exists in `Hermes.Core/Providers/`
  - Factory method: `IChatClient CreateChatClient(ProviderConfig config)`
  - Supports "Ollama" and "OpenAI" provider names (config-driven)
  - Returns valid `IChatClient` for each provider

- [ ] **Configuration**
  - `appsettings.json` in `Hermes.Host` with `"Provider": "Ollama"` key
  - Configuration is read at startup; swapping provider requires config change only (no code recompile)

- [ ] **IHermesChatService**
  - Interface defined in `Hermes.Core/Chat/`
  - Accepts chat message string, returns response string
  - Signature: `Task<string> SendMessageAsync(string message)`
  - Internally uses `IChatClient` and logs via Telemetry

- [ ] **DI Registration**
  - `Program.cs` in `Hermes.Host` registers:
    - `IHermesChatService` → `HermesChatService`
    - `IChatClient` → result of `ChatClientFactory.CreateChatClient(...)`
  - No manual instantiation needed; full DI container wiring

- [ ] **R1 Integration Test**
  - Test: `R1IntegrationDrift.cs` in `tests/Hermes.Core.Tests/Integration/`
  - E2E: config → factory → provider → response
  - Test passes: real response received from Ollama (or mock)
  - Session ID is generated and retrievable
  - Ripley reviews abstraction map; approves "R1 GREEN"

**Done Definition:**
```
ChatClientFactory_CreateClient_ReturnsValidClient → Pass
HermesChatService_SendMessage_ReturnsResponseString → Pass
R1IntegrationDrift → Pass
Ripley: "R1 GREEN" sign-off in PR comment → Yes
```

---

## T3 — Dependency Security Audit (Ash's Review)

**Owner:** Ash  
**Timeline:** Day 3 (May 24)  
**Depends on:** T1

### Acceptance Criteria

- [ ] **Audit Scope**
  - All packages in `Directory.Packages.props` scanned
  - Focus: xUnit, Coverlet, Microsoft.Extensions.*, OpenTelemetry, System.CommandLine

- [ ] **CVE Scan**
  - Use Dependabot or `dotnet list package --vulnerable`
  - Identify all critical/high severity vulnerabilities
  - None found: "✅ PASS"
  - Any found: Document & recommend patch version

- [ ] **Report**
  - Document findings in PR comment or `.squad/audit.md`
  - Example: "✅ Zero critical/high CVEs. All packages current as of 2026-05-24."

**Done Definition:**
```
dotnet list package --vulnerable → No critical/high CVEs
Ash: "Audit complete, zero blockers" → Yes
```

---

## T4 — OTel Instrumentation & Baseline (Parker's Measurement)

**Owner:** Parker  
**Timeline:** Days 3–4 (May 24–25)  
**Depends on:** T2 (IHermesChatService working)

### Acceptance Criteria

- [ ] **OTel Instrumentation**
  - `HermesTelemetry.cs` defined in `Hermes.Core/Telemetry/`
  - `ActivitySource` created with name "HermesNET"
  - Span names: `hermes.chat.request`, `hermes.provider.call`, `hermes.session.save`

- [ ] **Span Emission**
  - Parent span: `hermes.chat.request` wraps chat request start → response received
  - Child span: `hermes.provider.call` wraps provider (Ollama/OpenAI) call
  - Span attributes: `provider.name`, `session.id`, `message.length`

- [ ] **OTLP Exporter**
  - `Program.cs` configures OpenTelemetry with OTLP exporter to `http://localhost:4317`
  - Local Jaeger or Tempo collector receives traces

- [ ] **Performance Baseline**
  - Run 50 sequential chat requests to local Ollama
  - Collect `hermes.chat.request` span durations
  - Calculate P95 latency
  - Document result: "P95 turn latency: X ms" in `docs/benchmarks/m1-perf-baseline.md`
  - Target: ≤ 100 ms (soft gate; informational only)

- [ ] **Telemetry Tests**
  - `HermesTelemetryTests.cs` verifies spans are created correctly
  - Test: `HermesTelemetry_CreateSpan_EmitsCorrectly` → Pass

**Done Definition:**
```
HermesTelemetry_CreateSpan_EmitsCorrectly → Pass
50-request baseline run → Complete
docs/benchmarks/m1-perf-baseline.md → "P95 latency ≤ 100ms" (or measured value)
```

---

## T5 — Quality Gate Specifications & Test Framework Lock (Your Task)

**Owner:** Lambert  
**Timeline:** Days 4–5 (May 26–27)  
**Depends on:** T1–T4

### Acceptance Criteria

- [ ] **TEST-FRAMEWORK.md**
  - xUnit standard documented
  - Coverlet coverage tooling specified
  - 80% branch coverage gate defined
  - GitHub Actions CI workflow specified

- [ ] **M1-QUALITY-GATES.md**
  - All 6 gates documented with targets
  - Gate 1 (Coverage ≥ 80%) — Hard gate
  - Gate 2 (Zero warnings) — Hard gate
  - Gate 3 (Zero critical/high CVEs) — Hard gate
  - Gate 4 (R1 integration test passes) — Hard gate
  - Gate 5 (OTel baseline ≤ 100 ms) — Soft gate
  - Gate 6 (R5 load test) — Hard gate

- [ ] **CLI-SMOKE-TEST.md**
  - Smoke test procedure specified
  - Expected output format defined
  - Success criteria listed
  - Failure modes documented

- [ ] **TEST-CONVENTIONS.md**
  - Test naming: `[Method]_[Scenario]_[Result]` documented
  - xUnit patterns (Fact, Theory) documented
  - FluentAssertions standard documented
  - Prohibited patterns (base classes, SetUp/TearDown) documented

- [ ] **M1-TASK-CRITERIA.md** (this file)
  - All 12 task acceptance criteria defined
  - Critical path documented

- [ ] **GitHub Actions CI Workflow** (`.github/workflows/ci.yml`)
  - Runs on every PR + commit to main
  - Steps: Checkout → Setup .NET → Restore → Build → Test → Coverage check
  - Fails if: build fails, warnings detected, tests fail, coverage < 80%
  - Publishes coverage artifact

- [ ] **Hermes.Core.Tests/Usings.cs**
  - Shared imports defined
  - xUnit, FluentAssertions, Hermes.* namespaces included

**Done Definition:**
```
All 7 docs exist → Yes
CI workflow in .github/workflows/ci.yml → Yes
Usings.cs in tests/Hermes.Core.Tests/ → Yes
All gates defined + measurable → Yes
```

---

## T6 — Session Store & EF Core Integration

**Owner:** Dallas  
**Timeline:** Days 2–4 (May 23–25)  
**Depends on:** T1, T2

### Acceptance Criteria

- [ ] **SessionStore Implementation**
  - `ISessionStore` interface in `Hermes.Core/Session/`
  - Methods: `InsertSessionAsync(session)`, `GetSessionAsync(id)`, `UpdateSessionAsync(session)`
  - Uses EF Core + SQLite
  - Handles null checks, returns appropriate exceptions

- [ ] **Database Schema**
  - EF Core Code First migrations for ChatSession entity
  - Migration: `AddInitialSchema` in `Hermes.Core/Migrations/`
  - Runs automatically on startup via `DbContext.Database.MigrateAsync()`

- [ ] **Unit Tests**
  - `SessionStoreTests.cs` with xUnit + FluentAssertions
  - Tests: Insert, Get, Update, Delete, exception cases
  - Target: ≥ 80% branch coverage on SessionStore
  - All tests pass: `dotnet test`

- [ ] **Integration with T2**
  - `IHermesChatService` saves session after each chat via `ISessionStore`
  - Session persists to SQLite after message processed

**Done Definition:**
```
dotnet test → All session store tests pass
Coverage: SessionStore branch coverage ≥ 80%
dotnet build → Success, 0 warnings
```

---

## T7 — CLI Integration & Smoke Test

**Owner:** Dallas  
**Timeline:** Day 3 (May 24)  
**Depends on:** T1, T2, T6

### Acceptance Criteria

- [ ] **CLI Command**
  - Command: `hermes chat --profile default --message "text"`
  - Implemented via `System.CommandLine` in `Hermes.Cli/Program.cs`
  - Routes to `IHermesChatService` via DI

- [ ] **Output Format**
  - Response text from provider
  - Session ID (UUID)
  - Turn ID (integer ≥ 1)
  - Duration (milliseconds)
  - Example: `Response: 2+2=4\nSession ID: 550e8400...\nTurn ID: 1\nDuration: 245ms`

- [ ] **Smoke Test**
  - Manual: `dotnet run --project src/Hermes.Cli -- chat --profile default --message "hello"`
  - Expected: Response received, session saved, exit code 0
  - CI: Automated smoke test in GitHub Actions

- [ ] **Error Handling**
  - Missing `--message`: Show usage error
  - Provider unreachable: Show error message
  - Exit code 0 on success; non-zero on error

**Done Definition:**
```
Manual smoke test → Response received, session saved, exit code 0
CI smoke test → Passes on every PR
Output format → Matches specification
```

---

## T8 — Session Load Test & Performance Measurement (R5-A)

**Owner:** Dallas  
**Timeline:** Days 7–8 (May 28–29)  
**Depends on:** T6

### Acceptance Criteria

- [ ] **Load Test Scenario**
  - Phase 1: Insert 1,000 sessions sequentially, measure latency
  - Phase 2: 10 parallel readers, 100 queries each, measure query latency
  - Test: `SessionLoadTest.cs` in `tests/Hermes.Core.Tests/`

- [ ] **Metrics**
  - P95 insert latency ≤ 50 ms
  - P95 query latency ≤ 20 ms
  - Calculate percentiles from 1,000+ measurements

- [ ] **Benchmark Report**
  - Document results in `docs/benchmarks/m1-session-load.md`
  - Example:
    ```
    ## M1 Session Load Test Results
    - Inserts: 1,000 sequential
    - P95 insert latency: 48 ms ✅
    - Queries: 1,000 concurrent
    - P95 query latency: 19 ms ✅
    - Date: 2026-05-28
    ```

- [ ] **R5-A Gate Validation**
  - P95 insert ≤ 50 ms → Pass
  - P95 query ≤ 20 ms → Pass
  - Ripley reviews; marks "R5-A GREEN"

**Done Definition:**
```
SessionLoadTest runs end-to-end → Success
P95 insert ≤ 50 ms → Yes
P95 query ≤ 20 ms → Yes
docs/benchmarks/m1-session-load.md → Complete
```

---

## T9 — Skill Parser & YAML Validation (R5-B)

**Owner:** Dallas  
**Timeline:** Days 7–8 (May 28–29)  
**Depends on:** T1

### Acceptance Criteria

- [ ] **SkillParser Implementation**
  - `SkillParser.cs` in `Hermes.Core/Skills/`
  - Method: `SkillDefinition Parse(string yamlContent)`
  - Validates: non-empty YAML, required fields (`name`, `type`, `description`)
  - Throws `SkillParseException` on validation failure

- [ ] **Test Fixtures**
  - Malformed cases (5 total):
    - Empty YAML file
    - Missing `name` field
    - Missing `type` field
    - Null values for required fields
    - Invalid YAML syntax
  - Valid case (1 total):
    - Complete, valid skill YAML

- [ ] **Unit Tests**
  - `SkillParserTests.cs` with xUnit + FluentAssertions
  - 6 parameterized test cases (5 malformed + 1 valid)
  - All tests pass: `dotnet test`
  - Example: `SkillParser_MissingNameField_ThrowsSkillParseException` → Pass

- [ ] **R5-B Gate Validation**
  - All 5 malformed cases rejected with `SkillParseException` → Pass
  - 1 valid case accepted, returns `SkillDefinition` → Pass
  - Ripley reviews; marks "R5-B GREEN"

**Done Definition:**
```
SkillParserTests: 6/6 tests pass
5 malformed cases throw SkillParseException → Yes
1 valid case returns SkillDefinition → Yes
dotnet build → Success, 0 warnings
```

---

## T10 — Integration & Architecture Validation (R1 + R5 Final)

**Owner:** Dallas  
**Timeline:** Days 8–9 (May 28–29)  
**Depends on:** T2 (R1), T8–T9 (R5)

### Acceptance Criteria

- [ ] **R1 Validation (Architecture)**
  - Ripley reviews abstraction map (Hermes → MAF → IChatClient)
  - Zero concept mismatches identified
  - Architecture is documented in `docs/architecture/abstraction-map.md` (or PR description)
  - Sign-off: "R1 GREEN" in PR

- [ ] **R5 Validation (Scale)**
  - R5-A: P95 insert ≤ 50 ms, P95 query ≤ 20 ms → Pass
  - R5-B: 5 malformed YAML rejected, 1 valid accepted → Pass
  - Results committed to `docs/benchmarks/m1-session-load.md`
  - Sign-off: "R5 GREEN" in PR or issue

- [ ] **Coverage Audit**
  - Hermes.Core branch coverage ≥ 80% (Coverlet report)
  - No uncovered critical paths
  - Report: `TestResults/*/coverage.opencover.xml`

- [ ] **Build Cleanliness**
  - `dotnet build` → 0 warnings
  - All tests pass: `dotnet test`
  - CI workflow succeeds

**Done Definition:**
```
R1 GREEN → Ripley sign-off
R5 GREEN → Ripley sign-off
Coverage ≥ 80% → Coverlet report confirms
Build clean → 0 warnings
```

---

## T11 — Bug Fixes & Test Coverage Completion

**Owner:** Dallas  
**Timeline:** Day 9 (May 29)  
**Depends on:** T10

### Acceptance Criteria

- [ ] **Coverage Gap Fixes**
  - Any module < 80% branch coverage → add tests
  - Target: All critical modules ≥ 80%

- [ ] **Bug Triage**
  - Any bugs found during load test (T8) or smoke test (T7) → fixed
  - Example: "Session insertion timeout under 1,000 inserts" → fixed

- [ ] **Regression Tests**
  - If bug fix required code change → write test for regression
  - All tests pass: `dotnet test`

- [ ] **Final Build**
  - `dotnet build` → Success, 0 warnings
  - `dotnet test` → All tests pass
  - Coverage ≥ 80% on Hermes.Core

**Done Definition:**
```
All modules ≥ 80% branch coverage
dotnet test → All tests pass
dotnet build → 0 warnings
Ripley approves PR → Yes
```

---

## T12 — Go/No-Go Decision & M1 Completion

**Owner:** Ripley  
**Timeline:** Day 10 (May 30)  
**Depends on:** All T1–T11

### Acceptance Criteria

- [ ] **Gate Validation**
  - ✅ Gate 1: Coverage ≥ 80% (Coverlet report)
  - ✅ Gate 2: Zero warnings (build clean)
  - ✅ Gate 3: Zero critical/high CVEs (audit clean)
  - ✅ Gate 4: R1 GREEN (architecture approved)
  - ⚠️ Gate 5: OTel baseline recorded (informational)
  - ✅ Gate 6: R5 GREEN (scale + parser validated)

- [ ] **All Tasks Complete**
  - T1–T11 all marked DONE
  - All PRs merged to main
  - No open blockers or regressions

- [ ] **Go/No-Go Decision**
  - All gates GREEN → **GO**: M1 APPROVED, M2 gates open
  - Any gate RED → **NO-GO**: 3-day remediation sprint, re-assess

- [ ] **M1 Completion Sign-off**
  - Document decision in `.squad/decisions.md`
  - Update project status: "M1 Complete, M2 Ready"

**Done Definition:**
```
All gates GREEN → Ripley signs off "M1 APPROVED"
All 12 tasks complete + merged → Yes
No open blockers → Yes
M2 can start → Yes (decision documented)
```

---

## Critical Path Summary

```
T1 (Setup)
    ↓
    ├─→ T2 (Provider) → R1 Validation ✅
    ├─→ T3 (Security Audit)
    ├─→ T4 (OTel Baseline)
    ├─→ T5 (Quality Gates) ← YOU ARE HERE
    │
    ├─→ T6 (Session Store)
    │   ├─→ T7 (CLI Smoke Test)
    │   └─→ T8 (Load Test) ─┐
    │                        ├─→ T10 (Integration) → R5 Validation ✅
    └─→ T9 (Skill Parser) ──┘
        │
        ├─→ T11 (Bug Fixes)
        │
        └─→ T12 (Go/No-Go)
```

---

## Key Dates & Milestones

| Date | Milestone | Owner |
|------|-----------|-------|
| May 22 | T1–T5 complete | Ripley, Parker, Ash, Lambert |
| May 25 | R1 GREEN | Dallas, Ripley |
| May 24 | Smoke test (T7) works locally | Dallas |
| May 28 | R5-A GREEN (load test P95 latencies) | Dallas |
| May 28 | R5-B GREEN (skill parser) | Dallas |
| May 30 | T12 Go/No-Go decision | Ripley |

---

## References

- [M1-QUALITY-GATES.md](./M1-QUALITY-GATES.md) — Gate specifications
- [TEST-FRAMEWORK.md](./TEST-FRAMEWORK.md) — Test framework details
- [CLI-SMOKE-TEST.md](./CLI-SMOKE-TEST.md) — CLI acceptance criteria
- [TEST-CONVENTIONS.md](./TEST-CONVENTIONS.md) — Test writing standards
- [.squad/decisions.md](../../.squad/decisions.md) — M1 blocker resolutions
