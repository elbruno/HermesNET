# HermesNET Quick Start

Get up and running with the `hermesnet` CLI in under 5 minutes.

## 30-Second Setup

```bash
# 1. Install the global tool
dotnet tool install -g hermesnet

# 2. Configure your provider
hermesnet config

# 3. Create your first profile
hermesnet profile create myprofile

# 4. Create a session
hermesnet session create "Demo"

# 5. Check what you have
hermesnet session current
hermesnet memory show
```

## What You Just Did

✅ Installed `hermesnet` globally — available as `hermesnet` anywhere in your terminal  
✅ Configured the provider with `hermesnet config`  
✅ Created a **profile** — an isolated context for your work  
✅ Created a **session** — a named conversation / task container  
✅ Viewed your **memory** — the curated context stored for this profile  

## Next Steps

| Goal | Command |
|------|---------|
| Explore available skills | `hermesnet skill list` |
| Inspect a skill | `hermesnet skill show <skill-name>` |
| List registered tools | `hermesnet tool list` |
| Send a chat message | `hermesnet chat --profile myprofile --message "Hello!"` |
| Create another profile | `hermesnet profile create work --description "Work projects"` |
| Switch profiles | `hermesnet profile switch <id-or-name>` |
| Update profile memory | `hermesnet memory update --content "# My Context\n..."` |
| Run diagnostics | `hermesnet doctor` |

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
hermesnet profile list

# Switch to an existing profile (by name or ID)
hermesnet profile switch myprofile
```

**Want to uninstall?**
```bash
dotnet tool uninstall -g hermesnet
```
