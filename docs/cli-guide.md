# HermesNET CLI User Guide

Complete reference for all `hermes` CLI commands, options, and workflows.

---

## Installation

### From NuGet (Global Tool)

```bash
dotnet tool install -g hermesnet
```

After installation, the `hermes` command is available in any terminal session.

**Update to the latest version:**
```bash
dotnet tool update -g hermesnet
```

**Uninstall:**
```bash
dotnet tool uninstall -g hermesnet
```

**Verify installation:**
```bash
dotnet tool list -g
hermes --version
```

---

## Commands

### Profile Operations

Profiles provide isolated contexts. Each profile has its own sessions and memory.

---

#### `hermes profile create <name>`

Creates a new profile with the given name.

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

Lists all profiles. The currently active profile is marked with `*`.

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

Sets the active profile. You can use either the profile name or its ID.

| Argument | Required | Description |
|---|---|---|
| `<name-or-id>` | ✅ | Profile name or ID to activate |

**Examples:**
```bash
hermes profile switch work
hermes profile switch a1b2c3d4-...
```

**Output:**
```
Switched to profile 'work' (e5f6g7h8-...)
```

---

#### `hermes profile current`

Shows the currently active profile.

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

### Session Operations

Sessions are named conversation or task containers that live within a profile.

---

#### `hermes session create <name>`

Creates a new session under the current (or specified) profile.

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

Lists sessions for the current (or specified) profile. The active session is marked with `*`.

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

Sets the active session by ID.

| Argument | Required | Description |
|---|---|---|
| `<id>` | ✅ | Session ID to activate |

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

Shows the currently active session.

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

### Skill Operations

Skills are reusable capability definitions registered in the skill registry.

---

#### `hermes skill list`

Lists all skills loaded in the registry.

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
read_file                      tool     n/a        Read a file from the local filesystem
```

---

#### `hermes skill show <name>`

Displays the full definition and metadata for a skill.

| Argument | Required | Description |
|---|---|---|
| `<name>` | ✅ | Skill ID or name |

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

### Memory Operations

Memory stores curated context (`MEMORY.md`) and user profile data (`USER.md`) per profile.

---

#### `hermes memory show`

Displays the `MEMORY.md` content for the current (or specified) profile.

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

Replaces the `MEMORY.md` content for the current (or specified) profile.

| Option | Required | Description |
|---|---|---|
| `--content`, `-c` | ✅ | New Markdown content for MEMORY.md |
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

Displays the `USER.md` (user profile document) for the current (or specified) profile.

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

### Tool Operations

Tools are registered capabilities that the agent can invoke (e.g., file reads, system info).

---

#### `hermes tool list`

Lists all registered tools, optionally filtered by category.

| Option | Required | Description |
|---|---|---|
| `--category`, `-c` | ❌ | Filter by category: `read_file`, `system_info`, `text_processing`, etc. |

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

Tools marked `[DENIED]` are outside the M2 safe-category whitelist and cannot be invoked.

---

#### `hermes tool show <name>`

Shows detailed information about a registered tool including its parameters.

| Argument | Required | Description |
|---|---|---|
| `<name>` | ✅ | Tool name to inspect |

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

### Chat Operations

Send messages to the AI model and receive responses. Sessions are persisted automatically.

---

#### `hermes chat`

Sends a message to the configured AI model and prints the response.

| Option | Required | Description |
|---|---|---|
| `--profile`, `-p` | ✅ | Profile name to use for this chat |
| `--message`, `-m` | ✅ | Message to send |

**Example:**
```bash
hermes chat --profile dev --message "What is the capital of France?"
hermes chat -p dev -m "Summarise the key differences between TCP and UDP."
```

**Output:**
```
Paris is the capital of France.
```
> **Note:** The session ID is printed to stderr (`session-id: <id>`) so it does not pollute stdout pipelines.

---

## Workflows

### Workflow 1: Get Started in 5 Minutes

```bash
# 1. Install
dotnet tool install -g hermesnet

# 2. Create a profile
hermes profile create myfirst

# 3. Create a session
hermes session create "My First Session"

# 4. List skills
hermes skill list

# 5. Send a message
hermes chat --profile myfirst --message "Hello, Hermes!"
```

---

### Workflow 2: Multi-Profile Isolation

Use profiles to keep contexts completely separate — e.g., one profile per project or client.

```bash
# Set up two isolated profiles
hermes profile create client-a --description "Client A context"
hermes profile create client-b --description "Client B context"

