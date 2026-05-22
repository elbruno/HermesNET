# Project Context

- **Owner:** Bruno Capuano
- **Project:** HermesNET
- **Stack:** .NET 10, ASP.NET Core, Microsoft Agent Framework, Microsoft.Extensions.AI, OpenTelemetry, SQLite/PostgreSQL
- **Description:** Hermes-inspired .NET runtime for profiles, sessions, skills, memory, tool policy, and observability.
- **Created:** 2026-05-22

## Learnings

Initialized as memory/data owner for PRD execution plan.

## M1 Onboarding Notes

**M1 Scope Summary:**  
M1 (Foundations, 2 weeks) is about establishing the technical foundation so local chat works end-to-end. Parker's role is **OTel lead** and **performance baseline owner**. The goal is to wire OpenTelemetry (traces, metrics, logs) from day one and establish a latency baseline (< 100 ms response loop) against which M2+ will measure OTel overhead. Curated memory (MEMORY.md/USER.md) is explicitly deferred to M2; M1 focuses on session store persistence, provider path, and observability wiring.

**Blockers:**  
None explicit yet, but the plan's language around "latency overhead" needs clarification:
- **M1 baseline measurement:** The M2 gate specifies "< 20% latency overhead with full OTel active *vs. M1 recorded baseline*." This implies the M1 baseline should be measured in a known OTel state (disabled, minimal, or full). Is the baseline recorded with OTel off (to get "true" latency) or on (to measure real-world overhead)? This affects whether M1's 100 ms gate includes or excludes OTel.
- **Response loop definition:** The plan says "< 100 ms agent response loop latency (local provider, no load)." Does this mean: user message → provider returns response (excluding session save)? Or include session persist? Clarifying the exact measurement point is critical for the M2 regression gate to be meaningful.

**Questions:**  
1. **OTel framework scope in M1:** Are there established templates or conventions in Microsoft.Extensions.AI or MAF for OTel instrumentation, or does Parker define the instrumentation strategy from scratch? Should M1 establish a `TelemetryConfiguration` class or lean on ASP.NET Core's built-in OTel middleware?
2. **SQLite schema forward-compatibility:** The plan defers curated memory to M2, but does M1's SQLite schema include any tables or columns for memory-related data (e.g., `CuratedMemoryEntry` table)? Or are these added in M2? Need to avoid schema migrations that block M1→M2 transition.
3. **Load-test performance reporting:** Dallas owns the 1,000+ session load test, but who reports the P95 latency result that Parker uses to measure regression? Should Parker run the test independently, or integrate with Dallas's benchmark runner?
4. **Local-first baseline:** The plan emphasizes local-first education. Should the M1 baseline be measured against a local Ollama instance, or a public provider (OpenAI-compatible)? This affects whether the 100 ms gate is achievable.
5. **Sampling strategy placeholder:** The M2 gate mentions "adaptive sampling on high-frequency spans" as a possible optimization if OTel overhead exceeds 20%. Should M1 establish a pluggable sampling strategy from day one, or add it only if M2 shows a problem?

**Ready Signal:**  
**READY with clarifications.** Parker can start M1 OTel work once Ripley approves the breakdown and clarifies:
- (1) Whether the M1 latency baseline should be measured with OTel fully active or disabled
- (2) The exact definition of "response loop" for latency measurement
- (3) Whether SQLite schema should include forward-compatible memory columns in M1

Once these are clarified, Parker will own the OTel instrumentation framework and baseline measurement without blocking the team; Dallas's session store work and Ash's provider audit are independent.

---

## M1 OTel & Latency Baseline Locked — 2026-05-22

✅ **READY TO EXECUTE** — OTel instrumentation strategy + baseline measurement plan finalized.

**My M1 ownership:**
- OTel instrumentation: Traces (spans), metrics (token count, provider latency), structured logs
- Baseline measurement: Turn latency (user input → response) with OTel ON, P95 ≤ 100 ms target
- Provider latency: Measure Ollama response time separately (diagnostic)
- M2 overhead gate: Establish M1 baseline to measure "no >20% overhead" in M2

**Key commitments:**
- Enable OTel from Day 1 (not measured before/after)
- Turn latency = user CLI input sent → agent response returned (no SQLite persist latency)
- Capture traces: IChatClient call + provider latency + turn boundary
- Metrics: tokens/turn, provider latency ms, turn latency ms
- P95 latency target: ≤ 100 ms (with local Ollama, OTel ON)
- SQLite persist is async/background — not part of turn latency gate

**Baseline measurement locked, M2 regression gate defined, ready to wire OTel.**

## M1 Week 1 Kick-off Approved — 2026-05-22

✅ **GO SIGNAL:** All blockers resolved. OTel instrumentation strategy confirmed. Turn latency = user CLI input → response returned (no SQLite persist latency).

**Day 1 (2026-05-23):** Dallas builds T1 scaffold. Parker standby for T1 verification.

**Day 2–3 (2026-05-24–25):** Parker begins T4 OTel instrumentation. Wire `HermesTelemetry.cs` spans, register OTLP exporter, integrate with `Program.cs`. Target: OTLP collector (Aspire dashboard or Jaeger) displays span hierarchy by Day 3.

**Week 1 watch:** R1 checkpoint at end of week. Week 2: T10 baseline measurement (P95 ≤ 100 ms with OTel ON, 50 requests, local Ollama). Commit results to `docs/benchmarks/m1-perf-baseline.md`.

**Ready to execute OTel baseline.**
