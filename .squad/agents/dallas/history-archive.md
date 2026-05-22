# Dallas Archive — M1 & M2 History Summary — 2026-05-22

## M1 Completed (Foundation Phase)

### M1 Execution Summary
- **T1-T2:** Solution scaffold + Provider path wiring (IChatClient via ChatClientFactory)
- **T6-T7:** SQLite session store + CLI integration (hermes chat command)
- **T8-T9:** R5 load test (1,000 sessions, P95 latency ✅ PASS) + YAML skill parser
- **T14:** Markdown skill parser & ISkillRegistry (18/18 tests pass)
- **T13:** Profile and session management (92/92 tests pass)

### Key Decisions Locked
- Provider: Microsoft.Extensions.AI.IChatClient, config-driven Ollama
- Schema: EF Core Code First migrations for M1; raw ADO.NET for M2 session store
- Test: xUnit + Coverlet, 80% branch coverage (M1 exit: 87.5%)
- CLI: System.CommandLine for command routing

### M1 Exit Status
- ✅ R1 GREEN — Architecture validated (IChatClient → MAF → tool invocation E2E)
- ✅ R5 GREEN — SQLite load test passed (P95 insert 12µs, P95 query 87µs)
- ✅ 50/50 tests passing (46 unit + 1 load + 3 integration)
- ✅ Zero warnings, TreatWarningsAsErrors=true
- ✅ 87.5% branch coverage on Hermes.Core

## M2 In Progress

### M2-002 & M2-003 — Profile/Session Management + T14 Unknowns
- **Deliverables:** IProfileService, ISessionService interfaces + CLI commands
- **Test Status:** 92/92 new tests passing
- **Status:** ✅ COMPLETE
- **Unknowns Flagged for M3:** Skill ID uniqueness scope, versioning strategy, metadata schema

### Risk Flags
- **Transaction Lifecycle Bug:** DeleteProfile/DeleteSession throw InvalidOperationException instead of KeyNotFoundException. **Must fix before M2 Week 1 exit** (hard blocker for M2 Go/No-Go).
- **R2 Coordination:** SessionService validates profile ownership for R2 isolation. Parker must use profileId as isolation key.
- **M3 Planning:** Ripley to review T14 design unknowns (3 deferred decisions).

## Learnings
- Raw ADO.NET (Microsoft.Data.Sqlite) outperforms EF Core for simple session/profile store operations
- Transaction safety: use single catch path to avoid double-rollback bug pattern
- Schema additive approach: new ProfileSessions table additive to M1 Sessions table (no migration risk)
