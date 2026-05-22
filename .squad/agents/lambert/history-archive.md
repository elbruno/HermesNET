# Lambert Archive — M1 & M2 Quality Assurance Summary — 2026-05-22

## M1 Completed (Foundation Phase)

### M1 Test Strategy Locked
- **Framework:** xUnit (no MSTest/NUnit) + Coverlet branch coverage + FluentAssertions
- **Coverage Gate:** 80% branch (hard gate for M1 exit)
- **Quality Gates:** 6 measurable gates (coverage, build cleanliness, security, R1 integration, OTel baseline, R5 load test)
- **CI/CD:** GitHub Actions on every PR, multi-OS matrix (Linux/macOS/Windows)

### M1 Exit Status
- ✅ **Gate 1:** 87.5% branch coverage on Hermes.Core (exceeds 80%)
- ✅ **Gate 2:** Zero build warnings (TreatWarningsAsErrors=true)
- ✅ **Gate 3:** Zero CVEs (security audit passed)
- ✅ **Gate 4:** R1 integration test suite passes (6/6 tests)
- ✅ **Gate 5:** OTel baseline P95 = 48ms (< 100ms target)
- ✅ **Gate 6:** R5 load test (1,000 sessions, P95 latencies under budget)
- ✅ **50/50 tests passing** (46 unit + 1 load + 3 integration)

## M2 In Progress

### M2-004, M2-005, M2-006 — Test Strategy + T14 & T15 Verdicts
- **Coverage Target Raised:** 85% per-module branch (Profiles, Sessions, Skills, Memory)
- **R2 Gate:** Zero-tolerance cross-profile contamination (hard gate, no partial credit)
- **Error Contract:** All M2 stores throw KeyNotFoundException on missing IDs (extends M1-011)
- **Blocked Test Pattern:** `Skip = "Blocked:..."` scaffolds compile immediately; removal signals implementation done

### Test Results
- **T14 (SkillRegistry):** 18/18 tests activated and passing
- **T15 (Memory + R2):** 34/34 tests activated and passing
  - R2 isolation gate ✅ PASSED (7 supporting tests all green)
  - 16 unit memory + 15 memory isolation unit + 3 integration
- **M1 Regression:** 92/92 baseline tests still passing (0 regressions)
- **Total Active:** 144 tests, all green

### Pre-Existing Bug Flagged
- **Transaction Lifecycle Issue:** DeleteProfile/DeleteSession throw InvalidOperationException instead of KeyNotFoundException
- **Impact:** Hard blocker for M2 Go/No-Go
- **Action Required:** Dallas must fix before M2 Week 1 exit

### Orphaned Scaffold Alert
- **ProfileManagerTests.cs:** 14 tests reference non-existent IProfileManager interface
- **Action:** Delete (T13 coverage adequate via ProfileAndSessionServiceTests) or rewrite against IProfileService
- **Requires:** Coordinator clarification

## Learnings
- **Per-module coverage gates** (85%) catch under-covered areas that overall aggregate can mask
- **Blocked tests as living contracts** pattern validates: compile-time safety + behavioral documentation + automatic activation
- **R2 isolation requires bidirectional testing:** Assert both A→B and B→A contamination paths
- **File-based tests need unique GUID workspaces:** Prevents cross-contamination under parallel CI execution
