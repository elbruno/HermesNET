# HermesNET User Guide

Welcome to HermesNET — a distributed agent runtime for managing persistent context, reusable tasks, and multi-project workflows.

---

## What is HermesNET?

HermesNET is a distributed agent runtime that lets you:

- Create **profiles** — isolated contexts for different projects or environments
- Manage **sessions** — conversations and tasks within a profile
- Use **skills** — reusable, well-defined tasks you can discover and invoke
- Store **memory** — persistent context that survives session restarts
- Interact via **CLI** or **REST API**

Think of it like a personal assistant that remembers context, executes specialized tasks, and keeps your work organized across multiple projects.

---

## Core Concepts

### Profiles

A profile is an isolated workspace. Each profile has:

- Its own sessions
- Its own memory
- Independent configuration

Use profiles to separate concerns:

| Profile | Purpose |
|---------|---------|
| `dev` | Active development work |
| `prod` | Production monitoring and tasks |
| `research` | Research and experimentation |

**Switching profiles switches your entire context.**

---

### Sessions

A session is a conversation or task thread within a profile. Sessions:

- Belong to exactly one profile
- Maintain their own history and context
- Can be saved, resumed, and deleted

Examples:

| Session | Purpose |
|---------|---------|
| `"Q&A Bot"` | Interactive Q&A interactions |
| `"Code Review"` | Ongoing code analysis |
| `"Data Analysis"` | Data processing work |

---

### Skills

A skill is a reusable, well-defined task with clear inputs and outputs. Skills:

- Are defined as Markdown files with YAML front-matter
- Are discoverable via `hermes skill list`
- Can be chained together for complex workflows

Built-in examples:

| Skill | What it does |
|-------|-------------|
| `math/sum` | Calculates the sum of numbers |
| `text/summarize` | Summarizes text content |
| `system/disk-usage` | Checks disk usage |
| `json/validate` | Validates JSON structure |

See the full list: `hermes skill list`

---

### Memory

Memory is persistent key-value storage for a profile. It:

- Survives session restarts and process restarts
- Is scoped to the active profile
- Can be viewed, updated, and exported

Use memory to:

- Store important facts and decisions
- Track ongoing progress
- Maintain project context across sessions

---

## Workflows

### Workflow 1: Getting Started (5 minutes)

```bash
# 1. Install HermesNET
dotnet tool install -g hermesnet

# 2. Create a profile
hermes profile create mywork

# 3. Create a session
hermes session create "My Task"

# 4. Browse available skills
hermes skill list

# 5. Check profile memory
hermes memory show
```

You're up and running. Sessions auto-save, and memory persists across restarts.

---

### Workflow 2: Multi-Project Organization

Working on multiple projects simultaneously? Use separate profiles:

```bash
# Set up Project 1 — Development
hermes profile create dev
hermes session create "Sprint 42"

# Set up Project 2 — Production monitoring
hermes profile create prod
hermes session create "Incident Review"

# Switch between them
hermes profile switch dev
hermes profile switch prod
```

Each profile has fully isolated sessions, memory, and context.

---

### Workflow 3: Using Skills

Skills let you execute specific tasks on demand:

```bash
# Browse all available skills
hermes skill list

# Inspect a specific skill
hermes skill show math/sum

# View sample skills for reference
ls samples/skills/
```

Sample skills ship with HermesNET in `samples/skills/`. Copy and customize them:

```bash
# Copy a sample skill into your config
copy samples\skills\math-sum.md config\skills\my-sum.md
```

---

### Workflow 4: Memory Management

Memory persists automatically. To manage it:

```bash
# View current memory
hermes memory show

# Update memory from a file
hermes memory update --file notes.txt

# Export memory as backup
hermes memory show > backup-2024-01-01.txt
```

**Best practice:** Export memory before major changes so you always have a recovery point.

---

### Workflow 5: Using the REST API

HermesNET exposes a full REST API for automation and integrations:

```bash
# Start the API server
hermes serve

# Create a profile via API
curl -X POST http://localhost:5000/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "api-demo"}'

# List all profiles
curl http://localhost:5000/api/profiles

# Create a session
curl -X POST http://localhost:5000/api/sessions \
  -H "Content-Type: application/json" \
  -d '{"profileId": "<id>", "name": "Demo Session"}'
```

Full API reference: [`docs/openapi.yaml`](openapi.yaml)

---

## Architecture Overview

```
┌─────────────────────────────────────┐
│         CLI / REST API              │
│   (hermes command  /  HTTP)         │
└──────────────────┬──────────────────┘
                   │
┌──────────────────┴──────────────────┐
│         HermesNET Runtime           │
│  ┌────────────────────────────────┐ │
│  │  Profile Manager               │ │
│  │  Session Manager               │ │
│  │  Skill Registry                │ │
│  │  Memory Store                  │ │
│  │  Tool Registry                 │ │
│  └────────────────────────────────┘ │
└──────────────────┬──────────────────┘
                   │
┌──────────────────┴──────────────────┐
│           Data Layer                │
│     (SQLite / PostgreSQL)           │
└─────────────────────────────────────┘
```

**Request flow:** User → CLI/API → Runtime → Data Layer → Response

The CLI and REST API share the same runtime internals — everything you can do from the command line, you can also automate via HTTP.

---

## Best Practices

1. **One profile per project** — keeps contexts fully isolated so switching projects is instant
2. **Descriptive session names** — `"Sprint 42 Planning"` is easier to find than `"session-3"`
3. **Regular memory updates** — keep context fresh so HermesNET stays useful over time
4. **Export memory backups** — run `hermes memory show > backup.txt` before major changes
5. **Use skills for repetitive tasks** — define it once, reuse it everywhere
6. **Clean up unused sessions** — `hermes session list` + `hermes session delete {id}`

---

## Next Steps

| What you want | Where to go |
|---------------|-------------|
| All CLI commands and options | [`docs/cli-guide.md`](cli-guide.md) |
| Write your own skills | [`samples/skills/`](../samples/skills/) |
| REST API reference | [`docs/openapi.yaml`](openapi.yaml) |
| Quick 2-minute setup | [`docs/quickstart.md`](quickstart.md) |
| Troubleshoot problems | [`docs/troubleshooting.md`](troubleshooting.md) |

---
