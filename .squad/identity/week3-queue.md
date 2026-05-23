# M2 Week 3 Work Queue (T21–T24)

**Prepared for immediate dispatch after T17–T19 complete**

---

## T21 — CLI Commands (Dallas)

**Objective:** Wire CLI commands for `hermes session`, `hermes skill`, `hermes memory` operations.

**Tasks:**
- `hermes session create <name>` — create new session
- `hermes session list [--profile <id>]` — list sessions (optionally filtered by profile)
- `hermes session switch <id>` — switch to session
- `hermes skill list` — show all registered skills
- `hermes skill show <name>` — show skill definition + metadata
- `hermes memory show [--profile <id>]` — display curated memory for profile

**Dependencies:** T13 (profiles), T14 (sessions), T17 (skills), T15 (memory)

**Duration:** 1.5 days

---

## T22 — REST API Contract Tests (Dallas + Lambert)

**Objective:** Integration tests validating all REST endpoints from acceptance criteria.

**Test Coverage:**
- Happy path: create, read, list, update, delete for profiles, sessions
- Error cases: 400/404/500 for invalid/missing resources
- SSE stream format: `token` and `done` events
- Concurrency: simultaneous requests to different sessions/profiles

**Format:** xUnit integration tests in `tests/Hermes.Integration.Tests/RestApiContractTests.cs`

**Dependencies:** T19 (REST API endpoints)

**Duration:** 1 day

---

## T23 — OTel Coverage Audit (Parker)

**Objective:** Verify ≥90% of new M2 code paths emit OTel spans.

**Audit Process:**
1. List all new code paths (T13–T19)
2. Verify span emission at entry/exit points
3. Check span attributes (profile ID, session ID, memory size, etc.)
4. Report coverage % in `docs/benchmarks/m2-otel-coverage.md`

**Target:** ≥90% coverage

**Duration:** 1 day

---

## T24 — M2 Go/No-Go Review (Ripley)

**Objective:** Final evaluation of M2 MVP readiness.

**Checklist:**
- All 8 quality gates GREEN:
  - [ ] Unit coverage ≥85% on Profiles, Sessions, Skills, Memory
  - [ ] OTel coverage ≥90% new paths
  - [ ] Latency < 20% overhead (R4 GREEN)
  - [ ] CLI smoke tests pass
  - [ ] REST API contract tests pass
  - [ ] Markdown skill validation works
  - [ ] Memory profile scoping (R2 GREEN)
  - [ ] M1 regression: 100% M1 tests still pass
- Risk checkpoints: R2 GREEN ✅, R4 GREEN ✅
- All 12 M2 tasks complete
- Decisions ledger locked (M2-001 through M2-009)

**Go outcome:** Mark M2 APPROVED, clear team for M3 + CLI/Docs phases

**Duration:** 1 day (review + approval)

---

## Critical Path

T17 → T18 → T19 → T21 (sequential Dallas work)
T22 (parallel with T21; Lambda validates)
T23 (parallel with T22; Parker validates OTel)
T24 (final gate; Ripley approves)

**Week 3 exit date:** June 9, 2026 (Go/No-Go)

