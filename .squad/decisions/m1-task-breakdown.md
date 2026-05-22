# Milestone 1: Foundations — Detailed Task Breakdown

**Issued by:** Ripley (Project Lead)  
**Date:** 2026-05-22  
**Status:** ✅ APPROVED — Gates Dallas to start  
**Reference:** `docs/research/plan.md` § Milestone 1: Foundations

---

## Framing

M1 is **not** a feature milestone — it is a confidence milestone. The team ships nothing to end users. The team proves that the platform can be built: clean solution, provider path wired, SQLite holding up at scale, OTel visible from day one, and a minimal CLI chat loop that works locally. Every line of Hermes-specific code written in M2–M6 depends on these foundations being correct.

**Critical path (strict order):**  
Solution setup → Provider path wiring (R1 spike) → OTel baseline → SQLite session store → CLI integration → Load test (R5) → Security baseline

Parker and Ash run parallel to Dallas in Week 1. Week 2 converges on integration.

---

## Week 1: Setup, Provider Path, OTel Baseline (Days 1–5)

| Task | Owner | Duration | Acceptance Criteria | Dependencies | Key Files |
|------|-------|----------|---------------------|--------------|-----------|
| **T1 — Create solution structure** | Dallas | 1 day | ✅ `dotnet build` produces zero warnings on Linux/macOS/Windows in CI; ✅ `Directory.Build.props` sets `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, TFM `net10.0`; ✅ `Directory.Packages.props` pins all package versions centrally with no `Version=` attributes in individual `.csproj` files | None | `Hermes.sln`, `src/Hermes.Core/Hermes.Core.csproj`, `src/Hermes.Host/Hermes.Host.csproj`, `src/Hermes.Cli/Hermes.Cli.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `global.json` |
| **T2 — Wire IChatClient provider path** *(R1 spike)* | Dallas + Ripley | 2 days | ✅ `IChatClient` resolves from DI with both OpenAI-compatible and local Ollama configs (via `appsettings.json` switch); ✅ A single `ChatAsync(string prompt)` call in `Hermes.Core` returns a real model response with no hardcoded mock; ✅ Ripley code-reviews the mapping: `IProfile` → MAF agent, `ISession` → MAF session, tool descriptor → MAF function — no concept mismatch identified (R1 GREEN) | T1 | `src/Hermes.Core/Providers/ChatClientFactory.cs`, `src/Hermes.Core/Abstractions/IHermesChatService.cs`, `src/Hermes.Host/appsettings.json`, `src/Hermes.Host/Program.cs` |
| **T3 — Wire MAF host and agent lifecycle** | Dallas | 1 day | ✅ `IAgentRuntime` (MAF) bootstraps from `IServiceCollection` with `AddHermesAgent()`; ✅ Agent start/stop lifecycle methods call without exception on empty input; ✅ Unit test: `AgentRuntimeTests.cs` — startup/shutdown cycle passes | T2 | `src/Hermes.Core/Runtime/HermesAgentRuntime.cs`, `src/Hermes.Core/Extensions/ServiceCollectionExtensions.cs`, `tests/Hermes.Core.Tests/Runtime/AgentRuntimeTests.cs` |
| **T4 — Implement OTel baseline** | Parker | 2 days | ✅ Traces, metrics, and logs are emitted to a local OTLP collector (Aspire dashboard or local Jaeger) for every chat request path; ✅ Minimum spans: `hermes.chat.request`, `hermes.provider.call`, `hermes.session.save`; ✅ Parker walks the Aspire trace dashboard — all three spans visible in a single E2E trace ID | T2 | `src/Hermes.Core/Observability/HermesTelemetry.cs`, `src/Hermes.Core/Observability/ActivitySources.cs`, `src/Hermes.Host/appsettings.json` (`OTLP` config block), `src/Hermes.Host/Program.cs` (OTel registration) |
| **T5 — Run dependency and secrets audit** | Ash | 1 day | ✅ `dotnet list package --vulnerable --include-transitive` returns zero critical CVEs; ✅ Secret scanner CI step (e.g., `trufflehog` or GitHub secret scanning) configured in `.github/workflows/security.yml`; ✅ No hardcoded secrets in any file committed to the repo | T1 | `.github/workflows/security.yml`, `.gitignore` (secrets exclusion), `SECURITY.md` |

---

## Week 2: Session Store, CLI, Integration, Risk Validation (Days 6–10)

