# M3C Phase Gate: Microsoft Agent Framework Suitability & Integration Evaluation

**Date:** 2026-05-22T23:21:23.494-04:00  
**Authority:** Ripley (Lead)  
**Status:** DECISION GATE — Go/No-Go for MAF integration in M3C  
**Review Context:** M2 shipped with 262 passing tests (87.5% coverage), custom SkillRegistry + ToolRegistry implementation. Current decision point: integrate MAF or extend custom stack?

---

## Executive Summary

**VERDICT: DEFER MAF Refactor to M4. Extend M3 with Focused Feature Completeness.**

The custom SkillRegistry and ToolRegistry implementation is **semantically aligned with MAF patterns** but provides faster iteration for M3C scope (Policy Engine + MCP Integration). A full MAF refactor would add 2–3 weeks and significant test rewrite without proportional benefit in M3 scope. **Recommended Path:** Implement T16 (Policy Engine) on top of current registries; design MAF wrapper abstractions in parallel; commit to MAF integration as M3→M4 technical bridge, not blocking M3C completion.

---

## Part 1: MAF Suitability Evaluation

### 1.1 Custom Implementation Analysis (Current State)

**SkillRegistry (`ISkillRegistry`, `SkillRegistry.cs`):**
- In-memory ConcurrentDictionary-backed skill registry
- O(1) lookup by skill ID (case-insensitive)
- Idempotent directory loading from `.md` files (YAML front matter parsing)
- Global uniqueness enforcement (DuplicateSkillException on collision)
- OTel-instrumented on all operations (10+ activity spans)
- Metadata support (free-form key-value pairs per skill)

**ToolRegistry (`IToolRegistry`, `ToolRegistry.cs`):**
- Read-only, sandboxed tool registry (M2 safe categories only)
- Category-based enumeration support (ReadFile, SystemInfo, TextProcessing)
- Policy-aware validation before invocation (inputs, path traversal checks, input size limits)
- Audit trail on every validation call
- No direct tool execution (defers to policy engine in M3)

**Integration Points:**
- `IHermesChatService` → single `IChatClient` abstraction (no tool-calling wired yet in M2)
- No MAF agent loop implemented; chat is stateless request/response
- No MAF session abstraction; sessions implemented via `ISessionService` + SQLite
- No MAF memory providers; memory loaded directly in `CuratedMemoryLoader`

### 1.2 MAF Pattern Alignment

| MAF Concept | Hermes Concept | Custom Implementation | Alignment |
|---|---|---|---|
| `IAgentService` (agent runtime) | Chat service + orchestration | `IHermesChatService` + `ISessionService` | Loose; HermesChatService is stateless |
| `AIComponent` (composable runtime) | Skills + tools + memory | `ISkillRegistry` + `IToolRegistry` + `CuratedMemoryLoader` | **High**; these are exactly component candidates |
| `IToolSet` (tool collection) | Tool categories | `ToolRegistry.ListToolsByCategory()` | **High**; parallel structure |
| Session state | Session persistence | `ISessionService` + SQLite | **Medium**; MAF sessions are lighter-weight agent state |
| Function tool invocation | Skill/tool execution | Not yet wired (M3 scope) | Pending; not yet evaluated |

**Concrete Alignment Findings:**
1. ✅ **SkillRegistry is semantically isomorphic to MAF AIComponent + function tool patterns**
   - Both: discoverable, metadata-rich, composable, callable from agent loop
   - Difference: Skills are markdown-first (Hermes innovation), MAF skills are .NET function-based
   - **Wrap opportunity:** `SkillRegistry` → `IToolSet` adapter pattern (no rewrite needed; adapter layer only)

2. ✅ **ToolRegistry maps directly to MAF tool management**
   - Both: category-aware, policy-enforced, audit-able
   - Difference: MAF tooling is built into agent framework; HermesNET isolates tools in separate registry
   - **Wrap opportunity:** `ToolRegistry` → MAF function tools via adapter (again, adapter layer only)

