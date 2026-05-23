# Dallas History — HermesNET Infrastructure/Profiles/Sessions

**Current Focus:** M2 Infrastructure - Tools, Skills, Profiles, Sessions

## Active Work Summary

### T18 Completion (M2-T18, 2026-05-22)
- **`IToolRegistry` interface** (`src/Hermes.Core/Tools/IToolRegistry.cs`):
  - `RegisterToolAsync(toolDefinition)` — registers CLI tools only; skills never register tools
  - `GetToolAsync(name)` → `ToolDefinition` | `KeyNotFoundException`
  - `ListToolsByCategory(category)` → `IAsyncEnumerable<ToolDefinition>`
  - `ValidateToolInvocation(name, args)` → `ToolInvocationValidationResult`
  - `AuditLog` — ordered audit trail for M3 policy engine
- **`ToolRegistry` implementation** (`src/Hermes.Core/Tools/ToolRegistry.cs`):
  - Thread-safe `ConcurrentDictionary` for O(1) tool lookup
  - `SafeCategories` whitelist: `ReadFile`, `SystemInfo`, `TextProcessing`
  - Deny-by-default: `WriteFile`, `ExecuteCommand`, `Network`, `Delete`, `Unknown`
  - Path-traversal detection: `..`, `%2e%2e`, `%252e%252e`, backslash variants
  - Path-prefix whitelist enforcement per parameter
  - Input size enforcement (`MaxInputSize` per tool, default 10 240 B)
  - Required-parameter validation; optional params never error
  - Audit entry emitted for every invocation (allowed or denied) — M3 policy hook
- **Supporting types** in `src/Hermes.Core/Tools/`:
  - `ToolCategory.cs`, `ToolDefinition.cs`, `ToolParameter.cs`
  - `ToolAuditEntry.cs`, `ToolInvocationValidationResult.cs`
- **CLI wiring** (`src/Hermes.Cli/Commands/ToolCommand.cs`):
  - `hermes tool list [--category <cat>]` — streams all tools, optionally filtered
  - `hermes tool show <name>` — detailed view with safety status
  - `Program.cs` registers `IToolRegistry` as singleton and wires `ToolCommand`
- **Tests** (`tests/Hermes.Core.Tests/Tools/ToolRegistryTests.cs`): **36/36 passing**
  - Parameterised safe-category tests, all 5 denied-category enforcement tests
  - 5 path-traversal rejection tests, 3 safe-path whitelist acceptance tests
  - Input-size enforcement, required/optional parameter, audit-log coverage
  - Case-insensitive lookup, duplicate registration guard, defaults validation
- **Provider stubs fixed**: Added `StreamAsync` stubs to `OllamaClient` and
  `OpenAIClient` (missing `IChatClient.StreamAsync` implementation from T17/T19 WIP)
- **Pre-existing test failure noted** (not caused by T18):
  `SwitchSession_CrossProfile_Throws` expects `InvalidOperationException` but
  `SessionService.SwitchSessionAsync` throws `UnauthorizedAccessException` (T14 change)

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


