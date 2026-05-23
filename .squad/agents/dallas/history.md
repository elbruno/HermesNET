# Dallas History — HermesNET Infrastructure/Profiles/Sessions

**Current Focus:** M2 Infrastructure - REST API (T19)

## Active Work Summary

### T19 Completion (M2-T19, 2026-05-22)
- **Hermes.Host** converted from class library to `Microsoft.NET.Sdk.Web` ASP.NET Core Web API
- **IHermesChatService** extended with `StreamChatAsync(message, profileId, sessionId, ct)` → `IAsyncEnumerable<string>`
- **IChatClient** extended with `StreamAsync(messages, ct)` → `IAsyncEnumerable<string>`
- **OllamaClient** implements streaming via Ollama NDJSON streaming API (`stream: true`)
- **OpenAIClient** implements `StreamAsync` as NotImplementedException stub (M3)
- **Controllers** in `src/Hermes.Host/Controllers/`:
  - `ChatController` — `POST /api/chat` with SSE (`event: token`, `event: done`)
  - `SessionsController` — GET/POST/GET{id}/DELETE{id}
  - `ProfilesController` — GET/POST/GET{id}/PUT{id}/DELETE{id}
  - `MemoryController` — `GET /api/profiles/{profileId}/memory`, `GET /api/profiles/{profileId}/user-profile`
- **ErrorHandlingMiddleware** — `KeyNotFoundException`→404, `ArgumentException`→400, `UnauthorizedAccessException`→403, unhandled→500; always returns `{ error, details }` JSON
- **Program.cs** wires DI, CORS for localhost:3000, Swashbuckle OpenAPI, OTel with AspNetCore instrumentation; port 5000
- **OpenAPI docs** generated and committed: `docs/openapi.json` + `docs/openapi.yaml`
- **ISessionService** fixed: added missing `UpdateSessionAsync`, `GetSessionsByProfileAsync`, `ListSessionsAsync` interface declarations (were in implementation but absent from interface)
- **Tests**: 214/215 pass, 1 skipped (`SwitchingProfile_DoesNotAffectOtherProfilesCurrentSession` — M3 feature), 0 errors build

### Integration notes for Lambert (T22)
- Chat SSE: validate profile+session before streaming; `text/event-stream` with `event: token\ndata: "..."\n\n` + `event: done`
- All endpoints at `/api/` prefix (not `/api/v1/`) — update test client base URLs accordingly
- Error body: `{ "error": "...", "details": "..." }`
- Port: 5000 (default); CORS: localhost:3000

### T14 Completion (M2-T14, 2026-05-22)
- **ISessionService additions** (`src/Hermes.Core/Profiles/ISessionService.cs`):
  - `UpdateSessionAsync(id, name)` — update session name
  - `ListSessionsAsync(profileId?)` — list sessions for current or named profile
  - `GetSessionsByProfileAsync(profileId)` — explicit profile isolation query (Parker T15 coordination)
- **`UnauthorizedAccessException`** enforced in `SwitchSessionAsync` for cross-profile (was `InvalidOperationException`)
- **CLI wired**: `hermes session create/list/switch/current` all operational

### T13 Completion Verified (M2-002, 2026-05-22)
- **IProfileService**: CreateProfileAsync, GetProfileAsync, GetProfileByNameAsync, ListProfilesAsync, UpdateProfileAsync, DeleteProfileAsync, SwitchProfileAsync, GetCurrentProfileAsync — all implemented and tested
- **CLI commands**: `hermes profile create/list/switch/current` — all wired

### Design Unknowns Flagged for M3 Planning
1. Skill ID uniqueness scope (global assumed; may need namespacing at 50+ skills)
2. Skill versioning strategy (one version per ID assumed; M3 decision needed)
3. Auth layer (JWT/managed identity) — Ash owns M3

---

### Previous Milestones (M1, Early M2)
See `dallas/history-archive.md` for M1 completion summary, session store implementation, and provider wiring details.