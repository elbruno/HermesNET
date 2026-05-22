# Project Context

- **Owner:** Bruno Capuano
- **Project:** HermesNET
- **Stack:** .NET 10, ASP.NET Core, Microsoft Agent Framework, Microsoft.Extensions.AI, OpenTelemetry, SQLite/PostgreSQL
- **Description:** Hermes-inspired .NET runtime for profiles, sessions, skills, memory, tool policy, and observability.
- **Created:** 2026-05-22

## Learnings

Team initialized for PRD-first planning. No implementation started.

## Learnings — 2026-05-22

### PRD Plan Finalization (Milestone 1 Prep)

**Key decisions made:**
- Integrated three improvements into `docs/research/plan.md` implementation section: Critical Path Analysis, Risk Closure Plan, and per-milestone Quality Gates/Exit Criteria/Risk Validations.
- Risk validation happens at the **START** of each milestone (early detection protocol). A failed checkpoint blocks the milestone from proceeding until resolved.
- **Top 5 risks mapped to checkpoints:** R1 (MAF integration drift → Foundations), R2 (memory semantics → MVP Runtime), R3 (policy engine complexity → Safe Tools), R4 (OTel latency overhead → MVP Runtime), R5 (SQLite scale → Foundations Week 2).
- Team assignments locked: Dallas (backend/session store lead), Parker (OTel/memory lead), Ash (security/policy lead), Lambert (test strategy), Ripley (architecture + go/no-go authority on all milestones).
- Gantt updated to show parallel workstreams — Dallas + Parker + Ash working concurrently within milestones; critical path remains Session → Skills → Memory → Policy.
- Go/No-Go decision on each milestone: Ripley verifies all exit criteria and quality gates; fail → max 3-5 day fix sprint, then re-validate.
- Scope boundary held: Incubation (M6) features are explicitly non-production; Ripley pulls any incubation work that threatens M1–M5 stability.

**Plan structure (new sections added to plan.md):**
- Critical Path Analysis table (milestone × blocker × parallel × team)
- Risk Closure Plan table (R1–R5 × checkpoint × milestone × owner)
- Six milestone sections, each with: Risk Validations, Deliverables, Quality Gates, Exit Criteria, Go/No-Go decision
- Updated Gantt chart showing parallel bars by team member

**Next action:** Ripley to produce Milestone 1 detailed task breakdown (week-by-week) for team kickoff.

---

## Learnings — 2026-05-22 (M1 Breakdown)

### M1 Detailed Task Breakdown Issued

**Key decisions made:**
- 12 tasks across 2 weeks. Week 1: T1–T5 (solution setup, IChatClient + MAF wiring, OTel, security audit). Week 2: T6–T12 (session store, CLI, load test, YAML parser, perf baseline, E2E smoke, Go/No-Go review).
- **R1 checkpoint (Week 1):** 4 integration assertions in `R1IntegrationDrift.cs` — real chat response, session persisted, tool invocation via 3 tool types, abstraction map reviewed by Ripley. Zero concept mismatches required for GREEN.
- **R5 checkpoint (Week 2):** SQLite P95 insert ≤ 50 ms at 1,000 sessions, P95 query ≤ 20 ms, AND 5 malformed YAML inputs each throw typed `SkillParseException`. Both sub-parts required for GREEN.
- **Fallback paths defined:** R1 fail → architecture session + redesign (max 1 day); SQLite fail → WAL+index optimization, then PostgreSQL migration decision; YAML fail → harden parser before any Skills code merges.
- **Go/No-Go authority:** Ripley. Signs off in `.squad/decisions.md` with `"M1 APPROVED — R1 GREEN, R5 GREEN"`. No partial pass accepted.
- T1 (solution structure) is day-zero — nothing starts without it. Dallas owns T1 and must complete it Day 1.

**Documents produced:**
- `.squad/decisions/m1-task-breakdown.md` — full task table (Week 1 + Week 2), both risk checkpoint definitions, dependency map
- `.squad/decisions.md` — M1-001 decision entry added

