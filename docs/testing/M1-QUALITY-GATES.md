# M1 Quality Gates Specification

**Status:** ✅ LOCKED  
**Date:** 2026-05-22  
**Owner:** Lambert (Tester)  
**Applies to:** All M1 tasks (T1–T12) and M1 exit criteria

---

## Executive Summary

HermesNET M1 enforces **six quality gates** to ensure architectural integrity, code quality, performance, and scale. All gates are **measurable** in CI; gates marked as **hard** block milestone progression.

| Gate | Target | Hard? | Enforcer |
|------|--------|-------|----------|
| **Gate 1: Code Coverage** | ≥ 80% branch (Hermes.Core) | ✅ Yes | Coverlet + CI |
| **Gate 2: Build Cleanliness** | Zero compiler warnings | ✅ Yes | `TreatWarningsAsErrors=true` |
| **Gate 3: Dependency Security** | Zero critical/high CVEs | ✅ Yes | Dependabot + manual audit |
| **Gate 4: R1 Integration Test** | E2E chat → provider → response works | ✅ Yes | Ripley code review |
| **Gate 5: OTel Baseline** | P95 turn latency ≤ 100 ms | ⚠️ Soft | Parker's measurement |
| **Gate 6: R5 Load Test** | 1,000 sessions, P95 latencies measured | ✅ Yes | Dallas + Lambert execution |

---

## Gate 1: Code Coverage

### Specification

**Metric:** Branch coverage on Hermes.Core  
**Target:** ≥ 80%  
**Tool:** Coverlet  
**Hard Gate:** ✅ YES — M1 build fails if < 80%  

### Measurement

```bash
dotnet test --collect:"XPlat Code Coverage"
# Report: TestResults/*/coverage.opencover.xml
```

### In-Scope Modules for 80% Branch Coverage

- `Hermes.Core/Session/SessionStore.cs` — all CRUD operations, error paths
- `Hermes.Core/Providers/ChatClientFactory.cs` — both provider branches (Ollama, OpenAI config)
- `Hermes.Core/Chat/HermesChatService.cs` — chat request orchestration, null checks
- `Hermes.Core/Skills/SkillParser.cs` — valid + 5 malformed input cases
- `Hermes.Core/Telemetry/HermesTelemetry.cs` — span lifecycle (start, end, events)

### Out-of-Scope (M2+)

- Hermes.Host (DI container — integration only)
- Hermes.Cli (System.CommandLine routing — smoke test only)

### Enforcement in CI

- GitHub Actions workflow runs `dotnet test --collect:"XPlat Code Coverage"`
- Parses `coverage.opencover.xml` for Hermes.Core branch coverage %
- If < 80%: workflow fails with clear message: `"Branch coverage 75% < gate 80%"`
- PR cannot merge until coverage gate passes

### M1 Coverage Responsibility

- **Dallas (T6–T9):** Writes all unit test cases; target 80% branch coverage
- **Lambert (ongoing):** Audits coverage reports; flags modules < 80%
- **Ripley (T12):** Approves M1 completion only if gate passes

---

## Gate 2: Build Cleanliness

### Specification

**Metric:** Compiler warnings count  
**Target:** Zero warnings  
**Tool:** `TreatWarningsAsErrors=true`  
**Hard Gate:** ✅ YES — M1 build fails if any warning detected  

### Implementation

