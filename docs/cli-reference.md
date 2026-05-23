# HermesNET CLI Reference

Exhaustive reference for all `hermesnet` CLI commands, flags, and exit codes.

> **Note:** This reference document is the source of truth for CLI commands. It must always reflect the commands currently exposed by the CLI tool. See [Program.cs](../src/Hermes.Cli/Program.cs) for the authoritative command definitions.

For installation instructions and workflows, see [cli-guide.md](./cli-guide.md).

## Table of Contents

- [Global Flags](#global-flags)
- [Commands](#commands)
  - [`hermesnet profile`](#hermesnet-profile)
  - [`hermesnet session`](#hermesnet-session)
  - [`hermesnet skill`](#hermesnet-skill)
  - [`hermesnet memory`](#hermesnet-memory)
  - [`hermesnet config`](#hermesnet-config)
  - [`hermesnet doctor`](#hermesnet-doctor)
  - [`hermesnet chat`](#hermesnet-chat)
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

### `hermesnet profile`

Manage isolated profiles. Each profile has its own sessions and memory.

---

#### `hermesnet profile create <name>`

Create a new profile.

| Argument / Option | Required | Description |
|---|---|---|
| `<name>` | ✅ | Unique profile name |
| `--description`, `-d` | ❌ | Optional description |

**Examples:**
```bash
hermesnet profile create dev
hermesnet profile create work --description "Client project workspace"
```

**Output:**
```
Created profile 'dev' (a1b2c3d4-...)
```

---

#### `hermesnet profile list`

List all profiles. The currently active profile is marked with `*`.

**Example:**
```bash
hermesnet profile list
```

**Output:**
```
a1b2c3d4-...  dev *
    My main development profile
e5f6g7h8-...  work
    Client project workspace
```

---

#### `hermesnet profile switch <name-or-id>`

Activate a profile by name or ID.

| Argument | Required | Description |
|---|---|---|
| `<name-or-id>` | ✅ | Profile name or UUID |

**Examples:**
```bash
hermesnet profile switch work
hermesnet profile switch e5f6g7h8-...
```

**Output:**
```
Switched to profile 'work' (e5f6g7h8-...)
```

---

#### `hermesnet profile current`

Show the currently active profile.

**Example:**
```bash
hermesnet profile current
```

**Output:**
```
e5f6g7h8-...  work
```

If no profile is active:
```
No active profile. Use: hermesnet profile switch <name>
```

---

### `hermesnet session`

Manage sessions within a profile. Sessions are named conversation containers.

---

#### `hermesnet session create <name>`

Create a new session under the current (or specified) profile.

| Argument / Option | Required | Description |
|---|---|---|
| `<name>` | ✅ | Session name |
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermesnet session create "Q&A Bot"
hermesnet session create "Research" --profile e5f6g7h8-...
```

**Output:**
```
Created session 'Q&A Bot' (s1t2u3v4-...) under profile e5f6g7h8-...
```

---

#### `hermesnet session list`

List all sessions for the current (or specified) profile. The active session is marked with `*`.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermesnet session list
hermesnet session list --profile e5f6g7h8-...
```

**Output:**
```
s1t2u3v4-...  Q&A Bot *  (accessed: 2026-05-22 23:00:00Z)
w5x6y7z8-...  Research   (accessed: 2026-05-21 10:30:00Z)
```

---

#### `hermesnet session switch <id>`

Activate a session by ID.

| Argument | Required | Description |
|---|---|---|
| `<id>` | ✅ | Session UUID to activate |

**Example:**
```bash
hermesnet session switch s1t2u3v4-...
```

**Output:**
```
Switched to session 'Q&A Bot' (s1t2u3v4-...)
```

---

#### `hermesnet session current`

Show the currently active session.

**Example:**
```bash
hermesnet session current
```

**Output:**
```
s1t2u3v4-...  Q&A Bot  (profile: e5f6g7h8-...)
```

If no session is active:
```
No active session. Use: hermesnet session switch <id>
```

---

### `hermesnet skill`

Browse registered prompt skills.

---

#### `hermesnet skill list`

List all skills loaded in the registry.

**Example:**
```bash
hermesnet skill list
```

**Output:**
```
ID                             TYPE     VERSION    DESCRIPTION
--------------------------------------------------------------------------------
summarize                      prompt   1.0        Summarise long text into bullet points
translate                      prompt   1.0        Translate text to a target language
```

---

#### `hermesnet skill show <name>`

Display the full definition and metadata for a skill.

| Argument | Required | Description |
|---|---|---|
| `<name>` | ✅ | Skill ID |

**Example:**
```bash
hermesnet skill show summarize
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

### `hermesnet memory`

Manage persistent memory and user profile documents per profile.

---

#### `hermesnet memory show`

Display the `MEMORY.md` content for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermesnet memory show
hermesnet memory show --profile e5f6g7h8-...
```

---

#### `hermesnet memory update`

Replace the `MEMORY.md` content for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--content`, `-c` | ✅ | New Markdown content |
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermesnet memory update --content "# My Context\n\nI prefer concise responses."
hermesnet memory update --profile e5f6g7h8-... --content "# Work Context\n\n..."
```

**Output:**
```
Memory updated for profile 'e5f6g7h8-...'.
```

---

#### `hermesnet memory profile-show`

Display the `USER.md` (user profile document) for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ❌ | Profile ID (defaults to current profile) |

**Examples:**
```bash
hermesnet memory profile-show
hermesnet memory profile-show --profile e5f6g7h8-...
```

**Output:**
```
[Profile: e5f6g7h8-...] USER.md — v1
<content of USER.md>
```

---

### `hermesnet config`

Configure the active LLM provider and model settings.

---

#### `hermesnet config`

Interactively configure the HermesNET LLM provider and model.

This command presents a guided workflow to select and configure your LLM provider (OpenAI or Ollama) along with necessary credentials and model parameters. Configuration is saved to a local config file for persistence across sessions, while the OpenAI API key is stored in the OS credential store.

| Option | Required | Description |
|---|---|---|
| (none) | N/A | Runs interactive configuration wizard |

**Examples:**
```bash
hermesnet config
```

**Workflow:**
1. Choose LLM provider (OpenAI or Ollama)
2. Enter provider-specific settings:
   - **OpenAI:** API key (stored in the OS credential store) and model name
   - **Ollama:** Base URL and model name
3. Validates configuration and displays a summary

**Output:**
```
─ HermesNET config ─
Saving user settings to /home/user/.config/hermesnet/config.json

Choose the LLM provider:
> OpenAI
  Ollama

OpenAI API key [current: [set]]: 
OpenAI model [current: gpt-4]: 

─ Configuration summary ─
┌─────────────────────┬────────┬─────────────────────┐
│ Check               │ Status │ Details             │
├─────────────────────┼────────┼─────────────────────┤
│ Config file exists  │ PASS   │ /home/user/...      │
│ Provider set        │ PASS   │ OpenAI              │
│ API key configured  │ PASS   │ Key is set          │
│ Model set           │ PASS   │ gpt-4               │
└─────────────────────┴────────┴─────────────────────┘
```

---

### `hermesnet doctor`

Inspect HermesNET configuration and runtime health.

---

#### `hermesnet doctor`

Run diagnostics on the HermesNET setup and display a health report.

This command inspects your configuration files, validates LLM provider settings, checks database connectivity, and reports any issues that may affect runtime.

| Option | Required | Description |
|---|---|---|
| (none) | N/A | Runs diagnostics and displays results |

**Examples:**
```bash
hermesnet doctor
```

**Output:**
```
─ HermesNET doctor ─
Effective config from /app/appsettings.json
User config path: /home/user/.config/hermesnet/config.json

┌──────────────────────┬────────┬──────────────────────────┐
│ Check                │ Status │ Details                  │
├──────────────────────┼────────┼──────────────────────────┤
│ Config file exists   │ PASS   │ /home/user/.config/...   │
│ Provider set         │ PASS   │ OpenAI                   │
│ API key configured   │ PASS   │ Key is set               │
│ Model set            │ PASS   │ gpt-4                    │
│ Database connection  │ PASS   │ Connected to hermes.db   │
└──────────────────────┴────────┴──────────────────────────┘

No blocking issues found.
```

**Exit codes:**
- `0` - All checks passed
- `1` - One or more checks failed

**Check types:**
- **Config file exists** - Validates that configuration files are readable
- **Provider set** - Confirms an LLM provider is configured
- **API key configured** - Validates required credentials
- **Model set** - Confirms model selection
- **Database connection** - Tests connectivity to the session/profile database

---

### `hermesnet chat`

Send a message to the AI model and print the response. The session is persisted automatically.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ✅ | Profile name to use |
| `--message`, `-m` | ✅ | Message to send |

**Examples:**
```bash
hermesnet chat --profile dev --message "What is the capital of France?"
hermesnet chat -p dev -m "Summarise the key differences between TCP and UDP."
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
| `hermesnet profile create <name> [-d <desc>]` | Create a new profile |
| `hermesnet profile list` | List all profiles |
| `hermesnet profile switch <name-or-id>` | Activate a profile |
| `hermesnet profile current` | Show active profile |
| `hermesnet session create <name> [-p <profile-id>]` | Create a session |
| `hermesnet session list [-p <profile-id>]` | List sessions |
| `hermesnet session switch <id>` | Activate a session |
| `hermesnet session current` | Show active session |
| `hermesnet skill list` | List all skills |
| `hermesnet skill show <name>` | Inspect a skill |
| `hermesnet memory show [-p <profile-id>]` | Show MEMORY.md |
| `hermesnet memory update -c <content> [-p <profile-id>]` | Update MEMORY.md |
| `hermesnet memory profile-show [-p <profile-id>]` | Show USER.md |
| `hermesnet config` | Configure LLM provider |
| `hermesnet doctor` | Run configuration diagnostics |
| `hermesnet chat -p <profile> -m <message>` | Send a chat message |
