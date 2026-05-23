# M4 MAF Adapter Strategy

**Document:** M4 MAF Adapter Strategy  
**Task:** T37 — MAF Adapter Design Document  
**Author:** Ripley (Lead)  
**Date:** 2026-05-22  
**Status:** DESIGN — Approved for M4 planning  
**Decision reference:** M3-001 (MAF refactor deferred to M3→M4 boundary)  
**Parallel track:** T36 (Ash — Policy Engine Design)

---

## Executive Summary

This document specifies the adapter strategy for migrating HermesNET from its M3 custom component stack to Microsoft Agent Framework (MAF) orchestration in M4. The migration is structured as three sequential swap phases, each bounded by a green test gate, with zero breaking changes to R2 profile isolation across every phase.

The central design guarantee is: **ProfileId and SessionId survive every adapter boundary without mutation.** All three phases are designed so that the adapter layer is a transparent relay for the profile-scoped identity carrier — the R2 FK constraint is never relaxed, even transiently.

---

## 1. Current State (M3)

### 1.1 Component Inventory

The M3 runtime runs three custom components that will be replaced in M4:

| Component | Interface | Responsibility |
|---|---|---|
| **SkillRegistry** | `ISkillRegistry` | In-memory + file-backed skill catalogue; O(1) lookup by skill ID; markdown parser for skill descriptors |
| **ToolRegistry** | `IToolRegistry` | Category-sandboxed CLI tool registry; M2 safe-category whitelist (ReadFile, SystemInfo, TextProcessing); audit log |
| **HermesChatService** | `IHermesChatService` | Thin adapter over `IChatClient`; exposes `ChatAsync` and `StreamChatAsync`; profile/session identity passed as parameters |

### 1.2 Adapter Stubs (Parker's T34 Work — Already in Place)

Three adapter interfaces and three stub implementations are present in `src/Hermes.Adapters/`. These stubs define the M4 contract without introducing any MAF dependency:

| Adapter Interface | Source | Target (M4) |
|---|---|---|
| `IToolSetAdapter` | `ISkillRegistry` | MAF `IToolSet` |
| `IFunctionToolAdapter` | `IToolRegistry` | MAF function tools |
| `IAgentOrchestratorAdapter` | `IHermesChatService` | MAF `IAgentOrchestrator` |

The stubs (`ToolSetAdapterStub`, `FunctionToolAdapterStub`, `AgentOrchestratorAdapterStub`) are pass-through implementations. They will be replaced with full MAF-backed implementations in M4 and must not be modified before that swap.

### 1.3 Identity Carrier (M3)

Profile and session identity flow through the current stack as plain strings:

```
HTTP POST /chat
  → HermesChatController.ChatAsync(message, profileId, sessionId)
    → IHermesChatService.StreamChatAsync(message, profileId, sessionId, ct)
      → IChatClient.CompleteAsync(chatHistory)       ← profileId/sessionId injected into system prompt
        → SessionStore.SaveAsync(profileId, sessionId, turn)
```

The `profileId` and `sessionId` are passed as explicit parameters at every layer. There is no ambient context object — they are always visible and always explicit.

### 1.4 M3 Constraints That Must Survive M4

The following M3 constraints are hard gates; M4 is not shippable without them:

- **R2 isolation** — ProfileId is a FK in `SessionContext`; no response, token event, or tool invocation may escape the profile boundary.
- **M2 safe-category whitelist** — ReadFile, SystemInfo, TextProcessing are the only tool categories allowed in MAF execution context; WriteFile, ExecuteCommand, Network are always denied.
- **Audit log continuity** — Every `ValidateToolInvocation` call emits to `IToolRegistry.AuditLog`; the policy engine (T36) consumes this log; the MAF migration must not break the feed.
- **Streaming identity** — Every `TokenEvent` emitted by `StreamTokensAsync` carries the originating `SessionContext`; per-token context is the safety net for streaming isolation.

---

## 2. Target State (M4)

### 2.1 MAF Component Map

| M3 Component | M4 Replacement | Adapter Bridge |
|---|---|---|
| `ISkillRegistry` | MAF `IToolSet` | `IToolSetAdapter` → `MafToolSetImpl` |
| `IToolRegistry` | MAF function tools pipeline | `IFunctionToolAdapter` → `MafFunctionToolImpl` |
| `IHermesChatService` | MAF `IAgentOrchestrator` | `IAgentOrchestratorAdapter` → `MafOrchestratorImpl` |