**File:** `Directory.Build.props`

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>4</WarningLevel>
</PropertyGroup>
```

### Enforcement

- All projects inherit `TreatWarningsAsErrors=true` from `Directory.Build.props`
- `dotnet build` converts any warning to error → non-zero exit code → build fails
- CI workflow fails if `dotnet build` exit code ≠ 0
- Developers must fix warnings locally before pushing

### M1 Responsibility

- **Dallas:** Ensures code changes emit zero warnings
- **Lambert:** CI monitors; reports any warnings to team immediately
- **Ripley:** Approves M1 only if build is clean

---

## Gate 3: Dependency Security

### Specification

**Metric:** Known vulnerabilities (CVEs) in dependencies  
**Target:** Zero critical/high severity CVEs  
**Tools:** Dependabot + manual audit  
**Hard Gate:** ✅ YES — M1 blocks on critical/high vulnerabilities  

### Automation: Dependabot

**File:** `.github/dependabot.yml`

```yaml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10
```

**Behavior:**
- Scans `Directory.Packages.props` weekly
- Opens PR for each vulnerable dependency
- Auto-labels PRs with `dependencies` tag
- Ash reviews, approves low-risk updates; blocks high-risk until remediation

### M1 Audit Scope

- All packages in `Directory.Packages.props` scanned
- Focus: xUnit, Coverlet, Microsoft.Extensions.*, OpenTelemetry, System.CommandLine
- Known safe: xUnit (no known CVEs), Coverlet (no known CVEs), Microsoft libs (patched weekly)

### M1 Responsibility

- **Ash (T3):** Initial audit; flags any critical/high CVEs; recommends upgrades
- **Dallas:** Applies patches if needed
- **Lambert:** Monitors Dependabot; escalates any findings to Ripley

### Enforcement

- Dependabot PRs reviewed before merge
- No critical/high CVEs allowed in M1 final dependency list
- If critical found, Ripley decides: patch or remediate before M1 exit

---

## Gate 4: R1 Integration Test

### Specification

**Metric:** End-to-end chat loop validation  
**Test:** `R1IntegrationDrift.cs`  
**Target:** Config → Factory → Provider → Response works correctly  
**Hard Gate:** ✅ YES — Must pass before Week 2 begins  

### R1 Test Scope

**Test Case:** `ChatFlow_EndToEnd_ReturnsValidResponseAndPersistsSession`

**Steps:**
1. Initialize `IHermesChatService` with Ollama provider
2. Send chat message: `"What is 2+2?"`
3. Assert: response is non-empty string
4. Assert: response contains expected structure (not null/empty)
5. Assert: session was saved to SQLite
6. Assert: session ID is persisted + retrievable

**Pass Criteria:**
- Real response from Ollama (or mock if Ollama unavailable)
- Session exists in database after chat
- Tool invocation works (if applicable)

### R1 Ripley Review

**Checklist:**
- [ ] Abstraction map (Hermes → MAF → IChatClient) has zero concept mismatches
- [ ] Session concept maps correctly to `ISessionStore`
- [ ] Provider routing is configuration-driven (no hardcoded provider)
- [ ] Tool invocation (if used) maps to MAF function tools
- [ ] No "temporary scaffolding" left in codebase

**Sign-off:** Ripley approves abstraction map explicitly in PR; marks "R1 GREEN"

### R1 Responsibility

- **Dallas:** Writes `R1IntegrationDrift.cs` test (T2 spike)
- **Ripley:** Reviews architecture; approves R1 GREEN (Week 1, Day 4)
- **Lambert:** Ensures R1 test runs in CI; reports pass/fail status to team

---

## Gate 5: OTel Baseline (Soft Gate)

### Specification

**Metric:** P95 turn latency (OTel measured)  
**Target:** ≤ 100 ms (local Ollama, single turn)  
**Tool:** OpenTelemetry tracing + Parker's analysis  
**Hard Gate:** ⚠️ NO — Informational; becomes M2 regression baseline  

### Measurement Details

**Definition:** Turn latency = user CLI message → agent response returned (excluding SQLite persist)

**OTel Instrumentation:**
- Parent span: `hermes.chat.request` (start → response received)
- Child spans: `hermes.provider.call` (OTel provider latency)
- Measured with OTLP exporter to local collector

**Test Procedure (Parker, T4):**
1. Start OTLP collector locally (e.g., Jaeger)
2. Run HermesChatService with OTel enabled
3. Send 50 sequential chat requests to Ollama
4. Collect `hermes.chat.request` span durations
5. Calculate P95 latency across 50 samples
6. Report to `docs/benchmarks/m1-perf-baseline.md`

**Pass Criteria:**
- P95 ≤ 100 ms (soft target; doesn't block if slightly higher)
- All spans emitted correctly (no missing traces)
- OTel overhead < 10% (measured as delta from non-OTel run)

### M1 Responsibility

- **Parker:** Executes baseline measurement (T4)
- **Lambert:** Records baseline in benchmark docs for M2 regression testing
- **Ripley:** Uses baseline to set M2 performance budgets

---

## Gate 6: R5 Load Test

### Specification

**Metrics:** Concurrent session scalability + parser robustness  
**Hard Gate:** ✅ YES — M1 blocks on R5 failure  
**Executes:** Week 2, Day 8–9 (before T12 go/no-go)  

### R5-A: Session Store Load Test

**Test:** `SessionLoadTest.cs`

**Concurrency Model:**
1. **Phase 1 (Sequential Inserts):** Insert 1,000 sessions sequentially, measure P95 latency
2. **Phase 2 (Concurrent Reads):** 10 parallel readers, 100 queries each on recent sessions, measure P95 latency

**Targets:**
| Metric | Target | Measured by |
|--------|--------|-------------|
| P95 insert latency | ≤ 50 ms | SQLite.NET/EF Core timing |
| P95 query latency | ≤ 20 ms | SQLite timing |
| Throughput | ≥ 20 inserts/sec | Sequential / elapsed time |

**Test Output:**
- Pass: `R5-A: PASSED (P95 insert 45ms, P95 query 18ms)`
- Fail: `R5-A: FAILED (P95 query 25ms > gate 20ms)`

**Report Location:** `docs/benchmarks/m1-session-load.md`

### R5-B: YAML Parser Robustness

**Test:** `SkillParserTests.cs`

**Test Cases (6 total):**

| Case | Input | Expected |
|------|-------|----------|
| Valid | `valid-skill.yml` | `SkillDefinition` object |
| Malformed #1 | Empty YAML | `SkillParseException` |
| Malformed #2 | Missing `name:` field | `SkillParseException` |
| Malformed #3 | Missing `type:` field | `SkillParseException` |
| Malformed #4 | Null values for required fields | `SkillParseException` |
| Malformed #5 | Invalid YAML syntax (unclosed bracket) | `SkillParseException` |

**Fixtures Location:** `tests/Hermes.Core.Tests/Skills/fixtures/`

**Pass Criteria:**
- All 5 malformed cases throw `SkillParseException` (or appropriate error)
- 1 valid case parses successfully
- All 6 tests pass

**Test Output:**
- Pass: `R5-B: PASSED (6/6 tests passed)`
- Fail: `R5-B: FAILED (1 malformed case accepted instead of rejected)`

### R5 Execution Responsibility

- **Dallas:** Implements `SessionLoadTest.cs` and runs Phase 1 + 2 (T8)
- **Dallas:** Writes `SkillParserTests.cs` (T9)
- **Lambert:** Monitors execution; reports results to Ripley
- **Ripley:** Approves R5 GREEN; gates M1 completion

### R5 Sign-off

**Date:** Week 2, Day 9 (June 5, 2026)

**Checklist:**
- [ ] R5-A: P95 insert ≤ 50 ms ✅
- [ ] R5-A: P95 query ≤ 20 ms ✅
- [ ] R5-B: All 5 malformed cases rejected ✅
- [ ] R5-B: 1 valid case accepted ✅
- [ ] Results committed to `docs/benchmarks/m1-session-load.md` ✅
- [ ] Ripley approves "R5 GREEN" ✅

**If R5 fails:** Max 3-day remediation sprint; no M1 completion until R5 GREEN

---

## Gate Implementation: CI Workflow

**File:** `.github/workflows/ci.yml`

```yaml
name: M1 Quality Gates

