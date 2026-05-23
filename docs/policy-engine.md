# Policy Engine — Architecture & Integration Guide

**Milestone:** M3C — T36  
**Owner:** Ash (security/policy lead)  
**Status:** ✅ Implemented  
**Date:** 2026-05-22

---

## Overview

The Hermes Policy Engine enforces safety guardrails at the skill, tool, memory, and request layers. It runs on the custom stack (`ISkillRegistry` + `ToolRegistry`), consistent with M3-001 (MAF deferred). Every evaluation call produces an immutable audit entry regardless of verdict.

---

## Architecture

```
IPolicyEngine
    ├── ValidateSkill(ISkillDefinition) → PolicyResult
    ├── ValidateToolRequest(ToolCategory, input) → PolicyResult
    ├── EvaluateMemoryAccess(requestingProfileId, targetProfileId, MemoryScope) → PolicyResult
    ├── CheckRateLimit(profileId, sessionId) → PolicyResult
    └── AuditLog: IReadOnlyList<PolicyAuditEntry>

PolicyEngine (concrete)
    ├── SkillDenylistPolicy     — loads config/policies/skill-denylist.yaml
    ├── ToolCategoryPolicy      — category allowlist + input redaction
    ├── MemoryAccessPolicy      — R2 profile isolation
    └── RateLimitPolicy         — sliding window per profile / session
```

### Verdict Types

| Verdict | Meaning | Caller action |
|---------|---------|---------------|
| `Allow` | Request is permitted | Proceed normally |
| `Deny` | Request is blocked | Throw `PolicyViolationException` |
| `Redact` | Permitted after sanitisation | Replace input with `PolicyResult.RedactedInput` |

---

## Core Policies

### 1. Skill Denylist (`SkillDenylistPolicy`)

Loads rules from `config/policies/skill-denylist.yaml` at construction time. If the file is absent, the policy is permissive (Allow all).

**Evaluation order (first Deny wins):**

1. **ID match** — skill ID or Name (fallback) is in `denied_ids`
2. **Tag match** — any tag in `Metadata["tags"]` is in `denied_tags`
3. **Author match** — `Metadata["author"]` is in `denied_authors`

**Config format:**

```yaml
denied_ids:
  - system-shell-execute
  - network-fetch-raw

denied_tags:
  - dangerous
  - privileged
  - unsafe

denied_authors:
  - untrusted-author
```

All comparisons are case-insensitive. Tags are comma-separated values in the `tags` metadata field.

---

### 2. Tool Category (`ToolCategoryPolicy`)

Controls which `ToolCategory` values may be executed and scans inputs for sensitive data.

**Safe categories (Allow):**
- `ReadFile`
- `SystemInfo`
- `TextProcessing`

**Denied categories (always Deny):**
- `ExecuteCommand`
- `Network`
- `WriteFile`
- `Delete`
- `Unknown`

**Input redaction patterns** (applied when category is safe):

| Pattern | Placeholder |
|---------|-------------|
| `Bearer <token>` | `[REDACTED:TOKEN]` |
| `api_key=<value>`, `apitoken=<value>` | `[REDACTED:API_KEY]` |
| `password=<value>` | `[REDACTED:PASSWORD]` |
| `secret=<value>`, `private_key=<value>` | `[REDACTED:SECRET]` |
| AWS key patterns (`AKIA…`) | `[REDACTED:AWS_KEY]` |
| Long hex tokens (32+ chars) | `[REDACTED:HEX_TOKEN]` |

When redaction fires, the verdict is `Redact` and `PolicyResult.RedactedInput` contains the sanitised string.

---

### 3. Memory Access (`MemoryAccessPolicy`)

Enforces **R2 isolation** — a profile may only access its own memory.

| Scope | Same profile | Cross-profile |
|-------|-------------|---------------|
| `Profile` | ✅ Allow | ❌ Deny |
| `Session` | ✅ Allow | ❌ Deny |
| `Global` | ❌ Deny | ❌ Deny |

`Global` scope is unconditionally denied in M3. Cross-profile access of any scope is denied. Profile ID comparison is case-insensitive.

---

### 4. Rate Limiting (`RateLimitPolicy`)

Sliding 1-hour window counters per profile and per session.

| Window | Default limit |
|--------|--------------|
| Per profile / hour | 1,000 requests |
| Per session / hour | 500 requests |