### 2.2 MAF Primitives Assumed

Based on the M1 R1 integration-drift checkpoint (GREEN, 2026-05-22), the MAF surface that M4 will consume is:

- **`IAgentOrchestrator`** — accepts an `IAgentRequest`, emits token events, drives multi-agent delegation.
- **`IToolSet`** — flat list of named tools with typed parameters; consumed by the orchestrator to enumerate available capabilities.
- **Function tools** — strongly-typed callables with JSON-schema parameter contracts; invoked by the orchestrator during a turn.
- **`IContext` / `IAgentRequest`** — request envelope that carries identity, conversation history, and custom properties.

### 2.3 Session Context in MAF Model

MAF `IContext` is the M4 equivalent of the M3 explicit `(profileId, sessionId)` parameter pair. The carrier pattern is defined in Section 3.

---

## 3. Session Context Threading — The Carrier Pattern

### 3.1 Design Principle

The M3 approach (explicit string parameters) cannot map directly to MAF's `IContext` model without a deliberate carrier definition. The carrier pattern solves this by attaching `ProfileId` and `SessionId` as typed custom properties on `IContext` at the HTTP boundary and reading them back at every adapter call site.

The invariant: **`SessionContext` is constructed once at the HTTP request boundary and flows immutably through every adapter layer. Adapters read it; they never construct or mutate it.**

### 3.2 Carrier Definition

```
HermesContextKeys (static class)
  ├── ProfileId  = "hermes.profile.id"     (string, required, non-empty)
  └── SessionId  = "hermes.session.id"     (string, required, non-empty, FK to ProfileId)
```

These keys are stored as custom properties on MAF `IContext`. The values are the raw profile and session identifier strings carried verbatim from the inbound HTTP request.

### 3.3 MAF IContext Constructor Arguments

When constructing the MAF `IContext` at the HTTP boundary:

```csharp
// M4 implementation sketch (not M3 code)
var ctx = new MafAgentContext(
    user: new MafUser
    {
        Id   = httpRequest.UserId,     // authenticated user identity (JWT sub)
        Name = httpRequest.UserName    // display name; optional
    },
    customProperties: new Dictionary<string, object>
    {
        [HermesContextKeys.ProfileId] = profileId,   // from route/query/body
        [HermesContextKeys.SessionId] = sessionId    // from route/query/body
    }
);
```

`MafUser.Id` is the authenticated identity used by MAF's own auth layer. It is **separate** from `ProfileId`: a single authenticated user may own multiple Hermes profiles. The `ProfileId` is a Hermes-domain FK; `MafUser.Id` is an auth-domain principal.

### 3.4 SessionContext Extraction Helper

```csharp
// HermesSessionContextExtractor (M4 infrastructure helper)
public static SessionContext Extract(IContext mafContext)
{
    var profileId = mafContext.CustomProperties[HermesContextKeys.ProfileId] as string
        ?? throw new InvalidOperationException("ProfileId missing from MAF context.");
    var sessionId = mafContext.CustomProperties[HermesContextKeys.SessionId] as string
        ?? throw new InvalidOperationException("SessionId missing from MAF context.");
    return new SessionContext { ProfileId = profileId, SessionId = sessionId };
}
```

This helper is called by every adapter implementation that needs the `SessionContext`. It fails fast if the carrier was not populated — which is a programming error, not a runtime condition.

