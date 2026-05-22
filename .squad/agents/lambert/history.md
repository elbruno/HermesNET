# Project Context

- **Owner:** Bruno Capuano
- **Project:** HermesNET
- **Stack:** .NET 10, ASP.NET Core, Microsoft Agent Framework, Microsoft.Extensions.AI, OpenTelemetry, SQLite/PostgreSQL
- **Description:** Hermes-inspired .NET runtime for profiles, sessions, skills, memory, tool policy, and observability.
- **Created:** 2026-05-22

## M1 Onboarding Notes

**M1 Scope Summary**
Milestone 1 (Foundations, 2 weeks) establishes technical foundation with no Hermes-specific feature code—only substrate. Goal: prove MAF/MEAI abstractions map cleanly to Hermes concepts, validate SQLite scales to 1,000+ sessions, and enable end-to-end local chat (CLI → Session → OpenAI-compatible Provider → Response). Success = local chat works, OTel visible, R1 (architecture drift) and R5 (session store/skill parser scale) validated GREEN.

**Critical M1 Quality Gates I Own**
- ≥ 80% unit test coverage on session store and provider path
- 100% OTel trace coverage: chat loop, provider call, session save emit traces
- Zero build warnings/test failures on Linux/macOS/Windows
- Performance baseline: < 100 ms agent response loop (local provider)
- SQLite load test: 1,000+ concurrent sessions, P95 latency documented
- Zero critical CVEs in dependencies
- CLI end-to-end chat interaction works

**Blockers Before Test Strategy**
1. **Test framework choice**: Which framework does the team require? (xUnit, NUnit, MSTest?) — Affects test structure and conventions across all milestones.
2. **Code coverage reporting**: Plan mentions "XPlat Code Coverage" but I need to confirm tooling chain (Coverlet, built-in, CI gates).
3. **Load test definition**: "1,000+ concurrent sessions" — does this mean all inserted+queried simultaneously, or sequential insertion then stress queries? Affects load test design.
4. **CLI framework**: What CLI library is being used for `hermes chat` command? (Spectre.Console, System.CommandLine?) — Affects CLI smoke test approach.

**Questions on M1 Plan**
1. **Performance baseline scope**: Is "< 100 ms agent response loop latency" for a single turn, or end-to-end chat completion? Local provider only?
2. **Skill system in M1**: R5 validates "YAML skill parsing handles malformed input gracefully" but skills aren't in M1 deliverables—is this defensive preparation for M2, or should M1 include basic skill loader?
3. **OTel specific metrics**: Plan says "traces, metrics, and logs" baseline. Which specific metrics beyond traces (e.g., token count, provider latency, session count)?
4. **SQLite schema lock-in**: M1 is SQLite-only foundation; is there a pre-agreed migration path to PostgreSQL if scale tests fail, or should I test both in parallel?