on: [push, pull_request]

jobs:
  quality-gates:
    runs-on: ubuntu-latest
    steps:
      # Gate 1 + 2: Build + Coverage
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build  # Fails if warnings (Gate 2)
      - run: dotnet test --collect:"XPlat Code Coverage"
      
      # Parse coverage gate
      - name: Check Coverage Gate
        run: |
          # Parse coverage.opencover.xml; extract Hermes.Core branch coverage
          coverage=$(./scripts/parse-coverage.sh TestResults/coverage.opencover.xml Hermes.Core)
          if (( $(echo "$coverage < 80" | bc -l) )); then
            echo "❌ Coverage $coverage% < gate 80%"
            exit 1
          fi
          echo "✅ Coverage $coverage% >= 80%"
      
      # Gate 3: Dependabot runs weekly; flagged in separate workflow
      
      # Gates 4–6: Manual/weekly execution
      # (R1 tested in T2; R5 tested Week 2)
```

---

## Gate Summary Table

| # | Gate | Metric | Target | Hard? | Enforcer | Timeline |
|---|------|--------|--------|-------|----------|----------|
| 1 | Code Coverage | Branch % (Hermes.Core) | ≥ 80% | ✅ | Coverlet CI | Every PR |
| 2 | Build Cleanliness | Warning count | 0 | ✅ | TreatWarningsAsErrors | Every PR |
| 3 | Security | CVE count (critical/high) | 0 | ✅ | Dependabot | Weekly |
| 4 | R1 Integration | E2E chat loop | Pass | ✅ | Ripley review | Week 1, Day 4 |
| 5 | OTel Baseline | P95 turn latency | ≤ 100 ms | ⚠️ | Parker measure | T4 (Day 3) |
| 6 | R5 Load Test | P95 insert/query + parser | Meet targets | ✅ | Dallas execute | Week 2, Day 8–9 |

---

## M1 Exit Criteria

**Ripley approves M1 COMPLETE only when:**

- ✅ Gate 1: Coverage ≥ 80% (Coverlet CI report)
- ✅ Gate 2: Zero warnings (build clean)
- ✅ Gate 3: Zero critical/high CVEs (Dependabot + audit)
- ✅ Gate 4: R1 GREEN (Ripley-signed abstraction map)
- ⚠️ Gate 5: OTel baseline recorded (informational; no blocker)
- ✅ Gate 6: R5 GREEN (P95 latencies met, parser tests pass)

**If any hard gate fails:** 3-day remediation sprint; Ripley reassesses before final approval.

---

## References

- [TEST-FRAMEWORK.md](./TEST-FRAMEWORK.md) — xUnit, Coverlet, FluentAssertions specs
- [M1-TASK-CRITERIA.md](./M1-TASK-CRITERIA.md) — Per-task acceptance criteria
- [.squad/decisions.md](../../.squad/decisions.md) — M1 blocker resolutions