### 3.5 Request Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│  HTTP POST /chat?profileId=p1&sessionId=s1                          │
│  (or Authorization header decoded by middleware)                    │
└────────────────────────┬────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  HermesChatController (M4)                                          │
│  ─ validates profileId, sessionId (non-empty)                       │
│  ─ constructs IAgentRequest with:                                   │
│       message  = request.Body.Message                               │
│       IContext = { ProfileId = p1, SessionId = s1 }                 │
└────────────────────────┬────────────────────────────────────────────┘
                         │  IAgentRequest (message + IContext)
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  PolicyEnginePipeline (T36 — Ash)                                   │
│  ─ BEFORE orchestration: input policy check                         │
│  ─ reads ProfileId from IContext → fetches profile policy set       │
│  ─ may rewrite or reject request (see Section 5)                    │
└────────────────────────┬────────────────────────────────────────────┘
                         │  validated IAgentRequest
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  IAgentOrchestratorAdapter.OrchestrateAsync(request, context)       │
│  ─ extracts SessionContext from IContext via HermesSessionContext-   │
│    Extractor                                                        │
│  ─ calls IAgentOrchestrator.RunAsync(agentRequest)  [MAF call]      │
│  ─ wraps MAF response in AgentResponse with original SessionContext │
└────────────────────────┬────────────────────────────────────────────┘
                         │  (during orchestration, MAF invokes tools)
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  IFunctionToolAdapter (for each tool call)                          │
│  ─ MAF invokes tool → adapter intercepts                            │
│  ─ reads SessionContext from IContext to validate profile scope     │
│  ─ calls IPolicyEngine.ValidateFunctionCall(toolName, args, profile)│
│  ─ if allowed: executes tool, returns result                        │
│  ─ if denied: returns policy-rejection envelope (no exception)      │
└────────────────────────┬────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  IToolSetAdapter (skill lookup during orchestration)                │
│  ─ MAF calls IToolSet.GetToolsAsync()                               │
│  ─ adapter calls ISkillRegistry.GetSkillsForProfile(profileId)      │
│  ─ returns only the skills visible to the active profile            │
└────────────────────────┬────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  SessionStore.SaveTurnAsync(profileId, sessionId, turn)             │
│  ─ called by orchestrator adapter after turn completes              │
│  ─ ProfileId FK enforced at persistence layer                       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 4. Migration Steps

The migration is split into three sequential phases. Each phase must be gated by all existing tests green before the next phase begins.

### Phase 1 — Swap SkillRegistry → MAF IToolSet

**Goal:** Replace `ISkillRegistry` resolution with a MAF `IToolSet` backed by `IToolSetAdapter`.

**Steps:**
1. Implement `MafToolSetImpl : IToolSet` that delegates to `IToolSetAdapter.ProjectToolSetAsync()`.
2. Register `MafToolSetImpl` in the MAF host's DI container, wrapping the existing `ISkillRegistry` singleton.
3. Add a profile filter to `ProjectToolSetAsync`: the skill list returned must be filtered to skills tagged for the active profile (uses the `Category` field on `SkillDescriptor`; profile-level skill visibility rules defined in M4 profile schema).
4. Verify TC-01 through TC-10 pass with real implementation (remove `Skip` attributes).
5. Verify all 262 M3 tests still pass (no regressions).

**Risk gate:** If profile-level skill filtering does not exist in M3 `ISkillRegistry`, add `GetSkillsForProfile(profileId)` as a new method before Phase 1 completes. Do not use `ListSkillsAsync()` unfiltered — that would violate R2.

**Rollback:** Re-register the `SkillRegistryStub` adapter and revert the DI registration. No schema change.

### Phase 2 — Swap ToolRegistry → MAF Function Tools

**Goal:** Replace `IToolRegistry` invocation with MAF's function tool pipeline, backed by `IFunctionToolAdapter`.

**Steps:**
1. Implement `MafFunctionToolImpl` that registers each `MafFunctionToolDescriptor` as a MAF function tool delegate.
2. The function tool delegate wrapper must:
   a. Extract `SessionContext` from the ambient `IContext` before executing.
   b. Call `IPolicyEngine.ValidateFunctionCall(toolName, args, profileId)` — reject if policy fails.
   c. Forward allowed invocations to the existing tool executor.
   d. Emit an audit log entry regardless of allow/deny outcome (fed to `IToolRegistry.AuditLog` or its M4 replacement).
3. Enforce the M2 safe-category whitelist via `IFunctionToolAdapter.IsCategoryAllowed()` at registration time — denied-category tools are registered but immediately return a policy-rejection envelope when invoked.
4. Verify TC-11 through TC-20 pass with real implementation.
5. Verify policy engine integration tests (Section 5) pass.

**Risk gate:** The MAF function tool invocation model must support synchronous pre-invocation hooks. If it does not, use the MAF middleware pipeline as an alternative hook point (see Risk R-04 in Section 7).

**Rollback:** Re-register `FunctionToolAdapterStub` stubs and revert tool delegates. No schema change.

### Phase 3 — Swap HermesChatService → MAF IAgentOrchestrator

**Goal:** Replace `IHermesChatService` with a full MAF `IAgentOrchestrator` backed by `IAgentOrchestratorAdapter`.