3. ⚠️ **Session/state management mismatch**
   - MAF sessions are agent-scoped, lightweight (turn history + state)
   - HermesNET sessions are profile-scoped, heavyweight (turn history + memory snapshots + tool audit)
   - **Risk:** Forcing HermesNET session model into MAF session boundaries would break R2 (profile isolation)
   - **Reality:** Can coexist; MAF session wraps HermesNET session without conflict

### 1.3 Concrete Benefits of MAF Integration

| Benefit | Impact | Effort | Timing |
|---|---|---|---|
| **Automatic function tool wiring** | M3 Policy Engine gets transparent tool invocation; less custom plumbing | -1 week for tool binding; +1 week for MAF integration | M3→M4 boundary |
| **OTel observability hooks** | MAF emits spans for agent execution, tool calls, errors; less custom instrumentation | -0.5 weeks custom OTel code | M3→M4 boundary |
| **Streaming/token-level events** | MAF handles SSE/WebSocket token events natively; less custom streaming logic | -0.5 weeks streaming plumbing | M4+ (REST API already streams in M2) |
| **Standard multi-agent orchestration** | Delegation (M4) uses MAF parent-child patterns rather than custom session lineage | -0.5 weeks custom delegation code | M4 dependency |
| **Microsoft ecosystem alignment** | Azure deployment, prompt flow integration, Copilot plugins fit naturally | +0.5 weeks planning | M5+ (UX phase) |
| **Reduced custom infrastructure** | Less code to maintain, test, and document | -10–15% codebase after refactor | Long term |

**Critical Gap:** MAF does **not** provide markdown skills, curated memory, profile-scoped isolation, or safety policy abstractions. HermesNET must build those as application layers regardless of MAF integration.

### 1.4 Concrete Costs of MAF Integration (if done in M3C)

| Cost | Scope | Time | Risk |
|---|---|---|---|
| **Refactor SkillRegistry** | Adapt current registry to expose MAF function tools; two-way translation (markdown → AIFunction) | 3 days | Low (adapter layer only; no SkillRegistry logic change) |
| **Refactor ToolRegistry** | Wrap tool registry in MAF function tool factory; policy engine → MAF approval middleware | 3 days | Low (registry logic unchanged) |
| **Rewrite IHermesChatService** | Swap stateless ChatAsync for MAF AgentService chat loop; handle streaming, tool calls, context | 5 days | **High** (core orchestration change; extensive testing needed) |
| **Update Session State** | MAF sessions light; HermesNET sessions heavy; design bridge without breaking R2 isolation | 4 days | **High** (profile scoping must be verified under MAF) |
| **Test Rewrite** | ~80% of M2 tests become obsolete (mocks MAF instead of custom interfaces); new test contracts needed | 5 days | **High** (262 tests → rewrite needed) |
| **R2 Regression Risk** | Profile isolation must survive MAF session boundary; new integration tests required | 3 days | **High** (R2 is hard gate; no partial credit) |

**Total M3C Cost Estimate:** **10–12 days** (instead of 3 weeks focused on Policy Engine).  
**Net Risk:** High rework, high test churn, zero new M3 features shipped.

### 1.5 Edge Cases & Current Implementation Realities

**R2 Memory Isolation (Profile-Scoped):**
- Current: `CuratedMemoryLoader.LoadMemoryAsync(profileId)` enforces profile FK constraint at SQLite layer
- MAF: Sessions are lightweight, agent-coped; no built-in profile concept
- **Issue:** MAF doesn't know about Hermes profiles; session boundary ≠ profile boundary
- **Mitigation:** Wrap MAF session with HermesNET `ProfileId` context; pass through all load operations
- **Verdict:** Doable, but adds abstraction layer; M3C refactor would need explicit R2 validation

**OTel Coverage (M2 achieved 90%+):**
- Current: 10+ manual `TelemetryProvider.GetActivitySource().StartActivity()` calls
- MAF: Built-in `IAgentExecutionContext.Telemetry` + automatic spans for framework operations
- **Benefit:** Less boilerplate; but manual spans still needed for Hermes-specific operations (memory load, policy decision)
- **Reality:** Mixed instrumentation necessary either way; MAF removes framework boilerplate only
- **Verdict:** Modest OTel win; not a blocker for deferring

