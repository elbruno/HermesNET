# Project Context

- **Owner:** Bruno Capuano
- **Project:** HermesNET
- **Stack:** .NET 10, ASP.NET Core, Microsoft Agent Framework, Microsoft.Extensions.AI, OpenTelemetry, SQLite/PostgreSQL
- **Description:** Hermes-inspired .NET runtime for profiles, sessions, skills, memory, tool policy, and observability.
- **Created:** 2026-05-22

## Learnings

Initialized as runtime/backend owner for PRD execution plan.

## M1 Onboarding Notes

**Understanding of M1 Scope:**
M1 (Foundations, 2 weeks) establishes the technical foundation with three core deliverables: a SQLite session store with load testing for 1,000+ concurrent sessions (R5 owns this), an OpenAI-compatible provider path wired through Microsoft.Extensions.AI `IChatClient`, and OTel baseline instrumentation for traces/metrics/logs. The goal is confidence in architecture mapping (R1) and session persistence at scale before any Hermes-specific code (profiles, skills, memory) is written. End-to-end local CLI chat through the provider must work cleanly by Week 2.

**Blockers — Things I need before starting M1 implementation:**
1. **Solution structure preferences** — Is the project layout already created, or should I scaffold it? Need clarity on:
   - Top-level project/folder structure (src/, samples/, tests/ layout confirmed in plan, but want confirmation on Directory.Build.props strategy)
   - NuGet package versions for central coordination (particularly Microsoft.Agents.AI, Microsoft.Extensions.AI, OpenTelemetry packages)
   
2. **OpenAI-compatible provider path specifics** — The plan says "OpenAI-compatible + local Ollama" but doesn't specify:
   - Should I use the official OpenAI SDK, or Microsoft.Extensions.AI's provider abstraction directly?
   - Is there a preferred way to swap between OpenAI and Ollama at runtime? (config, DI, environment variable?)
   - What's the fallback strategy if neither provider is available? (error, mock, local?)

3. **SQLite schema ownership** — Plan shows entities (AgentProfile, Session, SessionTurn, etc.) but I need:
   - Confirmation that this entity model is the contract I should implement (no hidden requirements from M2 that affect M1 schema)
   - Whether migrations should use EF Core Code First or SQL scripts

**Questions — Ambiguities I should clarify with Ripley:**
1. **R1 Validation Scope** — The plan says "Can a complete E2E chat flow be built on MAF?" and Ripley validates in Week 1. Should I build a spike/proof-of-concept or does the actual session store count as the R1 validation?
2. **OTel Local Collector** — How should I set up the local collector? Docker Compose? .NET Aspire? Standalone? (Plan mentions Aspire in M5, so probably not Aspire-dependent yet.)
3. **CLI Framework** — What's the preference for the CLI: System.CommandLine, Spectre.Console, or something else?

**Ready Signal:**
**NOT YET READY.** I need:
- ✅ Confirmation that the provided plan is the approved scope (awaiting Ripley's Go signal)
- ⏳ Solution structure scaffold (if not already done)
- ⏳ Central NuGet package version strategy confirmed
- ⏳ OpenAI/Ollama provider path clarified (SDK choice, runtime swapping strategy)
- ⏳ SQLite schema / EF Core approach confirmed

Once Ripley approves the plan and clarifies the three questions above, I can start M1 immediately with focus on session store load testing and R5 validation.

---

## M1 Week 1 Ready — 2026-05-22

✅ **READY TO START** — All blockers resolved. T1 (solution structure) unblocked.

**My M1 ownership:**
- T1 (Week 1): Solution scaffold + provider path wiring
- T2 (Week 1 → Week 2): Session store (SQLite + ORM)
- R5 checkpoint (Week 2): Load test execution + latency validation

**Key understanding:**
- Ripley scaffolds the solution; I verify build
- Provider: Microsoft.Extensions.AI.IChatClient, config-driven Ollama swapping
- Schema: EF Core Code First migrations (Ripley initializes DbContext, I extend it)
- Test framework: xUnit + Coverlet, 80% branch coverage M1 target
- Load test: Sequential inserts (1K sessions) + concurrent reads, P95 latency gates

**Blockers cleared, dependencies mapped, ready to execute.**

## M1 Week 1 Kick-off Approved — 2026-05-22

✅ **GO SIGNAL:** All 7 Ripley blockers merged into canonical decisions. Critical path locked. T1 start date: 2026-05-23.

**Day 1 objective:** Ripley scaffolds solution; Dallas verifies `dotnet build` passes zero warnings on Linux/macOS/Windows, all projects load in IDE.

**Week 1 watch:** After T1 verification, Parker (T4 OTel) and Ash (T3 provider audit) begin parallel work. Ripley gates R1 checkpoint (abstraction map review) at end of Week 1.

**Ready to execute Week 1 plan.**

---

## M1 T2 Complete — Provider Wiring & R1 Spike

IChatClient abstraction wired via ChatClientFactory. appsettings.json config + Hermes.Cli DI setup complete.
R1 integration test written (Hermes.Core.Tests/Integration/ChatClientFactoryTests.cs) — validates architecture mapping (config → factory → provider → response).
Ready for Ripley's R1 gate review on 2026-05-27.

**Unblocks:** Ripley R1 approval gates T6 (session store start). Next: await Ripley R1 verdict, then start T6.

---

## Week 2 Kickoff — R1 Verdict GREEN ✅
**2026-05-22 — Team Update from Scribe**

✅ **R1 VALIDATED GREEN** — All 6 integration tests pass. Factory routing, provider instantiation, and OTel baseline (P95 48ms) confirmed.

**Team Updates:**
- Week 2 unblocked. T6 (Session Store) can begin immediately without architectural concerns.
- R5 (SQLite scale test) proceeds as planned in Week 2.
- OTel baseline established (P95 48ms < 100ms target).
- Test extensibility confirmed for T6 and future tool invocation.

**Next:** Await Ripley's M1 detailed task breakdown (week-by-week) for team kickoff. R5 risk spike begins this week.
