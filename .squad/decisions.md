# Squad Decisions

## Active Decisions

### M1-001 — Milestone 1 Task Breakdown Approved

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Gates Dallas to start

**Decision:** Milestone 1 (Foundations, 2 weeks) task breakdown is finalized and approved. 12 tasks across two weeks, with Week 1 focused on solution setup + IChatClient/MAF wiring (R1 spike) + OTel baseline, and Week 2 focused on SQLite session store + CLI + load testing (R5).

**Critical path locked:** T1 → T2(R1) → T3 → T6 → T8/T9(R5) → T12(Go/No-Go)

**Risk checkpoints:**
- R1 (Integration drift): validated in Week 1 via IChatClient → MAF → tool invocation E2E spike; Ripley reviews abstraction map; GREEN required before any Hermes-specific code is written
- R5 (Skills/scale): validated in Week 2 via 1,000-session SQLite load test (P95 insert ≤ 50 ms, P95 query ≤ 20 ms) + 5 malformed YAML parser rejection tests; GREEN required before M2 starts

**Go/No-Go:** Ripley approves M1 completion only when ALL 12 tasks complete AND R1 + R5 are GREEN. Freeze if either is RED — max 3-day remediation sprint.

**Reference:** `.squad/decisions/m1-task-breakdown.md`

### M1-002 — BLOCKER 1: Solution Structure

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Solution scaffold prepared, Dallas verifies

**Decision:** Solution structure is centralized with three-project layout. NuGet versioning uses `Directory.Packages.props` (single source of truth). SQLite schema uses EF Core Code First migrations.

**Rationale:** Central scaffold prevents boilerplate drift across six milestones. Centralized NuGet simplifies security audits (single scan instead of N `.csproj` files). EF Core Code First migrations are version-controlled and auditable.

**Implementation:** Ripley produces root `Hermes.sln` with three projects (`Hermes.Core`, `Hermes.Host`, `Hermes.Cli`), `Directory.Build.props`, `Directory.Packages.props`, `global.json` (enforces .NET 10), and all project files. Dallas verifies: `dotnet build` passes zero warnings on Linux/macOS/Windows, all projects load in IDE, `dotnet test` finds zero test projects initially (expected).

---

### M1-003 — BLOCKER 2: Provider Path

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Provider abstraction locked, Dallas implements

**Decision:** Provider abstraction uses `Microsoft.Extensions.AI.IChatClient`. Runtime provider swapping is configuration-driven via `appsettings.json` (`Provider` key: "Ollama" | "OpenAI") and `ChatClientFactory`. M1 validates Ollama only; OpenAI integration via configuration but not validated in M1.

**Rationale:** IChatClient is vendor-agnostic standard, maintained by Microsoft, forces good abstraction discipline early. Config-driven routing is .NET standard; no code changes needed to swap providers locally. Ollama is local, no credentials, fast iteration—clear latency baseline without network noise.

**Implementation:** Dallas produces `ChatClientFactory.cs`, `appsettings.json`, `IHermesChatService.cs`, and `Program.cs` DI registration (T2, Day 2–3). Ripley reviews abstraction map Week 1 (R1 GREEN trigger).

---

### M1-004 — BLOCKER 3: Test Framework & Coverage

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Test framework locked, Lambert owns quality gates

**Decision:** Test framework is xUnit (Microsoft standard, clean syntax, native async). Coverage tooling is Coverlet (lightweight, CI-friendly). CI integration publishes coverage to `artifacts/coverage/`; M1 target: 80% branch coverage on `Hermes.Core`.

**Rationale:** xUnit is standard for .NET ecosystem, zero magic, excellent async support. Coverlet integrates directly into `dotnet test`. 80% target covers critical path (provider wiring, session store, CLI integration).

**Implementation:** `Directory.Packages.props` includes xUnit, Coverlet, Microsoft.NET.Test.Sdk, Moq, FluentAssertions. New test project structure: `tests/Hermes.Core.Tests/` with Runtime/, Session/, Skills/, Integration/ subdirectories. `.github/workflows/test.yml` runs `dotnet test --collect:"XPlat Code Coverage"`. Dallas writes test cases (T6–T9); Ripley audits sample coverage report before T12.

---

### M1-005 — BLOCKER 4: CLI Framework

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — CLI framework locked, Dallas implements

**Decision:** CLI library is `System.CommandLine` (official Microsoft library). CLI behavior: `hermes chat --profile default --message "Hello"` prints model response to stdout + session ID. `hermes --help` shows command tree.

**Rationale:** System.CommandLine is official, maintained by Microsoft, modern async/await support, integrates with DI. Part of .NET runtime roadmap. `chat` command is the only user-facing CLI in M1; minimalist design (no interactive REPL in M1).

**Implementation:** `src/Hermes.Cli/Program.cs` defines root command with `chat` subcommand, options for `--profile` and `--message`, handler invokes `IHermesChatService` via DI. Usage: `hermes chat --profile default --message "What is 2+2?"` returns response + prints session ID (T7 acceptance).

---

### M1-006 — BLOCKER 5: OTel Baseline & Latency Measurement

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — OTel wired Day 1, Parker owns baseline