**Next action:** Dallas to start T1 (Create solution structure) on Day 1. Ripley co-owns T2 review (R1 spike).

---

## M1 Completion — T11 + T12 Go/No-Go — 2026-05-22

### M1 APPROVED ✅

**All 6 quality gates PASS. R1 GREEN. R5 GREEN. 50/50 tests passing.**

**T11 — E2E Smoke Test:**
- Created `tests/Hermes.Integration.Tests/E2ESmokeTest.cs` (3 tests)
- Full CLI→DI→SessionStore→Provider chain verified with mock IChatClient
- OTel spans confirmed: `hermes.chat.turn`, `hermes.provider.call`, `hermes.session.persist` all captured via ActivityListener
- Session persisted and retrievable from in-memory SQLite

**T12 — Go/No-Go Decision:** M1 COMPLETE ✅

| Gate | Result | Evidence |
|------|--------|----------|
| Coverage ≥80% | ✅ 87.5% branch | Coverlet (Hermes.Core) |
| Zero warnings | ✅ 0 warnings | `dotnet build /p:TreatWarningsAsErrors=true` |
| Zero CVEs | ✅ 0 critical/high | SECURITY.md + Dependabot |
| R1 Integration | ✅ 5/5 pass | ChatClientFactoryTests.cs |
| OTel Baseline | ✅ P95=51ms ≤ 100ms | M1-BASELINE.txt |
| R5 Load Test | ✅ P95 insert 12µs, query 176µs | m1-session-load.md |

**Spec divergence resolved (M1-011):** `SessionStore.DeleteAsync` and `UpdateAsync` throw `KeyNotFoundException` for missing IDs (fail-fast wins; spec updated).

**M2 cleared.** Reference `.squad/decisions/m2-kickoff.md`.

---

## M1 Blocker Resolution — 2026-05-22

### All 7 Critical Blockers RESOLVED

Team (Dallas, Parker, Ash, Lambert) identified 7 architectural blockers. Ripley made synchronous decisions on all 7. Full decision document: `.squad/decisions/inbox/ripley-m1-blockers.md`

**Key decisions:**

1. **Solution Structure:** Ripley scaffolds (Directory.Build.props, Directory.Packages.props, .sln). Dallas verifies build. NuGet versioning: centralized. SQLite schema: EF Core Code First + migrations.

2. **Provider Path:** Abstraction = `Microsoft.Extensions.AI.IChatClient`. Runtime swapping = config-driven (appsettings.json). M1 provider = Ollama only (local, no creds).

3. **Test Framework:** xUnit (Microsoft standard) + Coverlet coverage + GitHub Actions CI. M1 target: 80% branch coverage on `Hermes.Core`.

4. **CLI Framework:** System.CommandLine (official Microsoft). Single command: `hermes chat --profile default --message "..."`.

5. **OTel Baseline:** Enable OTel from Day 1 (T4). Measure turn latency (excl. SQLite persist) with OTel ON. M1 baseline: P95 ≤ 100 ms (local Ollama).

6. **Load Test:** Sequential inserts + concurrent reads. P95 insert ≤ 50 ms, P95 query ≤ 20 ms at 1,000 sessions (in-memory SQLite).

7. **YAML Parser Tests:** M1 includes 5 malformed input tests + 1 valid input test. `SkillParseException` required for all invalid inputs. Defensive prep for M2 skills.

**Architectural notes for M2–M6:**
- Provider swapping is config-driven only; no code changes needed.
- OTel spans use `hermes.*` prefix consistently.
- Session store is abstractable; can migrate SQLite→PostgreSQL without touching chat service.
- YAML parser is strict; no silent failures.
- Coverage ratchets: M1=80%, M2=85%, M3+=90%.

**Go/No-Go gates:**
- R1 GREEN (Week 1): `R1IntegrationDrift.cs` passes, Ripley reviews abstraction map, zero concept mismatches.
- R5 GREEN (Week 2): SQLite load test P95 within budget + all 5 YAML parser tests pass.

**Status:** ✅ Dallas unblocked to start T1 immediately (Day 1, 2026-05-23).