**Ready Signal**
**PARTIALLY READY.** I can write quality gate specs for performance, load, and build cleanliness immediately (blockers 1–3 don't block those). I need answers to blockers 1–2 before writing comprehensive unit/coverage strategy. Blocker 4 (CLI framework) affects CLI smoke test design but can be deferred if team clarifies the framework choice in sync. Recommend synchronous clarification on blockers 1–2 before M1 week 1 ends, so coverage/test structure can be locked in for M2 onwards.

## M1 Test Strategy Locked — 2026-05-22

✅ **READY TO EXECUTE** — Test framework, coverage, and load test definitions finalized.

**My M1 ownership:**
- Test framework lock: xUnit + Coverlet + GitHub Actions
- Quality gate drafting: 80% branch coverage spec + performance baseline (P95 ≤ 100 ms turn latency)
- Load test validation: Sequential inserts, concurrent reads, latency P95 gates
- Acceptance criteria validation for all 12 M1 tasks

**Key commitments:**
- xUnit all unit + integration tests (no MSTest, no NUnit)
- Coverlet branch coverage gate: 80% minimum (hard gate for M1 exit)
- Load test: SQLite 1,000 concurrent sessions, P95 insert/query latency measured with OTel ON
- YAML parser: 5 edge cases + 1 valid case (R5 checkpoint)
- CLI smoke test: `hermes chat --profile default --message "test"` end-to-end

**Test strategy locked, gates measurable, ready to write specs.**

**Test strategy locked, gates measurable, ready to write specs.**

## M1 Week 1 Kick-off Approved — 2026-05-22

✅ **GO SIGNAL:** All blockers resolved. xUnit + Coverlet + GitHub Actions workflow confirmed. 80% branch coverage gate is hard M1 exit criterion.

**Week 1 priority:** Dallas executes T6–T9 test case writing (while also building session store and provider wiring). Lambert drafts acceptance criteria specs; audit coverage as Dallas completes each test module.

**Week 1 watch:** R1 checkpoint (architecture validation) at end of week. Week 2 focus shifts to load test execution and coverage audit before T12 go/no-go.

**Ready to execute testing roadmap.**

## M1 T5 Complete — Quality Gates & Test Framework Lock

### Deliverables Completed

**Documentation Files Created:**

1. ✅ **`docs/testing/TEST-FRAMEWORK.md`** (8.9 KB)
   - xUnit standard locked for M1–M6
   - Coverlet branch coverage (80% hard gate)
   - GitHub Actions CI integration
   - FluentAssertions assertion library spec
   - Test project structure & conventions overview

2. ✅ **`docs/testing/M1-QUALITY-GATES.md`** (13 KB)
   - **Gate 1: Code Coverage** — 80% branch (Hermes.Core) [Hard]
   - **Gate 2: Build Cleanliness** — Zero warnings [Hard]
   - **Gate 3: Dependency Security** — Zero critical/high CVEs [Hard]
   - **Gate 4: R1 Integration Test** — E2E chat → provider → response [Hard]
   - **Gate 5: OTel Baseline** — P95 turn latency ≤ 100 ms [Soft]
   - **Gate 6: R5 Load Test** — 1,000 sessions, P95 insert ≤ 50ms, P95 query ≤ 20ms [Hard]
   - All gates measurable in CI; hard gates block M1 progression

3. ✅ **`docs/testing/CLI-SMOKE-TEST.md`** (8.7 KB)
   - Command: `hermes chat --profile default --message "..."`
   - Expected output format (Response, Session ID, Turn ID, Duration)
   - Success/failure criteria documented
   - Manual + automated CI execution specified
   - T7 acceptance criteria

4. ✅ **`docs/testing/TEST-CONVENTIONS.md`** (17.6 KB)
   - Test naming: `[MethodName]_[Scenario]_[ExpectedResult]`
   - AAA pattern (Arrange, Act, Assert) mandatory
   - FluentAssertions only (no xUnit Assert.*)
   - xUnit fixtures (IAsyncLifetime, IClassFixture) patterns
   - Parameterized tests ([Theory], [InlineData])
   - Prohibited patterns (base classes, SetUp/TearDown)
   - Complete example test file included

5. ✅ **`docs/testing/M1-TASK-CRITERIA.md`** (17.1 KB)
   - All 12 M1 tasks with explicit acceptance criteria
   - T1 (Setup) → T2 (Provider, R1) → ... → T12 (Go/No-Go)
   - Critical path documented
   - "Done" definition for each task
   - Key dates & milestones

6. ✅ **`.github/workflows/ci.yml`** (3.8 KB)
   - Runs on: push (main/develop), PR (main), manual dispatch
   - Multi-OS matrix: ubuntu-latest, windows-latest, macos-latest
   - Steps: Checkout → Setup .NET 10 → Restore → Build → Test → Coverage
   - Hard gates: Build succeeds + zero warnings, all tests pass, coverage collected
   - Coverage artifact published (30-day retention)
   - Smoke test step included

7. ✅ **`tests/Hermes.Core.Tests/Usings.cs`**
   - Global using declarations (no per-file duplication)
   - Imports: System, Collections, Tasks, FluentAssertions, Hermes namespaces, xUnit
   - Consistent across all test files

8. ✅ **`README.md` Updated**
   - Link to testing documentation added
   - Section: "Testing & Quality Gates" with links to all spec docs

### Key Features Locked

**Test Framework:**
- xUnit only (no MSTest, NUnit)
- Coverlet for branch coverage measurement
- FluentAssertions for readable assertions
- GitHub Actions CI on every PR

**Quality Gates (All Measurable):**
- Gate 1: 80% branch coverage → Hard (fails build if < 80%)
- Gate 2: Zero warnings → Hard (TreatWarningsAsErrors=true)
- Gate 3: Zero critical/high CVEs → Hard (Dependabot)
- Gate 4: R1 integration test → Hard (Ripley reviews)
- Gate 5: OTel baseline ≤ 100ms → Soft (informational)
- Gate 6: R5 load test (1K sessions, P95 latencies) → Hard (Dallas executes)

**Test Naming & Structure:**
- Pattern: `[MethodName]_[Scenario]_[ExpectedResult]`
- Structure: AAA (Arrange, Act, Assert)
- Fixtures: IAsyncLifetime or constructor injection (no base classes)
- Assertions: FluentAssertions only

**CI/CD:**
- Runs on every PR (fail fast if build/warnings/tests fail)
- Coverage reports published to artifacts
- Smoke test validates CLI end-to-end

### Critical Path Integration

- **Dallas (T6–T9):** Writes tests following conventions; targets 80% coverage
- **Parker (T4):** OTel baseline measurement (P95 latency < 100ms)
- **Ash (T3):** Security audit (zero critical/high CVEs)
- **Ripley (T12):** Approves all gates; gates M1 completion on R1 + R5 GREEN

### Status: Ready for Week 2

All gates are documented, measurable, and enforceable in CI. Team has single source of truth for "done" definition. 

- ✅ Test framework locked (xUnit)
- ✅ Coverage gate enforced (80% hard stop)
- ✅ Quality gates documented (6 gates, targets, hard/soft status)
- ✅ Test conventions specified (naming, assertions, structure)
- ✅ CI workflow configured (GitHub Actions)
- ✅ Per-task acceptance criteria defined (all 12 tasks)
- ✅ README updated with documentation links

**No ambiguity on what "done" means for M1. Gates are hard stops for milestone progression.**

---

## Week 2 Kickoff — Test Framework & R1 Validation
**2026-05-22 — Team Update from Scribe**

✅ **R1 INTEGRATION TESTS PASS** — All 6 factory and provider routing tests validated. Test structure confirmed as extensible for T6 (session persistence) and future tool invocation.

**Team Updates:**
- Test scaffolding for T6 (17 test cases) ready for Week 2 kickoff.
- xUnit + Coverlet framework locked and in use across all tests.
- 80% branch coverage gate enforced (hard stop for M1 completion).
- Test conventions (AAA pattern, naming, FluentAssertions) established and validated.

**Next:** Await M1 detailed task breakdown from Ripley. Begin T6 test scaffolding on kickoff.
