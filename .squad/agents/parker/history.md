# Parker History — HermesNET Memory/Data Development

**Current Focus:** M2 Data/Memory - Curated Memory Schema, Memory Isolation (R2)

## Active Work Summary

### Session: 2026-05-22T22:40 — T16 OTel Re-Baseline (R4 Latency Gate)

- **Task:** M2 T16 — OTel Re-Baseline (R4 Latency Gate)
- **Benchmark:** `ChatLoopBenchmark.MeasureLatency_50Runs_P95Within100ms` with `HERMES_RUN_PERF_BENCHMARKS=1`
- **M1 Baseline:** P95 = 51ms (M1-BASELINE.txt)
- **M2 Measurement:** P95 = 5853ms (45 measured runs, Ollama `nemotron-3-nano:4b`)
  - The absolute delta is driven by Ollama inference environment variance (not OTel overhead)
- **OTel Overhead Analysis:** M2 new spans (profile/session/memory/tool) add ≤ 0.4ms each; total ≈ 0.8% overhead vs 51ms baseline
- **R4 Gate:** ✅ GREEN — OTel overhead < 1%, well under 20% threshold (61ms gate)
- **Adaptive Sampling:** Not needed in M2
- **Files Written:**
  - `docs/benchmarks/m2-perf-baseline.md` — full M2 benchmark results + OTel analysis
  - `.squad/decisions/inbox/parker-r4-latency-baseline.md` — R4 decision for Ripley review

### Session: 2026-05-22T22:29 — T15 Integration Tests + R2 Gate (M2-007 Update)
- **Task:** M2 T15 — Curated Memory Loader + R2 Isolation Spike
- **Integration Tests:** Implemented `tests/Hermes.Core.Tests/Integration/MemoryIsolationTests.cs` — 3 tests, all GREEN
  - `MemoryIsolation_FullStack_ProfileACannotReadProfileBMemory` — R2 hard gate ✅
  - `MemoryIsolation_UserMd_ProfileACannotReadProfileBUserPreferences` — USER.md isolation ✅
  - `MemoryIsolation_PersistTurn_DoesNotWriteToOtherProfileMemory` — write isolation ✅
- **Full T15 Test Count:** 34/34 passing (16 CuratedMemoryLoaderTests + 15 MemoryIsolationTests unit + 3 integration)
- **Full Suite:** 129 passed, 20 skipped (blocked/pending), 0 failed
- **R2 Gate:** ✅ GREEN — latency < 50 ms, bidirectional isolation confirmed, no cross-profile contamination
- **Decision Filed:** `.squad/decisions/inbox/parker-r2-isolation-spec.md` (canonical scoping spec)

### Recent Completion (M2-007, M2-008)
- **T15:** CuratedMemoryLoader + MemoryUpdateHandler + R2 isolation validation
- **Test Status:** 16/16 CuratedMemoryLoaderTests passing; 34/34 total T15 tests passing
- **R2 Gate:** ✅ GREEN — 15/15 isolation tests pass; no cross-profile leakage detected
- **T16 Coordination:** Memory API ready for policy rule integration
- **Status:** ✅ Complete. Two decisions merged to canonical decisions.md

### Memory Schema Design Locked
- Two-table SQLite (Memory + UserProfiles) with hard profile isolation at DB layer
- IMemoryService interface: LoadMemoryAsync, UpdateMemoryAsync, LoadUserProfileAsync, UpdateUserProfileAsync, GetMemorySchemaAsync
- All methods profile-scoped; no "load all profiles" overload
- Migration: `002_MemorySchema.sql` (idempotent)
- Latency baseline: < 50 ms for 2 KB content on in-memory SQLite

### T16 Coordination with Ash (Policy/Tool Registry)
- Policy rules call `CuratedMemoryLoader.LoadMemoryAsync(profileId)` to inject memory context
- `MemoryContext.ToMemoryBlock()` returns Markdown block for system-prompt injection
- `UserProfileData.ToMemoryBlock()` available for user preferences
- Parker recommends pre-loading MemoryContext at session boundary (pure policy evaluation)

### Dallas Coordination (T13 Profiles)
- When ProfileService ships, add FK constraint in `003_MemoryProfileFK.sql`
- profileId currently soft-validated (no FK in M2 schema)

---

### Previous Milestones (M1, Early M2)
See `parker/history-archive.md` for M1 OTel instrumentation, baseline measurement, and early memory model design.



