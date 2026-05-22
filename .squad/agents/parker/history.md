# Parker History — HermesNET Memory/Data Development

**Current Focus:** M2 Data/Memory - Curated Memory Schema, Memory Isolation (R2)

## Active Work Summary

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