**Global Skill ID Uniqueness (M2-003 Decision Ambiguity):**
- Current: Global uniqueness enforced in `SkillRegistry.RegisterSkillAsync()`
- MAF: No enforced uniqueness; function tools scoped by agent
- **Issue:** If we add namespacing (e.g., "math/calculate-sum"), MAF approach is better; current impl would need redesign
- **Reality:** M2 shipped without namespacing; M3C can defer namespace decision
- **Verdict:** Not a blocker for M3C; MAF more flexible if namespacing needed later

**Memory Semantics under Multi-Agent Delegation (M4 dependency):**
- Current: Each session loads distinct `MEMORY.md` + `USER.md` per profile
- MAF: Child agents can inherit parent context; semantics unclear if child is different profile
- **Issue:** What if delegated task runs as different profile? Memory scoping must be explicit
- **Reality:** M3C doesn't implement delegation; M4 design needed regardless
- **Verdict:** Not a blocker for M3C; delegation design must happen before M4

---

## Part 2: M3C Decision Logic

### 2.1 M3 Scope (Current)

**T16: Policy Engine** (Ash, 2 weeks)
- Approvals, denials, URL allow/deny lists
- Input/output redaction
- Audit trail to OTel
- No tool execution yet; registry integration happens after policy is defined

**M3 Quality Gate:**
- 100% adversarial policy tests pass
- Zero secret leakage paths
- Audit events on every tool call

### 2.2 Why MAF Integration Blocks M3C Completion

1. **IHermesChatService refactor is not M3 scope.** M3 focuses on policy engine, not orchestration.
   - Policy engine can be plugged into current `IHermesChatService` without MAF
   - No need to rewrite chat loop for safety validation

2. **Session/context threading is not M3 scope.** M3 doesn't implement delegation.
   - MAF's session abstraction becomes critical only in M4
   - M3 safety model can work with current lightweight context passing

3. **Test rewrite would consume 5+ days.** M3 needs feature focus.
   - 262 existing tests are validation; rewriting them for MAF adds zero new capability
   - Ash should write 50+ new adversarial policy tests, not rewrite history

4. **R2 isolation re-validation would slip M3C schedule.** Risk checkpoint is time-critical.
   - R2 was validated in M2 on current architecture
   - Introducing MAF adds risk re-validation burden to M3C gate
   - Conservative choice: keep architecture stable in M3; refactor after safety proof

### 2.3 Better Approach: M3 on Custom Stack, M3→M4 Bridge Design

**Phase M3C (Current + Adapter Design):**
1. Implement T16 (Policy Engine) on current `IHermesChatService`
2. Policy engine validates tool calls; registry remains unchanged
3. **In parallel:** Ripley + Dallas design MAF adapter layer
   - `IHermesChatService` → MAF `IAgentService` wrapper
   - `SkillRegistry` → MAF `IToolSet` factory
   - `ToolRegistry` → MAF function tool collection
   - Session context threading (profile ID, memory) → MAF context carrier
4. Adapter design reviewed and approved before M3 closes

**Phase M3→M4 (Refactor Boundary):**
1. Implement M4 (Automation & Delegation) using MAF delegation patterns
2. After M4 safety validation, backport M3 tests to MAF mocks (if worthwhile)
3. Retire custom chat loop only after delegation story validates that MAF boundaries work

**Advantage:** Zero M3 schedule impact. Policy Engine ships. MAF readiness path clear. M4 gets cleaner delegation story.

---

## Part 3: Design & Migration Path (If MAF Integration Were Chosen)

### 3.1 Minimal Wrapper Strategy

**Do not rewrite SkillRegistry. Wrap it.**

