# M2 Kickoff — MVP Runtime

**Date:** 2026-05-22  
**Authority:** Ripley  
**Status:** ✅ APPROVED — M1 complete; M2 cleared to start

---

## Prerequisites Confirmed

- M1 Go/No-Go: ✅ APPROVED (see `.squad/decisions.md` M1-012)
- R1 GREEN: IChatClient abstraction validated
- R5 GREEN: Session store load tested; YAML parser hardened
- All 6 M1 quality gates PASS

---

## M2 Goal

First public MVP. All core Hermes concepts implemented:
- Profile and session management (CRUD, multi-profile)
- Markdown skill parser and registry (YAML front matter)
- Curated memory loader (`MEMORY.md` + `USER.md`, per-profile scoped)
- Native tool registry (read-only, safe tool categories)
- REST API with SSE streaming
- CLI: `hermes chat`, `hermes session`, `hermes skill`, `hermes memory`
- OTel on all new paths

---

## M2 Task Breakdown (12 tasks, 3 weeks)

### Week 1 — Core Runtime + R2/R4 Risk Checkpoints

| Task | Description | Owner | Duration |
|------|-------------|-------|----------|
| T13 | Profile CRUD + multi-profile switching | Dallas | 2 days |
| T14 | Session management (CRUD, listing, switching) | Dallas | 1.5 days |
| T15 | R2 risk spike: curated memory loader (`MEMORY.md`/`USER.md`); profile scoping | Parker | 1.5 days |
| T16 | R4 risk re-baseline: OTel overhead measurement vs M1 baseline | Parker | 0.5 day |

**Week 1 exit gate:** R2 GREEN (no cross-profile memory contamination) + R4 GREEN (OTel overhead ≤ 20% vs M1 P95=51ms)

### Week 2 — Skills, Tools, REST API

| Task | Description | Owner | Duration |
|------|-------------|-------|----------|
| T17 | Markdown skill parser full implementation (YAML front matter + registry) | Dallas | 2 days |
| T18 | Native tool registry (read-only file/system tools; safe categories) | Dallas | 1.5 days |
| T19 | REST API with SSE streaming (`/api/chat`, `/api/sessions`) | Dallas | 2 days |
| T20 | Lambert: 85% unit test coverage on profiles, sessions, skills, memory | Lambert | ongoing |

**Week 2 exit gate:** All four core modules at ≥ 85% branch coverage

### Week 3 — CLI, Integration, OTel Coverage, Go/No-Go

| Task | Description | Owner | Duration |
|------|-------------|-------|----------|
| T21 | CLI: `hermes session`, `hermes skill list`, `hermes memory show` | Dallas | 1.5 days |
| T22 | REST API contract tests (Postman / integration tests) | Dallas + Lambert | 1 day |
| T23 | OTel coverage audit: ≥ 90% of new paths emit traces | Parker | 1 day |
| T24 | M2 Go/No-Go: Ripley reviews MVP readiness | Ripley | 1 day |

---

## M2 Quality Gates

| Gate | Target | Hard? | Owner |
|------|--------|-------|-------|
| Unit test coverage (profiles, sessions, skills, memory) | ≥ 85% branch each module | ✅ Yes | Lambert |
| OTel coverage (new flows) | ≥ 90% new code paths emit traces | ✅ Yes | Parker |
| Latency regression vs M1 baseline | < 20% overhead vs M1 P95=51ms (< 61ms P95) | ✅ Yes | Parker |
| CLI smoke tests | All 4 commands succeed without error | ✅ Yes | Lambert |
| REST API contract tests | All endpoints tested; OpenAPI spec generated | ✅ Yes | Dallas |
| Markdown skill validation | Malformed YAML rejected; valid skills execute | ✅ Yes | Dallas |
| Curated memory profile scoping | No cross-profile contamination | ✅ Yes | Parker |
| M1 regression | 100% of M1 exit criteria still pass | ✅ Yes | Lambert |

---

## M2 Risk Checkpoints

### R2 (Week 1, Day 3) — Curated Memory Semantics
- **Owner:** Parker
- **Validation:** 2-profile integration test; profile A cannot read profile B's `MEMORY.md`
- **Go trigger:** Parker documents canonical scoping spec; zero contamination
- **No-Go action:** Parker redefines scoping spec + reimplements before memory layer merges

### R4 (Week 1, Day 4) — OTel Latency Overhead
- **Owner:** Parker
- **Validation:** Benchmark re-run with all new spans active; P95 ≤ 61ms (< 20% over 51ms)
- **Go trigger:** Delta documented in `docs/benchmarks/m2-perf-baseline.md`
- **No-Go action:** Adaptive sampling strategy; reduce span density on hot paths

---

## Schedule

| Date | Milestone |
|------|-----------|
| 2026-05-26 | M2 Day 1 — Dallas starts T13, Parker starts T15 |
| 2026-05-28 | R2 + R4 checkpoint (Week 1, Day 3-4) |
| 2026-06-02 | Week 2 complete — Skills, Tools, REST API |
| 2026-06-09 | T24 — M2 Go/No-Go review by Ripley |

---

## Team Assignments

- **Dallas:** T13, T14, T17, T18, T19, T21, T22 (backend core + API + CLI)
- **Parker:** T15, T16, T23 (memory + OTel + perf)
- **Lambert:** T20, T22 (test strategy + coverage + contract tests)
- **Ripley:** T24 (Go/No-Go), architecture reviews on T15+T17 (curated memory + skill parser)
- **Ash:** Security baseline review on REST API + tool registry (M3 prep)

---

## M2 Exit Criteria (Definition of Done)

- ✅ Profile and session CRUD works end-to-end
- ✅ Markdown skill loads, validates, and executes
- ✅ Curated memory loads and scopes correctly per profile; no cross-profile contamination
- ✅ Native tools execute with correct sandboxing
- ✅ REST API serves chat with SSE streaming
- ✅ CLI first-run experience works from `dotnet run`
- ✅ R2 and R4 risk checkpoints PASSED
- ✅ All quality gates above are GREEN
- ✅ Zero M1 regressions

---

*Ripley — M2 Kickoff, 2026-05-22*
