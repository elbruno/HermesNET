# HermesNET CLI Reference

Exhaustive reference for all `hermes` CLI commands, flags, and exit codes.

For installation instructions and workflows, see [cli-guide.md](./cli-guide.md).

## Table of Contents

- [Global Flags](#global-flags)
- [Commands](#commands)
  - [`hermes profile`](#hermes-profile)
  - [`hermes session`](#hermes-session)
  - [`hermes skill`](#hermes-skill)
  - [`hermes memory`](#hermes-memory)
  - [`hermes tool`](#hermes-tool)
  - [`hermes chat`](#hermes-chat)
- [Exit Codes](#exit-codes)
- [Quick Reference](#quick-reference)

---

## Global Flags

| Flag | Description |
|------|-------------|
| `--help`, `-h` | Show help for a command |
| `--version` | Show the CLI version |

---

## Commands

### `hermes profile`

Manage isolated profiles. Each profile has its own sessions and memory.

---

#### `hermes profile create <name>`

Create a new profile.

| Argument / Option | Required | Description |
|---|---|---|
| `<name>` | ✅ | Unique profile name |
| `--description`, `-d` | ❌ | Optional description |

**Examples:**
```bash
hermes profile create dev
hermes profile create work --description "Client project workspace"
```

**Output:**
```
Created profile 'dev' (a1b2c3d4-...)
```

---

#### `hermes profile list`

List all profiles. The currently active profile is marked with `*`.

**Example:**
```bash
hermes profile list
```

**Output:**
```
a1b2c3d4-...  dev *
    My main development profile
e5f6g7h8-...  work
    Client project workspace
```

---

#### `hermes profile switch <name-or-id>`

Activate a profile by name or ID.

| Argument | Required | Description |
|---|---|---|
| `<name-or-id>` | ✅ | Profile name or UUID |

**Examples:**
```bash
hermes profile switch work
hermes profile switch e5f6g7h8-...
```

**Output:**
```
Switched to profile 'work' (e5f6g7h8-...)
```

---

#### `hermes profile current`

Show the currently active profile.

**Example:**
```bash
hermes profile current
```

**Output:**
```
e5f6g7h8-...  work
```

If no profile is active:
```
No active profile. Use: hermes profile switch <name>
```

---

### `hermes session`

Manage sessions within a profile. Sessions are named conversation containers.

---

#### `hermes session create <name>`

Create a new session under the current (or specified) profile.

| Argument / Option | Required | Description |
|---|---|---|
| `<name>` | ✅ | Session name |
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermes session create "Q&A Bot"
hermes session create "Research" --profile e5f6g7h8-...
```

**Output:**
```
Created session 'Q&A Bot' (s1t2u3v4-...) under profile e5f6g7h8-...
```

---

#### `hermes session list`

List all sessions for the current (or specified) profile. The active session is marked with `*`.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermes session list
hermes session list --profile e5f6g7h8-...
```

**Output:**
```
s1t2u3v4-...  Q&A Bot *  (accessed: 2026-05-22 23:00:00Z)
w5x6y7z8-...  Research   (accessed: 2026-05-21 10:30:00Z)
```

---

#### `hermes session switch <id>`

Activate a session by ID.

| Argument | Required | Description |
|---|---|---|
| `<id>` | ✅ | Session UUID to activate |

**Example:**
```bash
hermes session switch s1t2u3v4-...
```

**Output:**
```
Switched to session 'Q&A Bot' (s1t2u3v4-...)
```

---

#### `hermes session current`

Show the currently active session.

**Example:**
```bash
hermes session current
```

**Output:**
```
s1t2u3v4-...  Q&A Bot  (profile: e5f6g7h8-...)
```

If no session is active:
```
No active session. Use: hermes session switch <id>
```

---

### `hermes skill`

Browse registered prompt skills.

---

#### `hermes skill list`

List all skills loaded in the registry.

**Example:**
```bash
hermes skill list
```

**Output:**
```
ID                             TYPE     VERSION    DESCRIPTION
--------------------------------------------------------------------------------
summarize                      prompt   1.0        Summarise long text into bullet points
translate                      prompt   1.0        Translate text to a target language
```

---

#### `hermes skill show <name>`

Display the full definition and metadata for a skill.

| Argument | Required | Description |
|---|---|---|
| `<name>` | ✅ | Skill ID |

**Example:**
```bash
hermes skill show summarize
```

**Output:**
```
ID:          summarize
Name:        summarize
Type:        prompt
Version:     1.0
Category:    text_processing
Description: Summarise long text into bullet points

Metadata:
  author: hermes-team
  tags: nlp, summarization

Content:
  You are a summarization assistant. Given the following text, ...
```

---

### `hermes memory`

Manage persistent memory and user profile documents per profile.

---

#### `hermes memory show`

Display the `MEMORY.md` content for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermes memory show
hermes memory show --profile e5f6g7h8-...
```

---

#### `hermes memory update`

Replace the `MEMORY.md` content for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--content`, `-c` | ✅ | New Markdown content |
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermes memory update --content "# My Context\n\nI prefer concise responses."
hermes memory update --profile e5f6g7h8-... --content "# Work Context\n\n..."
```

**Output:**
```
Memory updated for profile 'e5f6g7h8-...'.
```

---

#### `hermes memory profile-show`

Display the `USER.md` (user profile document) for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermes memory profile-show
hermes memory profile-show --profile e5f6g7h8-...
```

**Output:**
```
[Profile: e5f6g7h8-...] USER.md — v1
<content of USER.md>
```

---

### `hermes tool`

Inspect registered tools that the AI agent can invoke.

---

#### `hermes tool list`

List all registered tools, optionally filtered by category.

| Option | Required | Description |
|---|---|---|
| `--category`, `-c` | ❌ | Filter by category (e.g., `read_file`, `system_info`) |

**Examples:**
```bash
hermes tool list
hermes tool list --category read_file
```

**Output:**
```
NAME                           CATEGORY         TIMEOUT(ms)  DESCRIPTION
------------------------------------------------------------------------------------------
read_file                      read_file        5000         Read a file from the filesystem
get_system_info                system_info      2000         Retrieve OS and runtime info
```

Tools outside the safe-category whitelist are shown as `[DENIED]` and cannot be invoked.

---

#### `hermes tool show <name>`

Show full detail for a registered tool, including its parameters.

| Argument | Required | Description |
|---|---|---|
| `<name>` | ✅ | Tool name |

**Example:**
```bash
hermes tool show read_file
```

**Output:**
```
Name        : read_file
Category    : read_file
Status      : ALLOWED
Description : Read a file from the filesystem
MaxInputSize: 65536 bytes
Timeout     : 5000 ms
Parameters  :
  path (string, required) [file-path]: Absolute or relative path to the file
```

---

### `hermes chat`

Send a message to the AI model and print the response. The session is persisted automatically.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ✅ | Profile name to use |
| `--message`, `-m` | ✅ | Message to send |

**Examples:**
```bash
hermes chat --profile dev --message "What is the capital of France?"
hermes chat -p dev -m "Summarise the key differences between TCP and UDP."
```

**Output:**
```
Paris is the capital of France.
```

> **Note:** The session ID is written to stderr (`session-id: <id>`) so it does not pollute stdout pipelines. Use `2>/dev/null` to suppress it in scripts.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | General error (see stderr for details) |
| `2` | Invalid arguments |

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `hermes profile create <name> [-d <desc>]` | Create a new profile |
| `hermes profile list` | List all profiles |
| `hermes profile switch <name-or-id>` | Activate a profile |
| `hermes profile current` | Show active profile |
| `hermes session create <name> [-p <profile-id>]` | Create a session |
| `hermes session list [-p <profile-id>]` | List sessions |
| `hermes session switch <id>` | Activate a session |
| `hermes session current` | Show active session |
| `hermes skill list` | List all skills |
| `hermes skill show <name>` | Inspect a skill |
| `hermes memory show [-p <profile-id>]` | Show MEMORY.md |
| `hermes memory update -c <content> [-p <profile-id>]` | Update MEMORY.md |
| `hermes memory profile-show [-p <profile-id>]` | Show USER.md |
| `hermes tool list [-c <category>]` | List tools |
| `hermes tool show <name>` | Inspect a tool |
| `hermes chat -p <profile> -m <message>` | Send a chat message |
