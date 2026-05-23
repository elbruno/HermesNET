# HermesNET Quick Start

Get up and running with the `hermes` CLI in under 5 minutes.

## 30-Second Setup

```bash
# 1. Install the global tool
dotnet tool install -g hermesnet

# 2. Create your first profile
hermes profile create myprofile

# 3. Create a session
hermes session create "Demo"

# 4. Check what you have
hermes session current
hermes memory show
```

## What You Just Did

✅ Installed `hermesnet` globally — available as `hermes` anywhere in your terminal  
✅ Created a **profile** — an isolated context for your work  
✅ Created a **session** — a named conversation / task container  
✅ Viewed your **memory** — the curated context stored for this profile  

## Next Steps

| Goal | Command |
|------|---------|
| Explore available skills | `hermes skill list` |
| Inspect a skill | `hermes skill show <skill-name>` |
| List registered tools | `hermes tool list` |
| Send a chat message | `hermes chat --profile myprofile --message "Hello!"` |
| Create another profile | `hermes profile create work --description "Work projects"` |
| Switch profiles | `hermes profile switch <id-or-name>` |
| Update profile memory | `hermes memory update --content "# My Context\n..."` |

**Full reference:** See [`docs/cli-guide.md`](./cli-guide.md)

## Troubleshooting

**Command not found after install?**
```bash
# Verify the tool is installed
dotnet tool list -g

# If hermesnet isn't listed, reinstall:
dotnet tool install -g hermesnet

# On some systems you may need to add the tools path to your shell profile:
# ~/.dotnet/tools  (Linux/macOS)
# %USERPROFILE%\.dotnet\tools  (Windows)
```

**"No active profile" error?**
```bash
# List all profiles
hermes profile list

# Switch to an existing profile (by name or ID)
hermes profile switch myprofile
```

**Want to uninstall?**
```bash
dotnet tool uninstall -g hermesnet
```