Both limits are configurable at construction time. Independent counters per profile ensure one profile exhausting its limit does not affect others.

---

## Integration Points

### `HermesChatService.StreamChatAsync`

Rate limit check is enforced before any chat processing:

```csharp
var rateLimitResult = _policyEngine.CheckRateLimit(profileId, sessionId);
if (rateLimitResult.Verdict == PolicyVerdict.Deny)
    throw new PolicyViolationException(rateLimitResult);
```

### Skill execution (recommended pattern)

```csharp
var result = _policyEngine.ValidateSkill(skill);
if (result.Verdict == PolicyVerdict.Deny)
    throw new PolicyViolationException(result);
// proceed with skill execution
```

### Tool execution (recommended pattern)

```csharp
var result = _policyEngine.ValidateToolRequest(tool.Category, serialisedInput);
if (result.Verdict == PolicyVerdict.Deny)
    throw new PolicyViolationException(result);
var effectiveInput = result.Verdict == PolicyVerdict.Redact
    ? result.RedactedInput!
    : serialisedInput;
// proceed with effectiveInput
```

### Memory access (recommended pattern)

```csharp
var result = _policyEngine.EvaluateMemoryAccess(requestingId, targetId, MemoryScope.Profile);
if (result.Verdict == PolicyVerdict.Deny)
    throw new PolicyViolationException(result);
// proceed with memory read/write
```

---

## Audit Trail

Every call to any `IPolicyEngine` method appends a `PolicyAuditEntry` to `AuditLog`:

```csharp
public sealed class PolicyAuditEntry
{
    public DateTimeOffset Timestamp { get; }
    public string PolicyType { get; }   // "SkillDenylist" | "ToolCategory" | "MemoryAccess" | "RateLimit"
    public string Subject { get; }      // skill ID, tool category, "profileA->profileB:scope"
    public PolicyVerdict Verdict { get; }
    public string Reason { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
```

The log is append-only and thread-safe. **Both Allow and Deny decisions are logged** — the audit trail is a complete record of all policy decisions, not just violations.

---

## DI Registration

```csharp
services.AddSingleton<IPolicyEngine>(sp =>
    new PolicyEngine(
        denylistYamlPath: "config/policies/skill-denylist.yaml",
        maxRequestsPerHour: 1_000,
        maxRequestsPerSession: 500));
```

`IPolicyEngine` may be injected into `HermesChatService`, skill executors, and tool dispatchers.

---

## Test Coverage

**61 adversarial tests** in `tests/Hermes.Core.Tests/Policy/PolicyEngineT36Tests.cs`:

| Section | Count | Focus |
|---------|-------|-------|
| Skill Denylist | 10 | ID deny, tag deny, author deny, case-insensitivity, missing file |
| Tool Category | 15 | Safe/denied categories, redaction patterns, edge cases |
| Memory Isolation | 10 | Same-profile allow, cross-profile deny, global scope deny |
| Rate Limiting | 6 | Threshold, session limit, profile isolation, empty ID |
| Error Handling | 11 | Null args, malformed YAML, exception types, result factories |
| Audit Trail | 5 | Entry count, policy type labels, both verdicts logged |
| Integration | 4 | End-to-end: violation exception, redaction, audit on deny |

All 311 `Hermes.Core.Tests` pass. Zero regressions.

---

## Files

```
src/Hermes.Core/Policy/
    IPolicyEngine.cs             — Interface (M3C public contract)
    PolicyVerdict.cs             — Verdict enum (Allow/Deny/Redact)
    PolicyResult.cs              — Immutable result record + factories
    PolicyViolationException.cs  — Exception for Deny enforcement
    MemoryScope.cs               — Scope enum (Profile/Session/Global)
    PolicyAuditEntry.cs          — Audit log entry
    PolicyEngine.cs              — Concrete implementation
    SkillDenylistPolicy.cs       — Skill ID/tag/author denylist
    ToolCategoryPolicy.cs        — Category allowlist + redaction
    MemoryAccessPolicy.cs        — R2 profile isolation
    RateLimitPolicy.cs           — Sliding-window rate limiter

config/policies/
    skill-denylist.yaml          — Default denylist configuration

tests/Hermes.Core.Tests/Policy/
    PolicyEngineT36Tests.cs      — 61 adversarial tests
```
