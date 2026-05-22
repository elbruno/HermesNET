# Project Context

- **Owner:** Bruno Capuano
- **Project:** HermesNET
- **Stack:** .NET 10, ASP.NET Core, Microsoft Agent Framework, Microsoft.Extensions.AI, OpenTelemetry, SQLite/PostgreSQL
- **Description:** Hermes-inspired .NET runtime for profiles, sessions, skills, memory, tool policy, and observability.
- **Created:** 2026-05-22

## M1 Onboarding Notes

**M1 Scope Summary**
Milestone 1 (Foundations, 2 weeks) establishes technical foundation with no Hermes-specific feature code—only substrate. Goal: prove MAF/MEAI abstractions map cleanly to Hermes concepts, validate SQLite scales to 1,000+ sessions, and enable end-to-end local chat (CLI → Session → OpenAI-compatible Provider → Response). Success = local chat works, OTel visible, R1 (architecture drift) and R5 (session store/skill parser scale) validated GREEN.

**Critical M1 Quality Gates I Own**
- ≥ 80% unit test coverage on session store and provider path
- 100% OTel trace coverage: chat loop, provider call, session save emit traces
- Zero build warnings/test failures on Linux/macOS/Windows
- Performance baseline: < 100 ms agent response loop (local provider)
- SQLite load test: 1,000+ concurrent sessions, P95 latency documented
- Zero critical CVEs in dependencies
- CLI end-to-end chat interaction works

**Blockers Before Test Strategy**
1. **Test framework choice**: Which framework does the team require? (xUnit, NUnit, MSTest?) — Affects test structure and conventions across all milestones.
2. **Code coverage reporting**: Plan mentions "XPlat Code Coverage" but I need to confirm tooling chain (Coverlet, built-in, CI gates).
3. **Load test definition**: "1,000+ concurrent sessions" — does this mean all inserted+queried simultaneously, or sequential insertion then stress queries? Affects load test design.
4. **CLI framework**: What CLI library is being used for `hermes chat` command? (Spectre.Console, System.CommandLine?) — Affects CLI smoke test approach.

**Questions on M1 Plan**
1. **Performance baseline scope**: Is "< 100 ms agent response loop latency" for a single turn, or end-to-end chat completion? Local provider only?
2. **Skill system in M1**: R5 validates "YAML skill parsing handles malformed input gracefully" but skills aren't in M1 deliverables—is this defensive preparation for M2, or should M1 include basic skill loader?
3. **OTel specific metrics**: Plan says "traces, metrics, and logs" baseline. Which specific metrics beyond traces (e.g., token count, provider latency, session count)?
4. **SQLite schema lock-in**: M1 is SQLite-only foundation; is there a pre-agreed migration path to PostgreSQL if scale tests fail, or should I test both in parallel?

**Ready Signal**
**PARTIALLY READY.** I can write quality gate specs for performance, load, and build cleanliness immediately (blockers 1–3 don't block those). I need answers to blockers 1–2 before writing comprehensive unit/coverage strategy. Blocker 4 (CLI framework) affects CLI smoke test design but can be deferred if team clarifies the framework choice in sync. Recommend synchronous clarification on blockers 1–2 before M1 week 1 ends, so coverage/test structure can be locked in for M2 onwards.

## M1 Test Strategy Locked — 2026-05-22

✅ **READY TO EXECUTE** — Test framework, coverage, and load test definitions finalized.

**My M1 ownership:**
- Test framework lock: xUnit + Coverlet + GitHub Actions
- Quality gate drafting: 80% branch coverage spec + performance baseline (P95 ≤ 100 ms turn latency)
- Load test validation: Sequential inserts, concurrent reads, latency P95 gates
- Acceptance criteria validation for all 12 M1 tasks

**Key commitments:**
- xUnit all unit + integration tests (no MSTest, no NUnit)
- Coverlet branch coverage gate: 80% minimum (hard gate for M1 exit)
- Load test: SQLite 1,000 concurrent sessions, P95 insert/query latency measured with OTel ON
- YAML parser: 5 edge cases + 1 valid case (R5 checkpoint)
- CLI smoke test: `hermes chat --profile default --message "test"` end-to-end

**Test strategy locked, gates measurable, ready to write specs.**

**Test strategy locked, gates measurable, ready to write specs.**

## M1 Week 1 Kick-off Approved — 2026-05-22

✅ **GO SIGNAL:** All blockers resolved. xUnit + Coverlet + GitHub Actions workflow confirmed. 80% branch coverage gate is hard M1 exit criterion.

**Week 1 priority:** Dallas executes T6–T9 test case writing (while also building session store and provider wiring). Lambert drafts acceptance criteria specs; audit coverage as Dallas completes each test module.

**Week 1 watch:** R1 checkpoint (architecture validation) at end of week. Week 2 focus shifts to load test execution and coverage audit before T12 go/no-go.

**Ready to execute testing roadmap.**