# Work in client-a
hermes profile switch client-a
hermes session create "Sprint 1"
hermes memory update --content "# Client A\n\nFocus: REST API migration."

# Later, switch to client-b
hermes profile switch client-b
hermes session create "Discovery"
hermes memory update --content "# Client B\n\nFocus: Data pipeline audit."

# Check where you are
hermes profile current
hermes session current
```

---

### Workflow 3: Inspect Before You Chat

Browse available skills and tools before starting a conversation.

```bash
# See all skills
hermes skill list

# Get details on one
hermes skill show summarize

# See available tools
hermes tool list

# Check a specific tool
hermes tool show read_file

# Now chat
hermes chat --profile dev --message "Please summarise this: ..."
```

---

### Workflow 4: Memory-Driven Sessions

Keep persistent context across conversations using memory.

```bash
# Set memory for your profile
hermes memory update --content "# Preferences\n\n- Always respond in bullet points\n- Prefer concise answers"

# Verify it was saved
hermes memory show

# Chat — the memory is loaded automatically
hermes chat --profile dev --message "Explain REST vs GraphQL"
```

---

## Configuration

HermesNET reads provider settings from `appsettings.json` in the CLI project and overlays a user config file created by `hermesnet config`:

OpenAI API keys are stored in the native OS credential store and are merged in at runtime.

- Windows: `%APPDATA%\Hermes\appsettings.json`
- macOS/Linux: `~/.hermes/appsettings.json`

```json
{
  "Provider": "Ollama",
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3"
  }
}
```

Switch to the OpenAI provider:
```json
{
  "Provider": "OpenAI",
  "OpenAI": {
    "Model": "gpt-4o"
  }
}
```

Run diagnostics with `hermesnet doctor` to validate the active provider, config files, and required settings.

---

## Troubleshooting

### `hermesnet: command not found`

The .NET global tools path is not on your `PATH`.

```bash
# Verify installation
dotnet tool list -g

# Add the tools directory to your PATH:
# Linux/macOS — add to ~/.bashrc or ~/.zshrc:
export PATH="$HOME/.dotnet/tools:$PATH"

# Windows — the path is automatically added; restart your terminal or run:
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

# Force reinstall if needed
dotnet tool install -g hermesnet
```

---

### `No active profile`

You haven't switched to a profile yet (or it was reset).

```bash
# List all profiles
hermes profile list

# Switch by name or ID
hermes profile switch dev
```

---

### `No profile found for '<name>'`

The profile name or ID doesn't match anything in the database.

```bash
# Check for typos — list all profiles
hermes profile list
```

---

### `No active session`

You need to create or switch to a session before commands that require one.

```bash
# List sessions under the current profile
hermes session list

# Switch to an existing one
hermes session switch <id>

# Or create a new one
hermes session create "New Session"
```

---

### `Skill '<name>' not found`

The skill ID or name you specified doesn't exist in the registry.

```bash
# List all loaded skills to find the correct ID
hermes skill list
```

---

### `Tool '<name>' not found`

The tool name doesn't exist in the registry.

```bash
# List all registered tools
hermes tool list
```

---

### Memory parse errors

`hermes memory update` requires valid Markdown. Ensure you escape special characters in the shell.

```bash
# Use a here-string or file redirect for multi-line content:
hermes memory update --content $'# My Context\n\nPrefer concise answers.'
```

---

## Best Practices

- **One profile per project** — keeps sessions, memory, and context fully isolated
- **Descriptive session names** — e.g., `"Sprint 12 planning"` instead of `"session1"`
- **Update memory regularly** — keep `MEMORY.md` current so the model has accurate context
- **Use `--profile` explicitly** in scripts — don't rely on the ambient current profile in automated workflows
- **Check `hermes skill list` first** — know what capabilities are loaded before starting a complex task
- **Pipe `hermes chat` output** — stdout is clean (just the response); use `2>/dev/null` to suppress the session-id line if needed

---

## Command Reference (Quick Look)

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
| `hermes tool list [-c <category>]` | List tools (optionally by category) |
| `hermes tool show <name>` | Inspect a tool |
| `hermes chat -p <profile> -m <message>` | Send a chat message |
| `hermesnet config` | Set up LLM provider settings |
| `hermesnet doctor` | Run configuration diagnostics |
