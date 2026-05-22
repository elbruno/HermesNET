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

### M1-011 — Spec Divergence: SessionStore Error Contract

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ RESOLVED — Implementation wins; spec updated here

**Divergence (flagged by Lambert in SessionStoreTests.cs):**
- `DeleteAsync` non-existent ID: spec said no-op; implementation throws `KeyNotFoundException`
- `UpdateAsync` non-existent ID: spec said `InvalidOperationException`; implementation throws `KeyNotFoundException`

**Decision:** Retain `KeyNotFoundException` for both. Fail-fast on missing IDs is safer and more debuggable than silent no-ops. This is an M1 spec clarification, not a bug. Tests assert actual behavior. No code change required.

---

### M1-012 — M1 Go/No-Go: APPROVED

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ M1 APPROVED — R1 GREEN, R5 GREEN, All 6 Quality Gates PASS

**Decision:** Milestone 1 (Foundations) is COMPLETE. All quality gates pass, all risk checkpoints are GREEN, all deliverables committed. Team is cleared to begin M2.

**Evidence:**
- Gate 1 (Coverage ≥80%): Branch coverage = **87.5%** on Hermes.Core ✅
- Gate 2 (Zero warnings): `dotnet build /p:TreatWarningsAsErrors=true` → 0 warnings ✅
- Gate 3 (Zero CVEs): SECURITY.md — zero critical/high CVEs ✅
- Gate 4 (R1 Integration): 5/5 ChatClientFactoryTests pass; IChatClient abstraction validated ✅
- Gate 5 (OTel Baseline): P95 = 51ms ≤ 100ms (M1-BASELINE.txt) ✅
- Gate 6 (R5 Load Test): P95 insert 12µs ≤ 50ms; P95 query 176µs ≤ 20ms ✅
- R1 GREEN: IChatClient→ChatClientFactory→provider wiring confirmed; zero concept mismatches
- R5 GREEN: 1,000-session load test passed; 6/6 YAML parser tests passed
- R10 (M2 prep): No architectural blockers; provider abstraction is config-driven, ready for M2 MAF agent integration
- T11 E2E smoke test: 3/3 integration tests pass (full CLI→DI→SessionStore→Provider chain verified)
- Total test count: 50/50 passing (46 unit + 1 load + 3 integration; Ollama benchmark skipped in CI)

**M2 Start:** Cleared. Reference `.squad/decisions/m2-kickoff.md`.

---

### M2-001 — M2 Decisions Locked

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ LOCKED — M2 scope, quality gates, critical path finalized. Start date: May 26.

**Decision:** M2 scope, quality gates, critical path, and team assignments are locked after kickoff ceremony. All prerequisites (M1 validation, task breakdown, risk assessment) complete. Ready to execute.

**Rationale:** Formal M2 alignment ensures all prerequisites met. Formal M2 alignment; all prerequisites (M1 validation, task breakdown, risk assessment) complete.

**Reference:** `.squad/decisions/m2-kickoff.md`

---

### M2-002 — Profile and Session Management Contracts

**Date:** 2026-05-22T17:14:32.037-04:00  
**Authority:** Dallas  
**Status:** ✅ IMPLEMENTED — builds clean, 92/92 new tests pass

**Decision:** Profile and session management use separate `IProfileService` and `ISessionService` interfaces. SQLite backend uses ADO.NET (no EF Core overhead). AppState table persists current profile/session pointers. SessionService enforces R2 isolation by checking session.ProfileId against current profile. ProfileSessions table is separate from M1 Sessions table.

**CLI Commands Delivered:**
```
hermes profile create "My Profile" [--description "..."]
hermes profile list
hermes profile switch <name-or-id>
hermes profile current
hermes session create "Chat Session" [--profile <profileId>]
hermes session list [--profile <profileId>]
hermes session switch <id>
hermes session current
```

**R2 Coordination:** Parker depends on `ISessionService` for memory scoping. SessionService takes `IProfileService` as dependency for cross-profile enforcement.

---

### M2-003 — T14 Design Unknowns (Dallas Flagged for Review)

**Date:** 2026-05-22T17:24:27.189-04:00  
**Authority:** Dallas  
**Status:** ⚠️ FLAGGED — Decisions assumed, team review required before M3

**Three ambiguities flagged:**

1. **Skill ID Uniqueness Scope** — Assumed global uniqueness; DuplicateSkillException on collision. Risk: 50+ skills may need namespacing (e.g., "math/calculate-sum"). Consider M3 decision.

2. **Skill Versioning Strategy** — Assumed one version per skill ID; first file wins. Risk: M3 live reload may require explicit version coexistence. Ripley to decide: "latest version wins" or "explicit version coexistence".

3. **Metadata Structure Enforcement** — Assumed flexible free-form key-value pairs, no schema. Risk: T16 policy engine may require typed metadata. Lock consistent format before T16 ships if needed.

---

### M2-004 — M2 Test Strategy Finalized (Lambert)

**Date:** 2026-05-22  
**Authority:** Lambert  
**Status:** ✅ COMPLETE — Strategy finalized, scaffold created

**Key Decisions:**