```csharp
// Current interface stays as-is
public interface ISkillRegistry
{
    Task RegisterSkillAsync(SkillDescriptor skillDefinition);
    Task<SkillDescriptor> GetSkillAsync(string skillId);
    Task<IReadOnlyList<SkillDescriptor>> ListSkillsAsync();
}

// New adapter for MAF (M4 scope, not M3)
public interface IMafSkillToolSet : IToolSet
{
    // Implement MAF IToolSet over ISkillRegistry
}

// Implementation (adapter pattern)
public class SkillRegistryToolSetAdapter : IMafSkillToolSet
{
    private readonly ISkillRegistry _skillRegistry;

    public async Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct)
    {
        var skills = await _skillRegistry.ListSkillsAsync();
        return skills.Select(s => ConvertSkillToAIFunction(s)).ToList();
    }

    private AIFunction ConvertSkillToAIFunction(SkillDescriptor skill)
    {
        // Translate skill.Parameters to AIFunctionParameter list
        // Create AIFunction with skill.Name, description, and invocation handler
        // Handler: invoke skill, catch SkillParseException, map to AIFunctionResult
    }
}
```

**Benefit:** Current `ISkillRegistry` contract unchanged; skills remain markdown-first; zero test rewrite in M3.

### 3.2 Tool Registry Wrapping

```csharp
public class ToolRegistryPolicyMiddleware : IChatClientBuilder
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IPolicyEngine _policyEngine;

    public async Task<ToolInvocationResult> ValidateAndInvokeAsync(
        string toolName,
        IReadOnlyDictionary<string, string> args,
        CancellationToken ct)
    {
        // Step 1: Get tool from registry
        var tool = await _toolRegistry.GetToolAsync(toolName);

        // Step 2: Validate invocation through policy
        var validation = _toolRegistry.ValidateToolInvocation(toolName, args);
        if (!validation.IsValid)
            throw new UnauthorizedAccessException(validation.Reason);

        // Step 3: Invoke
        return await InvokeToolAsync(tool, args, ct);
    }
}
```

**Benefit:** Reuses existing `IToolRegistry` validation logic; `IPolicyEngine` sits as middleware, not replacement.

### 3.3 Session Context Threading

```csharp
// Current HermesNET session model
public interface ISessionService
{
    Task<Session> CreateAsync(string profileId, string name, CancellationToken ct);
    Task<Session> GetAsync(string sessionId, CancellationToken ct);
}

// MAF session would be lighter-weight; bridge needed
public class HermesSessionContextCarrier : IAgentExecutionContext
{
    public string ProfileId { get; }  // Hermes-specific; carries profile isolation
    public AgentSession MafSession { get; }  // MAF session (turn history, state)
    public CuratedMemory Memory { get; }  // Hermes curated memory

    // IAgentExecutionContext implementation
}
```

**Benefit:** MAF session + HermesNET context coexist; no forced merging.

### 3.4 Test Impact (If Migration Chosen)

**Current (262 tests):**
- 232 unit tests (Core.Tests)
- 28 integration tests (Integration.Tests)
- 1 load test
- 1 benchmark

**Migration Impact:**
- ~80% of unit tests mock current interfaces; would need MAF mock patterns
- Integration tests still pass if MAF wraps correctly
- Load tests unchanged (registry performance is same)
- **Rewrite cost:** 5–7 days for 200 tests

**Recommendation:** Defer test rewrite to M4. Validate adapter layer with new tests (policy engine tests) in M3.

---

## Part 4: Go/No-Go Decision & Action Items

### VERDICT: DEFER MAF Refactor to M3→M4 Bridge

**Rationale:**
1. **M3 scope is policy + safety.** MAF is orchestration + streaming; orthogonal concerns.
2. **Custom implementation is production-ready.** 87.5% coverage, passing 262 tests, R2 validated.
3. **Adapter pattern is lower-risk.** Design MAF wrapping now; implement later without blocking M3C.
4. **Test rewrite is wasteful in M3.** Ash should write 50+ new adversarial policy tests, not rewrite existing ones.
5. **M4 delegation needs MAF.** MAF parent-child patterns natural for delegation; keep refactor for M4 boundary.

