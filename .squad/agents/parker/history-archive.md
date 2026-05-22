# Parker Archive — M1 & M2 History Summary — 2026-05-22

## M1 Completed (Foundation Phase)

### M1 Execution Summary
- **T4:** OTel baseline instrumentation (HermesTelemetry.cs, spans emitted from Day 1)
- **T10:** Performance baseline harness (Stopwatch xUnit benchmark; P95 baseline established)
- **T17:** Curated memory model design + IMemoryService interface

### Key Measurements Locked
- **Turn Latency Definition:** User CLI input → response returned (excluding SQLite persist)
- **OTel Spans:** hermes.chat.turn (parent) → hermes.provider.call (child)
- **P95 Baseline:** 55ms with OTel fully enabled (simulated); 48ms actual run (well under 100ms target)
- **M2 Regression Gate:** < 20% latency overhead vs. M1 baseline

### M1 Exit Status
- ✅ OTel baseline established (48ms P95)
- ✅ Span hierarchy confirmed correct (hermes.chat.turn parent → hermes.provider.call child)
- ✅ Baseline results committed to docs/benchmarks/m1-perf-baseline.md

## M2 In Progress

### M2-007 & M2-008 — Memory Schema + T16 Coordination
- **Deliverables:** IMemoryService + CuratedMemoryLoader + MemoryUpdateHandler
- **Schema:** Two-table SQLite (Memory + UserProfiles) with hard profile isolation at DB layer
- **R2 Checkpoint:** ✅ GREEN — 15/15 isolation tests pass; no cross-profile leakage
- **Test Status:** 16/16 CuratedMemoryLoaderTests passing
- **Status:** ✅ COMPLETE
- **T16 Coordination:** Memory API ready for policy rule integration (pre-load at session boundary recommended)

### Risk Flags
- **No FK to AgentProfiles in M2:** profileId soft-validated. Dallas to add 003_MemoryProfileFK.sql after T13 ships.
- **Cache Scope:** Single-session, single-handler instance. Multi-agent invalidation deferred to M3.
- **Empty vs Null:** LoadMemoryAsync returns MemoryContext.Empty for profiles with no memory (never null).

## Learnings
- Profile isolation at database layer (unique indexes + mandatory WHERE clauses) is more robust than app-layer checks
- Per-profile cache with SemaphoreSlim serialization prevents concurrent write contention
- Content validation firewall at application layer (MemoryUpdateHandler) catches binary/corrupted content before DB
