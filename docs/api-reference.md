# HermesNET REST API Reference

Complete endpoint reference for developers integrating with HermesNET.

---

## Base URL

```
http://localhost:5000/api
```

---

## Authentication

Currently no authentication required (development mode).  
Future: OAuth2 with Azure AD (M4+).

---

## OpenAPI / Swagger

Full OpenAPI 3.0 specification:

| Format | URL |
|--------|-----|
| YAML | [`docs/openapi.yaml`](./openapi.yaml) |
| JSON | [`docs/openapi.json`](./openapi.json) |

---

## Endpoints

### Profiles

Profiles are isolated contexts. Each profile has its own sessions and memory.

---

#### `GET /api/profiles`

List all profiles.

**Response `200`:**
```json
[
  {
    "id": "a1b2c3d4-0000-0000-0000-000000000001",
    "name": "dev",
    "description": "Development environment",
    "createdAt": "2026-05-22T23:00:00Z",
    "updatedAt": "2026-05-22T23:00:00Z"
  }
]
```

---

#### `POST /api/profiles`

Create a new profile.

**Request body:**
```json
{
  "name": "dev",
  "description": "Optional description"
}
```

> `name` is required. `description` is optional.

**Response `201`:**
```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000001",
  "name": "dev",
  "description": "Optional description",
  "createdAt": "2026-05-22T23:00:00Z",
  "updatedAt": "2026-05-22T23:00:00Z"
}
```

**Error `400`** (e.g., duplicate name or missing required field):
```json
{
  "error": "Profile with name 'dev' already exists",
  "details": null
}
```

---

#### `GET /api/profiles/{id}`

Get a single profile by ID.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Profile ID (UUID) |

**Response `200`:** Profile object (same shape as POST response)

**Error `404`:**
```json
{
  "error": "Profile not found",
  "details": null
}
```

---

#### `PUT /api/profiles/{id}`

Update a profile.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Profile ID (UUID) |

**Request body:**
```json
{
  "name": "updated-name",
  "description": "Updated description"
}
```

> Both fields are optional. Only provided fields are updated.

**Response `200`:** Updated profile object

**Error `404`:**
```json
{
  "error": "Profile not found",
  "details": null
}
```

---

#### `DELETE /api/profiles/{id}`

Delete a profile.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Profile ID (UUID) |

**Response `204`:** No content

**Error `404`:**
```json
{
  "error": "Profile not found",
  "details": null
}
```

---

### Sessions

Sessions are named conversation containers that belong to a profile.

---

#### `GET /api/sessions`

List sessions, optionally filtered by profile.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `profileId` | string | No | Filter sessions by profile ID |

**Response `200`:**
```json
[
  {
    "id": "s1t2u3v4-0000-0000-0000-000000000001",
    "profileId": "a1b2c3d4-0000-0000-0000-000000000001",
    "name": "Q&A Bot",
    "createdAt": "2026-05-22T23:00:00Z",
    "lastAccessed": "2026-05-22T23:05:00Z",
    "metadata": null
  }
]
```

---

#### `POST /api/sessions`

Create a new session.

**Request body:**
```json
{
  "name": "Q&A Bot",
  "profileId": "a1b2c3d4-0000-0000-0000-000000000001"
}
```

> Both `name` and `profileId` are required.

**Response `201`:**
```json
{
  "id": "s1t2u3v4-0000-0000-0000-000000000001",
  "profileId": "a1b2c3d4-0000-0000-0000-000000000001",
  "name": "Q&A Bot",
  "createdAt": "2026-05-22T23:00:00Z",
  "lastAccessed": "2026-05-22T23:00:00Z",
  "metadata": null
}
```

**Error `400`** (invalid input):
```json
{
  "error": "Session name is required",
  "details": null
}
```

**Error `404`** (profile not found):
```json
{
  "error": "Profile not found",
  "details": null
}
```

---

#### `GET /api/sessions/{id}`

Get a single session by ID.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Session ID (UUID) |

**Response `200`:** Session object (same shape as POST response)

**Error `404`:**
```json
{
  "error": "Session not found",
  "details": null
}
```

---

#### `DELETE /api/sessions/{id}`

Delete a session.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Session ID (UUID) |

**Response `204`:** No content

**Error `404`:**
```json
{
  "error": "Session not found",
  "details": null
}
```

---

### Memory

Memory stores curated Markdown context (`MEMORY.md`) per profile.

---

#### `GET /api/profiles/{profileId}/memory`

Get the memory context for a profile.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `profileId` | string | Profile ID (UUID) |

**Response `200`:**
```json
{
  "profileId": "a1b2c3d4-0000-0000-0000-000000000001",
  "content": "# My Context\n\nI prefer concise responses.",
  "format": "markdown",
  "version": 1,
  "updatedAt": "2026-05-22T23:00:00Z",
  "isEmpty": false
}
```

**Error `400`:**
```json
{
  "error": "Invalid profile ID",
  "details": null
}
```

---

#### `GET /api/profiles/{profileId}/user-profile`

Get the user profile document (`USER.md`) for a profile.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `profileId` | string | Profile ID (UUID) |

**Response `200`:**
```json
{
  "profileId": "a1b2c3d4-0000-0000-0000-000000000001",
  "data": "# User Profile\n\nName: Bruno\nPreferences: ...",
  "schemaVersion": 1,
  "updatedAt": "2026-05-22T23:00:00Z",
  "isEmpty": false
}
```

**Error `400`:**
```json
{
  "error": "Invalid profile ID",
  "details": null
}
```

---

### Chat

Send a message and receive a response from the configured AI model.

---

#### `POST /api/chat`

Send a chat message. The session history is persisted automatically.

**Request body:**
```json
{
  "message": "What is the capital of France?",
  "profileId": "a1b2c3d4-0000-0000-0000-000000000001",
  "sessionId": "s1t2u3v4-0000-0000-0000-000000000001"
}
```

> All three fields are required.

**Response `200`:** Plain text or JSON AI response (content-type: `text/plain` or `application/json`)

**Error `400`** (missing or invalid fields):
```json
{
  "error": "Message is required",
  "details": null
}
```

**Error `404`** (session or profile not found):
```json
{
  "error": "Session not found",
  "details": null
}
```

---

## Error Handling

All endpoints return a standard error object on non-2xx responses:

```json
{
  "error": "Human-readable error message",
  "details": "Optional technical detail or stack context"
}
```

### HTTP Status Codes

| Code | Meaning |
|------|---------|
| `200` | OK |
| `201` | Created |
| `204` | No Content |
| `400` | Bad Request — invalid input or missing required field |
| `404` | Not Found — resource does not exist |
| `500` | Internal Server Error |

---

## Rate Limiting

Not currently implemented. Will be added in M4.

---

## Data Types

| Type | Format | Example |
|------|--------|---------|
| IDs | UUID string | `"a1b2c3d4-0000-0000-0000-000000000001"` |
| Timestamps | ISO 8601 (UTC) | `"2026-05-22T23:00:00Z"` |
| Memory content | Markdown string | `"# Context\n\nDetails..."` |
