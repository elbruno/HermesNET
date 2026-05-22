# Dallas History — HermesNET Infrastructure/Profiles/Sessions

**Current Focus:** M2 Infrastructure - Profiles, Sessions, Skills

## Active Work Summary

### Recent Completion (M2-002, M2-003)
- **T13:** IProfileService + ISessionService + CLI commands (92/92 tests passing)
- **T14:** ISkillRegistry + MarkdownSkillParser (18/18 tests passing)
- **Status:** ✅ Complete. Three decisions merged to canonical decisions.md
- **Next:** Fix transaction lifecycle bug in DeleteProfile/DeleteSession before M2 Week 1 exit (hard blocker)

### Design Unknowns Flagged for M3 Planning
1. Skill ID uniqueness scope (global assumed; may need namespacing at 50+ skills)
2. Skill versioning strategy (one version per ID assumed; M3 decision needed)
3. Metadata structure enforcement (flexible key-value assumed; T16 may need schema)

### R2 Coordination with Parker
- SessionService validates profile ownership (SwitchSessionAsync enforces cross-profile check)
- Parker uses profileId as isolation key for all memory queries
- No direct SQLite access for memory operations (go through ISessionService)

---

### Previous Milestones (M1, Early M2)
See `dallas/history-archive.md` for M1 completion summary, session store implementation, and provider wiring details.