**Decision:** Proceed with M3C (Policy Engine) on current custom stack. Design MAF adapter layer in parallel. Commit M3→M4 refactor after M4 delegation design is locked.

---

## Part 5: Blockers & Assumptions

### Blockers for MAF Integration (if reconsidered)
None. MAF integration is technically feasible but not necessary for M3C completion.

### Assumptions for Current Path
1. **R2 isolation remains valid under no refactor.** ✅ Proven in M2 testing.
2. **Policy engine can work with current `IHermesChatService`.** ✅ Policy validates after tool selection, not in chat loop.
3. **Adapter design can proceed in parallel without slowing M3.** ✅ Ripley + Dallas own design; Ash focuses on policy.
4. **M4 delegation will naturally use MAF.** ✅ Delegation is multi-agent orchestration; MAF is designed for that.

---

## Part 6: Risk Checkpoint & M3C Go/No-Go

| Risk | Mitigation | Owner | Go/No-Go Criteria |
|---|---|---|---|
| **MAF deferred too long** | Adapter design locked before M3 closes; refactor planned for M3→M4 boundary; decision not revisited in M4 | Ripley | Adapter design doc signed off by Dallas before T24 |
| **R2 isolation breaks unexpectedly** | Current R2 test suite stays in place; no regression on profile scoping in M3C | Lambert | R2 isolation test passes in M3 test run |
| **M4 delegation blocked by architecture** | MAF adapter layer proves delegation works before M4 starts; design spike in M3→M4 window | Ripley | M3→M4 delegation design approved by Ripley |
| **Custom stack becomes maintenance burden** | Adapter pattern isolates custom code; MAF wrapping limits codebase fragmentation | Dallas | Adapter layer achieves >80% of MAF benefit without refactoring current code |

---

## Part 7: M3C Schedule & Gate Outcome

**M3 Duration:** 3 weeks (May 26 – Jun 16)

| Phase | Duration | Deliverable | Owner | Gate |
|---|---|---|---|---|
| **T16: Policy Engine** | 2 weeks | `IPolicyEngine` impl + approvals + denials + redaction | Ash | 100% adversarial tests pass |
| **T16→M4 Bridge Design** | 1 week (parallel) | MAF adapter layer design + session context threading | Dallas + Ripley | Design doc approved |
| **M3 Go/No-Go** | 3 days | Ripley reviews all M3 + M4 readiness | Ripley | All M3 exit criteria + R3 gate + M3→M4 bridge design GREEN |

**Exit Criteria for M3:**
- ✅ Policy engine enforces approvals/denials correctly
- ✅ Zero secret leakage; audit events on all tool calls
- ✅ R3 risk checkpoint PASSED (policy engine is safe and idiomatic)
- ✅ MAF adapter layer designed and reviewed (not implemented)
- ✅ No regression on M1–M2 tests
- ✅ Ripley Go for M4

**Go/No-Go:** GREEN if all exit criteria met + MAF design doc approved. Proceed to M4 with confidence that delegation will use MAF patterns.

---

## Appendix: MAF Package Versions & Prerequisites

| Package | Version | Status | Use in M3 |
|---|---|---|---|
| `Microsoft.Agents.AI` | 1.6.1 | Current | Not (defer to M4) |
| `Microsoft.Agents.Hosting.AspNetCore` | 1.0.1 | Current | Not (defer to M4) |
| `Microsoft.Extensions.AI` | 10.6.0 | Current | Already used (IChatClient) |
| `Microsoft.Extensions.VectorData.Abstractions` | 10.6.0 | Current | Not (defer to M6 memory) |

**NuGet Dependencies Not Added in M3:** Microsoft.Agents.AI and related packages added only after M3→M4 bridge design is approved and refactoring begins.

---

**Ripley Signature:**  
Decision locked. M3C proceeds on custom stack with parallel MAF adapter design. M3→M4 refactor committed after M4 delegation design validated.  
**Date: 2026-05-22**  
**Authority: Ripley (Lead)**