| Task | Owner | Duration | Acceptance Criteria | Dependencies | Key Files |
|------|-------|----------|---------------------|--------------|-----------|
| **T6 — Implement SQLite session store** | Dallas | 2 days | ✅ `ISessionStore` CRUD: `CreateAsync`, `GetAsync`, `UpdateAsync`, `DeleteAsync`, `ListRecentAsync` all pass unit tests with an in-memory SQLite; ✅ Schema migration runs idempotently on fresh and existing DBs (`EnsureCreated` or Fluent Migrator); ✅ ≥ 80% branch coverage on `SessionStore` class | T3 | `src/Hermes.Core/Session/SessionStore.cs`, `src/Hermes.Core/Session/ISessionStore.cs`, `src/Hermes.Core/Session/SessionEntity.cs`, `src/Hermes.Core/Data/Migrations/001_InitialSchema.sql`, `tests/Hermes.Core.Tests/Session/SessionStoreTests.cs` |
| **T7 — Implement CLI chat entry point** | Dallas | 1 day | ✅ `hermes chat --profile default "Hello"` returns a model response printed to stdout; ✅ Session is persisted to SQLite after each exchange (verified by querying the store in the test); ✅ `hermes --help` prints usage without error | T4, T6 | `src/Hermes.Cli/Commands/ChatCommand.cs`, `src/Hermes.Cli/Program.cs`, `src/Hermes.Cli/Hermes.Cli.csproj` (add `System.CommandLine` ref) |
| **T8 — Load-test SQLite session store** *(R5 validation)* | Dallas | 1 day | ✅ Insert 1,000 sessions sequentially; P95 insert latency ≤ 50 ms; ✅ `ListRecentAsync(limit: 50)` P95 ≤ 20 ms after 1,000 rows inserted; ✅ Load script and results documented in `docs/benchmarks/m1-session-load.md` — numbers committed to repo | T6 | `tests/Hermes.LoadTests/SessionLoadTest.cs`, `docs/benchmarks/m1-session-load.md` |
| **T9 — Validate YAML skill parser (R5 edge cases)** | Dallas | 0.5 day | ✅ 5+ malformed YAML inputs each throw `SkillParseException` with a descriptive message (tested: missing `name`, invalid `type`, null `description`, extra unknown keys, empty file); ✅ A valid minimal skill YAML parses without error; ✅ All 6 tests pass and are committed | T6 | `src/Hermes.Core/Skills/SkillParser.cs`, `src/Hermes.Core/Skills/SkillParseException.cs`, `tests/Hermes.Core.Tests/Skills/SkillParserTests.cs`, `tests/Hermes.Core.Tests/Skills/fixtures/` (YAML fixture files) |
| **T10 — Record performance baseline** | Parker | 0.5 day | ✅ BenchmarkDotNet or `Stopwatch`-based harness runs `ChatAsync` 50 times with local Ollama, records P50/P95/P99; ✅ Result: P95 ≤ 100 ms (local provider, no load); ✅ Results committed to `docs/benchmarks/m1-perf-baseline.md` — this file becomes the M2 regression baseline | T7 | `tests/Hermes.Benchmarks/ChatLoopBenchmark.cs`, `docs/benchmarks/m1-perf-baseline.md` |
| **T11 — Run E2E smoke test and OTel trace walk** | Parker + Dallas | 0.5 day | ✅ Full path: CLI → Session → Provider → Response executes without exception; ✅ Aspire/Jaeger dashboard shows a single trace with all three required spans (`hermes.chat.request`, `hermes.provider.call`, `hermes.session.save`); ✅ Parker screenshots the trace and commits it to `docs/benchmarks/m1-otel-trace.png` | T7, T4 | `docs/benchmarks/m1-otel-trace.png`, `tests/Hermes.Integration.Tests/E2ESmokeTest.cs` |
| **T12 — Ripley M1 architecture review & Go/No-Go** | Ripley | 0.5 day | ✅ Ripley reviews T2 mapping doc, T8 load test results, T9 YAML tests, T10 perf baseline, T11 trace walk — all GREEN; ✅ Ripley signs off in `.squad/decisions.md` with "M1 APPROVED"; ✅ Both risk checkpoints (R1 + R5) documented as GREEN in `docs/benchmarks/` | T8, T9, T10, T11 | `.squad/decisions.md` (M1 entry), `.squad/agents/ripley/history.md` (updated) |

---

## Risk Checkpoint Definitions

---