**Decision:** OTel instrumentation enabled from T4 (Day 1). Turn latency definition: user CLI input → agent response returned, **excluding** SQLite persist. Session save is overhead measurement (M2 scope, not M1 baseline).

**Rationale:** Observability is non-negotiable. M1 establishes baseline *with* OTel; M2 measures OTel overhead *relative* to that baseline. Prevents late-stage surprises. SQLite persist latency is measured separately in T8 (R5 load test).

**Implementation:** `HermesTelemetry.cs` defines `ActivitySource` and span names (`hermes.chat.request`, `hermes.provider.call`, `hermes.session.save`). `HermesChatService` emits spans for chat request (parent) and provider call (child). `Program.cs` registers OpenTelemetry with OTLP exporter (`http://localhost:4317`). T4 acceptance: OTLP collector displays trace hierarchy, P95 turn latency < 100 ms for local Ollama (50 requests). T10 baseline: results committed to `docs/benchmarks/m1-perf-baseline.md`.

---

### M1-007 — BLOCKER 6: Load Test Definition

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Load test spec locked, Dallas executes

**Decision:** Concurrency model is sequential inserts + concurrent reads (realistic: users create sessions over time, queries frequent on recent sessions). Metrics: P95 insert ≤ 50 ms, P95 query ≤ 20 ms (confirmed). No P99/P99.9 targets in M1.

**Rationale:** Sequential inserts match real usage; tests SQLite write contention under realistic conditions. Concurrent queries test index effectiveness and read scaling. 50 ms insert and 20 ms query budgets keep latency below perceptible delay threshold.

**Implementation:** `tests/Hermes.LoadTests/SessionLoadTest.cs` runs 1,000 sequential inserts, measures P95 latency, runs 10 parallel readers for 100 queries each, measures P95 query latency. T8 execution (Dallas): run test on CI Linux runner; record results to `docs/benchmarks/m1-session-load.md`. Target: P95 insert ≤ 50 ms, P95 query ≤ 20 ms (R5-A PASSED).

---

### M1-008 — BLOCKER 7: YAML Parser Tests & R5 Scope

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Parser tests locked, Dallas writes specs

**Decision:** M1 R5 includes YAML malformed input tests (5 malformed + 1 valid case). Parser brittleness is blocking risk; lock down parser behavior before any code depends on it. Not full skills M1; just parser input validation.

**Rationale:** Skills aren't M1 deliverables, but `SkillParser` class is. If parser silently accepts invalid inputs, M2+ skill logic inherits corrupt state. Lock it down now.

**Implementation:** `SkillParser.cs` validates non-empty YAML, deserializes to `SkillDefinition`, validates required fields (`name`, `description`, `type`). Throws `SkillParseException` on any validation failure. `SkillParserTests.cs` includes 5 parameterized malformed tests and 1 valid case. Fixtures in `tests/Hermes.Core.Tests/Skills/fixtures/` (missing-name, missing-description, missing-type, empty, null-fields). T9 execution (Dallas): write and execute 6 tests; all pass before T10. R5-B acceptance: all 5 malformed inputs rejected.

---

### M1-009 — Cross-Cutting: Provider Abstraction Map (R1 Validation)

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — R1 validation framework locked

**Decision:** R1 validation confirms architecture mapping (no concept mismatches). Ripley reviews abstraction map Week 1. GREEN required before any Hermes-specific code is written.

**Rationale:** Architectural drift early is a sunk cost. R1 gates downstream confidence.

**Implementation:** Abstraction map documents:
| Hermes Concept | Abstraction | Implementation File |
|---|---|---|
| Profile | DI-resolved agent config | `Hermes.Host/Program.cs` |
| Session | `ISessionStore` + SQLite | `Hermes.Core/Session/SessionStore.cs` |
| Tool Descriptor | MAF function tool | `Hermes.Core/Providers/ChatClientFactory.cs` |
| Provider Routing | `IChatClient` factory | `ChatClientFactory.CreateChatClient()` |

Ripley validates map; zero mismatches found. PR merged with explicit "R1 GREEN" sign-off. Date: Week 1, Day 4.

---

### M1-010 — Go/No-Go Checkpoints

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — Both checkpoints must be GREEN

**Decision:** Both R1 and R5 checkpoints must be GREEN before M1 marked APPROVED. Freeze if either RED — max 3-day remediation sprint.

**Rationale:** Confidence gates. R1 confirms architecture; R5 confirms scale.

**R1 GREEN (Week 1, Day 4):**
- `R1IntegrationDrift.cs` passes: real response, session persisted, tool invocation works
- Ripley reviews abstraction map; zero concept mismatches found
- PR merged with explicit "R1 GREEN" sign-off from Ripley

**R5 GREEN (Week 2, Day 9):**
- **R5-A:** SQLite P95 insert ≤ 50 ms, P95 query ≤ 20 ms at 1,000 sessions
- **R5-B:** All 5 malformed YAML inputs rejected with `SkillParseException`
- Results committed to `docs/benchmarks/m1-session-load.md`
- Ripley reviews; no fallback plan needed

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