**Steps:**
1. Implement `MafOrchestratorImpl : IAgentOrchestrator` that wraps the real MAF `IAgentOrchestrator`.
2. `IAgentOrchestratorAdapter.OrchestrateAsync` becomes a thin wrapper: construct `IAgentRequest`, call `IAgentOrchestrator.RunAsync`, extract `AgentResponse`, echo `SessionContext`.
3. `IAgentOrchestratorAdapter.StreamTokensAsync` maps to `IAgentOrchestrator.StreamAsync`, wrapping each MAF token event in a `TokenEvent` with the original `SessionContext` attached.
4. Multi-agent support: if MAF delegates to a sub-agent, the `SessionContext` must be forwarded via the `IContext.CustomProperties` carrier (Section 3) to the sub-agent's context. Sub-agent responses must carry the same `ProfileId`.
5. Verify TC-21 through TC-30 and R2-01 through R2-05 pass with real implementation.
6. Full regression run: all 262+ tests green.

**Rollback:** Re-register `AgentOrchestratorAdapterStub`. No schema change.

---

## 5. Policy Engine Integration with MAF

### 5.1 Where IPolicyEngine Sits

The policy engine (Ash's T36 work) integrates at **two points** in the MAF pipeline:

**Point A — Before IAgentOrchestrator (Input Policy):**
- Runs on the inbound `IAgentRequest` before the orchestrator sees it.
- Validates: input length, banned patterns, profile-level restrictions on topics.
- May **rewrite** the request (input redaction — see 5.3) or **reject** it with a structured error.
- Implementation: MAF middleware pipeline, registered before the orchestrator middleware.

**Point B — Before Each Function Tool Execution (Function Policy):**
- Runs on each tool call intercepted by `IFunctionToolAdapter`.
- Validates: tool name allowed for profile, argument values not violating data-access policies, category whitelist enforcement.
- May **deny** the tool call (returns policy-rejection envelope; never throws).
- Audit log entry emitted on every evaluation (allow or deny).
- Implementation: function tool delegate wrapper (Phase 2, Step 2b).

**Not** at the response output stage in M4 initial implementation — output filtering is a post-M4 extension point.

### 5.2 Policy Validation Hook in Function Tool Pipeline

```
MAF requests tool execution
    │
    ▼
MafFunctionToolDelegate (wrapper around IFunctionToolAdapter)
    │
    ├── Step 1: Extract SessionContext from IContext
    │
    ├── Step 2: IPolicyEngine.ValidateFunctionCall(
    │               toolName:  descriptor.Name,
    │               args:      invocationArgs,
    │               profileId: context.ProfileId
    │           )
    │
    ├── Step 3 (if ALLOWED):
    │       IToolRegistry.ValidateToolInvocation(toolName, args)  → audit log
    │       Execute tool → return result
    │
    └── Step 4 (if DENIED):
            Emit audit log entry (denied)
            Return PolicyRejectionResult { Reason = policyDecision.Reason }
            (MAF receives a structured error, not an exception)
```

### 5.3 Redaction in MAF — Input Rewriting

Input redaction (replacing PII or banned content before the model sees it) is implemented as a MAF middleware pipeline step registered **before** the orchestrator middleware:

```
HTTP request
    → AuthMiddleware
    → HermesProfileMiddleware       ← populates IContext with ProfileId/SessionId
    → PolicyInputRedactionMiddleware ← rewrites IAgentRequest.Message
    → PolicyInputValidationMiddleware ← rejects if still invalid after redaction
    → MafOrchestratorMiddleware     ← orchestrator sees clean message
```

The `PolicyInputRedactionMiddleware` calls `IPolicyEngine.RedactInput(message, profileId)` and replaces `IAgentRequest.Message` with the redacted version. If the engine returns `RedactionResult.Reject`, the middleware short-circuits with a `403 PolicyViolation` response before the orchestrator is invoked.

---

## 6. Test Strategy for M4

### 6.1 Parker's 35 Skeleton Tests — Classification

All 35 skeleton tests are currently marked `Skip = "M4 skeleton — not yet implemented"`. M4 removes `Skip` attributes as each phase completes.

#### Phase 1 Tests (TC-01 to TC-10) — Adapter Correctness: Mapping

| Test | Validates | Phase |
|---|---|---|
| TC-01 | Skill → `MafToolDescriptor` mapping (name, sourceId, description) | 1 |
| TC-02 | `ToMafToolName` normalises uppercase | 1 |
| TC-03 | `ToMafToolName` replaces spaces with dashes | 1 |
| TC-04 | `ToMafToolName` replaces special chars | 1 |
| TC-05 | Empty skill ID throws `ArgumentException` | 1 |
| TC-06 | Metadata forwarded verbatim | 1 |
| TC-07 | Null metadata → empty dictionary | 1 |
| TC-08 | `ProjectToolSetAsync` returns one descriptor per skill | 1 |
| TC-09 | Empty registry → empty list | 1 |
| TC-10 | `ValidateToolSetMappingAsync` flags empty descriptions | 1 |

#### Phase 2 Tests (TC-11 to TC-20) — Adapter Correctness: Function Tools

| Test | Validates | Phase |
|---|---|---|
| TC-11 | `ToolDefinition` → `MafFunctionToolDescriptor` (name, description) | 2 |
| TC-12 | ReadFile category → `IsAllowed = true` | 2 |
| TC-13 | WriteFile category → `IsAllowed = false` | 2 |
| TC-14 | ExecuteCommand category → `IsAllowed = false` | 2 |
| TC-15 | Network category → `IsAllowed = false` | 2 |
| TC-16 | All M2 safe categories → `IsAllowed = true` | 2 |
| TC-17 | Parameters forwarded (name, type, required) | 2 |
| TC-18 | `FilterAllowed` removes denied tools | 2 |
| TC-19 | `ProjectFunctionToolsAsync` returns all categories (with `IsAllowed` flags) | 2 |
| TC-20 | No parameters → empty `Parameters` list | 2 |

#### Phase 3 Tests (TC-21 to TC-30) — MAF Compatibility: Token Events + Streaming

| Test | Validates | Phase |
|---|---|---|
| TC-21 | `OrchestrateAsync` echoes `SessionContext` in response | 3 |
| TC-22 | `OrchestrateAsync` delegates to `IHermesChatService.ChatAsync` | 3 |
| TC-23 | `StreamTokensAsync` emits one `TokenEvent` per token | 3 |
| TC-24 | Only last token has `IsFinal = true` | 3 |
| TC-25 | Every token event carries unchanged `SessionContext` | 3 |
| TC-26 | Empty `ProfileId` → `ValidateSessionContext` false | 3 |
| TC-27 | Empty `SessionId` → `ValidateSessionContext` false | 3 |
| TC-28 | Valid context → `ValidateSessionContext` true | 3 |
| TC-29 | Invalid context → `OrchestrateAsync` throws `InvalidOperationException` | 3 |
| TC-30 | Invalid context → `StreamTokensAsync` throws before yielding | 3 |

#### R2 Isolation Tests (R2-01 to R2-05) — Critical Gate: Profile Isolation Persists

| Test | Validates | Phase |
|---|---|---|
| R2-01 | `ProfileId` survives full orchestration round-trip (exact bytes) | 3 |
| R2-02 | Two profiles: no context cross-contamination | 3 |
| R2-03 | Streaming: two profiles' token events carry correct `ProfileId` | 3 |
| R2-04 | Null context → `ValidateSessionContext` false (no exception) | 3 |
| R2-05 | `SessionContext` is passed by reference (not copied with mutation risk) | 3 |

**Critical gate:** R2-01 through R2-05 are the **last gate before M4 ships**. All five must pass with the real MAF implementation, not just the stub. These tests are the contractual proof that R2 isolation survives the MAF migration.

### 6.2 New Tests Required for M4 (Not in Parker's 35)

The 35 skeletons cover mapping, category filtering, token events, and R2 isolation. M4 requires additional test categories that Parker's skeletons did not anticipate:

**Context Threading Tests** (new, Phase 3):
- `IContext` custom properties survive multi-agent sub-delegation.
- `HermesSessionContextExtractor` throws `InvalidOperationException` when `ProfileId` key is absent.
- `HermesSessionContextExtractor` throws when `SessionId` key is absent.
- Parallel orchestrations with different profiles produce isolated `SessionContext` instances.

**Policy Engine Integration Tests** (new, Phase 2, cross-cutting with T36):
- Denied function tool call returns `PolicyRejectionResult`, does not throw.
- Policy engine called before `IToolRegistry.ValidateToolInvocation`.
- Redaction middleware rewrites message; model receives redacted version.
- Rejected input at policy layer returns `403 PolicyViolation` before orchestrator runs.
- Audit log receives an entry for every policy evaluation (allow and deny).

**Multi-Agent Tests** (new, Phase 3):
- Sub-agent receives same `ProfileId` as parent agent.
- Sub-agent receives same `SessionId` as parent agent.
- Sub-agent response carries `SessionContext` with original `ProfileId`.

---

## 7. Risk Register and Mitigations

### R-01: MAF IContext Custom Properties Not Supported at Sub-Agent Boundary

**Risk:** MAF may not propagate `IContext.CustomProperties` when delegating to a sub-agent. The `ProfileId` carrier would be silently dropped, violating R2.

**Severity:** Critical (R2 gate)  
**Probability:** Medium  
**Mitigation:**
1. Spike in M4 Week 1: stand up a two-agent MAF test harness and verify `IContext.CustomProperties` round-trip.
2. If propagation is absent: implement a `HermesContextPropagationMiddleware` that intercepts every sub-agent handoff and re-stamps `ProfileId`/`SessionId` on the child context.
3. Test coverage: multi-agent `SessionContext` propagation tests (Section 6.2).

### R-02: MAF Token Event Model Incompatible with `IsFinal` Semantics

**Risk:** MAF's native streaming model may not emit a clear "final token" signal, making it impossible to set `IsFinal = true` on the last `TokenEvent` without buffering the entire stream.

**Severity:** Medium (breaks TC-24)  
**Probability:** Medium  
**Mitigation:**
1. Spike: investigate MAF streaming events for end-of-stream marker (event type, property, or stream completion).
2. If no marker: buffer all tokens in `StreamTokensAsync`, mark last on flush — acceptable for M4 given current token volumes.
3. If buffering is unacceptable at scale: introduce `IsFinal = false` for all events and add a separate `StreamEndEvent` type; update TC-24 contract.
4. Test coverage: TC-23, TC-24, TC-25 validate this contract.

### R-03: SkillRegistry Profile Filtering Not Implemented

**Risk:** `ISkillRegistry.ListSkillsAsync()` returns all skills regardless of profile. Phase 1 requires `GetSkillsForProfile(profileId)`. If this method is absent, the `IToolSet` will expose all skills to all profiles — a hard R2 violation.

**Severity:** Critical (R2 gate)  
**Probability:** High (method does not currently exist in `ISkillRegistry`)  
**Mitigation:**
1. Before Phase 1 begins: add `GetSkillsForProfile(profileId)` to `ISkillRegistry` and `SkillRegistry`.
2. Default implementation: return all skills (safe for single-profile deployments); profile-level skill visibility is a M4 profile-schema feature.
3. Add a corresponding unit test: `GetSkillsForProfile_FiltersByProfile_ReturnsOnlyVisibleSkills`.
4. This is a **Phase 1 prerequisite**; Phase 1 is blocked until this is green.

### R-04: MAF Function Tool Pipeline Does Not Support Pre-Execution Hooks

**Risk:** If MAF's function tool invocation is opaque (no middleware, no delegate wrapper pattern), `IPolicyEngine.ValidateFunctionCall` cannot intercept tool calls before execution.

**Severity:** High (policy gate)  
**Probability:** Low (MAF is expected to support delegate patterns based on M1 R1 review)  
**Mitigation:**
1. Spike: verify MAF function tool registration allows a delegate wrapper (Phase 2, Step 1).
2. If no delegate hook: use MAF middleware pipeline with a `ToolCallInterceptorMiddleware` registered before the function tool runner.
3. If middleware is also absent: wrap each `ToolDefinition` in a proxy that calls the policy engine before delegating — implemented entirely in Hermes without MAF extension points.
4. Document chosen pattern in Phase 2 implementation notes.

### R-05: OTel Instrumentation Lost at Adapter Boundary

**Risk:** The M2 OTel baseline (P95 48ms, `hermes.chat.turn` → `hermes.provider.call` spans) may be broken when the orchestrator is replaced by MAF. MAF may not emit compatible spans or may not propagate the `ActivityContext`.

**Severity:** Medium (OTel gate, M4 quality metric)  
**Probability:** Medium  
**Mitigation:**
1. Before Phase 3: verify that MAF propagates W3C Trace Context headers across its internal pipeline.
2. If propagation is absent: add `HermesTelemetryMiddleware` that starts a `hermes.chat.turn` activity before the orchestrator and stops it after the response.
3. Re-run the M1 OTel baseline test after Phase 3 completes; fail M4 if P95 regresses beyond 150ms (50% slack over M1 baseline of 100ms target).
4. Test: add an OTel span-continuity test to Phase 3 test suite.

### R-06: Session Persistence FK Violated by MAF Turn Completion Order

**Risk:** MAF may complete a turn and emit a response before Hermes has persisted the turn to `SessionStore`. If the response is read by the client before persistence completes, the session history is out of sync.

**Severity:** Medium (data integrity)  
**Probability:** Low  
**Mitigation:**
1. `SessionStore.SaveTurnAsync` must be called and awaited **inside** `OrchestrateAsync` before returning `AgentResponse`.
2. `StreamTokensAsync` saves turn at stream completion (after `IsFinal = true` token emitted).
3. Test: add a turn-persistence ordering test — `SaveTurnAsync` must be called before `OrchestrateAsync` returns.

### R-07: Category Whitelist Enforcement Lost During MAF Tool Registration

**Risk:** If `MafFunctionToolImpl` registers all tools without calling `IsFunctionToolAdapter.IsCategoryAllowed()`, denied tools (WriteFile, ExecuteCommand, Network) will be invocable by MAF.

**Severity:** Critical (M2 safe-category contract)  
**Probability:** Low  
**Mitigation:**
1. `IsFunctionToolAdapter.FilterAllowed()` must be called at registration time (not just at runtime).
2. Denied-category tools are registered with an immediate-rejection delegate (returns `CategoryDeniedResult` — no execution).
3. TC-13, TC-14, TC-15, TC-16, TC-18 cover this contract; they are Phase 2 gates.

### R-08: MAF Multi-Model Support Breaks Single-Provider Assumption

**Risk:** MAF may route requests to a different model provider than Hermes expects (e.g., MAF picks GPT-4 when Hermes is configured for Ollama). The `IChatClient` abstraction is bypassed.

**Severity:** Medium (provider isolation)  
**Probability:** Medium  
**Mitigation:**
1. In Phase 3, configure MAF's model provider to delegate exclusively to Hermes' `IChatClient` — MAF is a routing shell, not a model picker.
2. Add a `ChatClientProviderBinding` that locks MAF to the `IConfiguration["Provider"]` value established in M1.
3. Test: verify that `OrchestrateAsync` still uses the configured provider after Phase 3.

### R-09: Adapter Stub Removal Breaks M3 DI Registration

**Risk:** When stub implementations are replaced in M4, DI registrations in `Program.cs`/`HermesHost` may still reference the stubs. Compilation will succeed but the wrong implementation will run.

**Severity:** Medium (configuration correctness)  
**Probability:** High (stubs are registered by type name)  
**Mitigation:**
1. All three stubs are registered by interface, not concrete type, in M3: `services.AddSingleton<IAgentOrchestratorAdapter, AgentOrchestratorAdapterStub>()`.
2. M4 replaces the right-hand side at one callsite per interface.
3. Add a startup health-check that verifies the registered implementation is the M4 implementation (not a stub) when `Environment = "Production"`.

### R-10: R2 Isolation Not Verified in Streaming Path Under Concurrent Load

**Risk:** R2-03 (streaming isolation between two profiles) is tested sequentially. Under concurrent load, two streaming sessions with different profiles might share a mutable buffer, leaking tokens across profiles.

**Severity:** Critical (R2 gate)  
**Probability:** Low (current stub uses a local buffer per call)  
**Mitigation:**
1. `StreamTokensAsync` implementation must use a per-call local buffer (`var tokens = new List<string>()`), never a class-level field.
2. Add a concurrent-streaming integration test: two parallel `StreamTokensAsync` calls with different profiles; verify zero cross-contamination at 100 concurrent pairs.
3. This test is a M4 load-test prerequisite before shipping.

---

## 8. M4 Start Prerequisites

The following conditions must be true before the M4 refactor begins. These are verified by Ripley at M4 kickoff.

### Mandatory Prerequisites

| Prerequisite | Owner | Verification |
|---|---|---|
| All 262 M3 tests green (0 failures) | Ripley | `dotnet test` with no failures |
| `ISkillRegistry.GetSkillsForProfile(profileId)` implemented and tested | Dallas | Unit test passing |
| `IPolicyEngine` interface defined (T36 complete) | Ash | Interface checked into `src/Hermes.Core/` |
| MAF NuGet package version locked in `Directory.Packages.props` | Ripley | Pinned version, no floating reference |
| Adapter stub tests (TC-01 to R2-05) all present and `Skip` attributed | Parker | Tests compile and appear in test runner |
| M4 branch created from M3B HEAD | Ripley | `git branch m4-maf-migration` |
| Phase 1/2/3 rollback plan documented per this spec | Ripley | This document |

### Recommended Prerequisites

| Prerequisite | Owner | Verification |
|---|---|---|
| MAF spike: `IContext.CustomProperties` sub-agent propagation verified | Ripley | Spike result in `docs/research/` |
| MAF spike: streaming `IsFinal` marker behaviour documented | Parker | Spike result in `docs/research/` |
| MAF spike: function tool delegate wrapper pattern verified | Ash | Spike result in `docs/research/` |
| OTel instrumentation compatible with MAF pipeline verified | Parker | Span output visible in test harness |
| Load test baseline re-established for M3B (before adapter swap) | Parker | `test-coverage.log` updated |

### Hard Blockers (Stops M4 Until Resolved)

1. **R2-01 to R2-05 fail with stub implementation** — means the stub itself is broken; fix before M4.
2. **`IPolicyEngine` interface not available** — Phase 2 depends on it; no interface = no function tool policy hook.
3. **MAF package does not compile on `net9.0`** — must resolve version conflict before any M4 work begins.

---

## 9. Compatibility Layer Summary

The compatibility layer is the full set of adapter stubs + `SessionContext` carrier + `HermesContextKeys` constants. It exists in `src/Hermes.Adapters/` and is the only code that knows about both the M3 custom surface and the M4 MAF surface simultaneously. Everything else talks to one side only.

| Compatibility Component | Purpose | Removed in |
|---|---|---|
| `AgentOrchestratorAdapterStub` | Pass-through `IHermesChatService` → `IAgentOrchestratorAdapter` | Phase 3 complete |
| `FunctionToolAdapterStub` | Pass-through `IToolRegistry` → `IFunctionToolAdapter` | Phase 2 complete |
| `ToolSetAdapterStub` | Pass-through `ISkillRegistry` → `IToolSetAdapter` | Phase 1 complete |
| `SessionContext` record | Identity carrier — **stays forever** | Not removed |
| `TokenEvent` record | Streaming identity carrier — **stays forever** | Not removed |
| `HermesContextKeys` (M4 new) | `IContext` property key constants | Not removed |

`SessionContext`, `TokenEvent`, and `AgentRequest` / `AgentResponse` are **permanent** types. They are the stable API surface of the `Hermes.Adapters` namespace regardless of whether the backend is stubs or real MAF.

---

## 10. Acceptance Criteria Verification

| Criterion | Status | Evidence |
|---|---|---|
| `docs/design/m4-maf-adapter-strategy.md` created (3,000+ words) | ✅ | This document |
| Session context threading specified (carrier pattern + diagram) | ✅ | Sections 3.2, 3.3, 3.4, 3.5 |
| Policy engine integration strategy clear | ✅ | Section 5 (Points A+B, redaction, audit) |
| Test strategy mapped to Parker's 35 skeletons | ✅ | Section 6.1 (all 35 classified by phase) |
| 5–10 risks identified + mitigated | ✅ | Section 7 (R-01 through R-10) |
| M4 start prerequisites documented | ✅ | Section 8 (mandatory + recommended + hard blockers) |
| All existing tests still pass (no M3 code changes) | ✅ | No code changes made; this is a design document only |

---

## Appendix A: Adapter Namespace Map

```
src/
  Hermes.Adapters/
    IAgentOrchestratorAdapter.cs   ← permanent interface
    IFunctionToolAdapter.cs        ← permanent interface
    IToolSetAdapter.cs             ← permanent interface
    AgentOrchestratorAdapterStub.cs ← removed after Phase 3
    FunctionToolAdapterStub.cs     ← removed after Phase 2
    ToolSetAdapterStub.cs          ← removed after Phase 1
    SessionContext.cs              ← (currently inline in IAgentOrchestratorAdapter.cs)
    AgentRequest.cs                ← (currently inline)
    AgentResponse.cs               ← (currently inline)
    TokenEvent.cs                  ← (currently inline)
    [M4 NEW] HermesContextKeys.cs
    [M4 NEW] HermesSessionContextExtractor.cs
    [M4 NEW] MafToolSetImpl.cs
    [M4 NEW] MafFunctionToolImpl.cs
    [M4 NEW] MafOrchestratorImpl.cs
```

---

*Document prepared by Ripley (Lead)*  
*T37 — MAF Adapter Design Document*  
*Co-authored by Copilot*  
*M3C sprint — 2026-05-22*