### Risk Checkpoint: R1 — Integration Drift — Week 1 (Day 3–5)

**Risk:** MAF/MEAI abstractions (`IChatClient`, agent lifecycle, session, function invocation) do not map cleanly to Hermes semantics (profile, session, tool response). A wrong mapping here contaminates every subsequent milestone — every Hermes concept sits on top of this mapping.

**Validation Requirement:** A working E2E chat flow must be demonstrably built on MAF primitives, with Hermes concept semantics layered above them, and a code-review by Ripley must confirm no concept mismatch requires a design rethink.

**Test/Demo to Prove It:**

```csharp
// Step 1: Wire DI in tests/Hermes.Core.Tests/Integration/R1IntegrationDrift.cs
var services = new ServiceCollection();
services.AddHermesAgent(cfg => cfg.UseOllama("http://localhost:11434", "llama3"));
var provider = services.BuildServiceProvider();

// Step 2: Send a real message through the full stack
var chatService = provider.GetRequiredService<IHermesChatService>();
var session = await sessionStore.CreateAsync(profileId: "default");
var response = await chatService.ChatAsync(session.Id, "What is 2+2?");

// Step 3: Assert the response is non-null and the session was persisted
Assert.NotNull(response.Content);
var saved = await sessionStore.GetAsync(session.Id);
Assert.NotNull(saved.LastMessage);

// Step 4: Assert a tool invocation flows through (use a no-op echo tool)
var toolResponse = await chatService.ChatWithToolsAsync(
    session.Id,
    "Echo: hello world",
    tools: [new EchoTool()]);
Assert.Contains("hello world", toolResponse.Content);
```

**Then:** Dallas sends a PR. Ripley reviews the abstraction map:

| Hermes Concept | MAF/MEAI Concept | Mapped in Code? |
|---|---|---|
| `IProfile` | MAF agent configuration | `HermesAgentRuntime.cs` |
| `ISession` | MAF session + `SessionStore` | `SessionStore.cs` |
| Tool descriptor | MAF function tool | `ChatClientFactory.cs` |
| Provider routing | `IChatClient` factory | `ChatClientFactory.cs` |

**Success Criteria:**
- All 4 integration assertions in `R1IntegrationDrift.cs` pass (real model response, session persisted, tool invocation routed correctly)
- Ripley finds **zero** concept mismatches in the abstraction map table that force a design rethink
- The PR is merged with Ripley's explicit "R1 GREEN" sign-off in the review comment
- No new abstract interfaces are invented; the MAF/MEAI primitives are used directly or wrapped with ≤ 1 thin adapter layer

**Owner:** Ripley (validation); Dallas (implementation)

**If It Fails:**
- **Option A:** Ripley calls a 2-hour architecture session with Dallas. Redesign the abstraction that failed (usually the profile↔agent or session↔MAF mapping). Spike a new proof-of-concept within 1 day. Re-validate against the same test.
- **Option B:** If MAF session model is fundamentally incompatible with Hermes session semantics, drop MAF session dependency and implement a standalone `ISessionStore` directly backed by SQLite. Document the deviation in `.squad/decisions.md`. This is a scoped fallback — all other MAF/MEAI primitives remain.

---

### Risk Checkpoint: R5 — Skills/Scale — Week 2 (Day 8–9)

**Risk:** SQLite degrades under realistic session load (1,000+ sessions), or the YAML skill parser silently accepts malformed inputs and propagates corrupt skill state into M2–M6. Either failure undermines the reliability story of the entire runtime.

**Validation Requirement:** SQLite must handle 1,000+ sessions with documented P95 latency within budget. The YAML parser must reject all 5+ defined malformed inputs with clear, typed exceptions before any Skills code is built on top of it.

**Test/Demo to Prove It:**

**Part A — SQLite Load Test (`tests/Hermes.LoadTests/SessionLoadTest.cs`):**