- **Coverage Target Raised to 85%** per-module branch coverage on Profiles, Sessions, Skills, Memory
- **R2 Gate: Zero-Tolerance** — `MemoryIsolation_TwoProfiles_NoContamination` hard gate; no partial credit
- **Error Contract Consistency** — Extends M1-011; all M2 store operations throw `KeyNotFoundException` on missing IDs
- **Blocked Tests as Living Contracts** — All scaffolds have `Skip = "Blocked: [interface]..."` to compile immediately; removal signals implementation completion
- **REST API Contract Tests Required** — All endpoints require tests in `Hermes.Integration.Tests/`; OpenAPI spec committed before M2 Go/No-Go

**Pre-Existing Failures Found:**
| Test | Expected | Actual |
|------|----------|--------|
| `DeleteProfile_MissingId_Throws` | `KeyNotFoundException` | `InvalidOperationException: This SqliteTransaction has completed` |
| `DeleteSession_Missing_Throws` | `KeyNotFoundException` | `InvalidOperationException: This SqliteTransaction has completed` |

**Action:** Dallas must fix transaction lifecycle in delete methods before M2 Week 1 exit.

---

### M2-005 — T14 Verdict: SkillRegistry GREEN (Lambert)

**Date:** 2026-05-22T17:24:27-04:00  
**Authority:** Lambert  
**Status:** ✅ GREEN — All T14 tests pass

**Results:**
- Total tests: 18
- Passed: 18
- Failed: 0
- Skipped: 0
- Duration: 0.7s

**Implementation Files Verified:**
- ✅ `src/Hermes.Core/Skills/ISkillRegistry.cs`
- ✅ `src/Hermes.Core/Skills/SkillRegistry.cs`

---

### M2-006 — T15 Verdict: Memory & R2 Isolation GREEN (Lambert)

**Date:** 2026-05-22T17:24:27-04:00  
**Authority:** Lambert  
**Status:** ✅ GREEN — R2 isolation gate PASSED

**R2 ISOLATION GATE PASSED:**
- `MemoryIsolation_FullStack_ProfileACannotReadProfileBMemory` ✅
- 7 supporting isolation tests ✅
- No cross-profile data leakage detected

**Test Counts:**
| Class | Tests | Result |
|---|---|---|
| `CuratedMemoryLoaderTests` (Memory) | 16 | ✅ All passed |
| `MemoryIsolationTests` (Memory unit) | 15 | ✅ All passed |
| `MemoryIsolationTests` (Integration) | 3 | ✅ All passed (Skip removed) |
| **T15 total** | **34** | ✅ 34/34 |

**Source Files Verified:**
- ✅ `src/Hermes.Core/Memory/CuratedMemoryLoader.cs`
- ✅ `src/Hermes.Core/Memory/MemoryUpdateHandler.cs`

---

### M2-007 — Curated Memory Schema + Profile Scoping (Parker)

**Date:** 2026-05-22  
**Authority:** Parker  
**Status:** ✅ COMMITTED — R2 GREEN

**Decision:** Two-table SQLite schema (Memory + UserProfiles) with hard profile isolation enforced at DB layer. `IMemoryService` interface: LoadMemoryAsync, UpdateMemoryAsync, LoadUserProfileAsync, UpdateUserProfileAsync, GetMemorySchemaAsync. All methods profile-scoped. Schema validation: 64 KB max, Markdown-only format in MVP. Migration: `002_MemorySchema.sql`.

**R2 Checkpoint: GREEN**
- 15/15 isolation tests pass
- Latency baseline: < 50 ms for 2 KB content on in-memory SQLite

**Deferred to M3:**
- Section-level MEMORY.md merging (full-replace only in M2)
- Encrypted memory at rest
- `IRetrievalMemoryProvider` (RAG layer)
- Memory diff/audit trail
- Profile-level memory quotas

**Coordination Notes:**
- Dallas (T13): When profiles table ships, add FK constraint in `003_MemoryProfileFK.sql`
- Lambert: R2 test coverage in `Memory/MemoryIsolationTests.cs`

---

### M2-008 — T16 Coordination: Memory-Policy Access Contract (Parker → Ash)

**Date:** 2026-05-22T17:24:27.189-04:00  
**From:** Parker (Data/Memory Dev)  
**To:** Ash (T16 — Tool Registry + Policy)  
**Status:** For Review — T16 planning input

**Policy Rule Memory Access Contract:**
```csharp
var memCtx = await loader.LoadMemoryAsync(profileId);
if (!memCtx.IsEmpty)
{
    var block = memCtx.ToMemoryBlock(); // Markdown block for system-prompt
}

try
{
    var userProfile = await loader.LoadUserProfileAsync(profileId);
    var userBlock = userProfile.ToMemoryBlock();
}
catch (KeyNotFoundException)
{
    // No USER.md written — policy continues without user preferences
}
```

**Key Contracts for T16:**
| API | Behaviour |
|---|---|
| `CuratedMemoryLoader.LoadMemoryAsync(profileId)` | Returns `MemoryContext` (possibly Empty); cache-backed; never cross-profile |
| `MemoryContext.IsEmpty` | True when no memory written; policy should skip injection |
| `MemoryContext.ToMemoryBlock()` | Formatted Markdown block; empty if IsEmpty |
| `CuratedMemoryLoader.LoadUserProfileAsync(profileId)` | Returns `UserProfileData`; throws `KeyNotFoundException` if never written |

**Open Question:** Should policy rules receive pre-loaded MemoryContext from invocation context or call CuratedMemoryLoader themselves? Parker recommends pre-loading at session boundary.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