```csharp
[Fact]
public async Task SQLite_Handles_1000_Sessions_Within_Latency_Budget()
{
    using var store = new SessionStore(":memory:");
    await store.InitializeAsync();

    // Insert 1,000 sessions and measure P95 insert latency
    var insertTimes = new List<long>();
    for (int i = 0; i < 1000; i++)
    {
        var sw = Stopwatch.StartNew();
        await store.CreateAsync(profileId: "perf-test", message: $"msg-{i}");
        sw.Stop();
        insertTimes.Add(sw.ElapsedMilliseconds);
    }
    var p95Insert = Percentile(insertTimes, 95);
    Assert.True(p95Insert <= 50, $"P95 insert = {p95Insert} ms (budget: 50 ms)");

    // Query ListRecentAsync after full load and measure P95
    var queryTimes = new List<long>();
    for (int i = 0; i < 100; i++)
    {
        var sw = Stopwatch.StartNew();
        var results = await store.ListRecentAsync(limit: 50);
        sw.Stop();
        queryTimes.Add(sw.ElapsedMilliseconds);
    }
    var p95Query = Percentile(queryTimes, 95);
    Assert.True(p95Query <= 20, $"P95 query = {p95Query} ms (budget: 20 ms)");
}
```

**Part B — YAML Skill Parser Malformed Input Tests (`tests/Hermes.Core.Tests/Skills/SkillParserTests.cs`):**

```csharp
[Theory]
[InlineData("fixtures/skill-missing-name.yaml")]        // name field absent
[InlineData("fixtures/skill-invalid-type.yaml")]        // type: 9999 (not a string)
[InlineData("fixtures/skill-null-description.yaml")]    // description: ~
[InlineData("fixtures/skill-extra-unknown-keys.yaml")]  // unknown: field present
[InlineData("fixtures/skill-empty.yaml")]               // completely empty file
public void SkillParser_RejectsAllMalformedInputs(string fixturePath)
{
    var parser = new SkillParser();
    var act = () => parser.Parse(File.ReadAllText(fixturePath));
    act.Should().Throw<SkillParseException>()
       .WithMessage("*")   // any message — we just need a typed exception
       .And.Message.Should().NotBeNullOrWhiteSpace();
}

[Fact]
public void SkillParser_AcceptsMinimalValidSkill()
{
    var yaml = """
        name: echo-test
        description: A minimal valid skill for testing
        type: chat
        """;
    var parser = new SkillParser();
    var skill = parser.Parse(yaml);
    Assert.Equal("echo-test", skill.Name);
}
```

**Success Criteria:**
- P95 SQLite insert latency ≤ 50 ms at 1,000 sessions
- P95 `ListRecentAsync` latency ≤ 20 ms after 1,000 rows
- All 5 malformed YAML inputs throw `SkillParseException` with a non-empty message
- Valid skill parses without error
- Results logged to `docs/benchmarks/m1-session-load.md` with exact P50/P95/P99 numbers and committed to repo

**Owner:** Dallas (implementation + load test execution); Ripley (final review)

**If It Fails:**
- **SQLite latency exceeds budget:** Dallas evaluates whether WAL mode + index optimization resolves the issue within 1 day. If not, Ripley calls a PostgreSQL migration decision before M2 starts. Migration path must be documented in `.squad/decisions.md`.
- **YAML parser silently accepts invalid input:** Dallas hardens the parser (strict-mode YamlDotNet deserialization + required-field annotations) before any skill-consuming code is written. No Skills code merges until all 5 tests pass.

---

## Go/No-Go Authority

> ⚠️ **All 12 tasks must complete AND both risk checkpoints (R1 + R5) must be GREEN before Ripley approves M1 completion. There is no partial pass. If either risk checkpoint is RED at the end of Week 2, Ripley freezes M2 scope until the issue is resolved — maximum 3 working days remediation sprint.**

**Ripley signs off in `.squad/decisions.md` with:**  
`"M1 APPROVED — R1 GREEN, R5 GREEN, all quality gates passed. Dallas cleared to start M2."`

---

## Critical Path & Dependencies Summary

```
T1 (Solution) ─→ T2 (IChatClient + R1 spike) ─→ T3 (MAF host) ─→ T6 (Session store) ─→ T8 (Load test / R5-A)
                                                                                         ─→ T9 (YAML parser / R5-B)
                              ─→ T4 (OTel baseline) ─→ T11 (E2E smoke)
T1 ─→ T5 (Security audit)                                           ↘
                                          T7 (CLI chat) ─→ T10 (Perf baseline) ─→ T11 (E2E smoke) ─→ T12 (Go/No-Go)
```

**T1 is day-zero. Nothing starts without it.** Dallas owns T1 and must complete it on Day 1. T2 is the highest-risk task in the milestone — it is the R1 spike and the longest-running design conversation. Ripley is co-owner on T2 review. Parker and Ash run independently after T1 (T4, T5 have no dependency on T2). Week 2 converges: T6 → T7 → T8/T9/T10 → T11 → T12.
