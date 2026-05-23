# Migration Guide: M1 to M2

**Status:** No Migration Required  
**M2 Release:** 2.0.0 (First Public Release)

---

## Overview

HermesNET 2.0.0 is the **first public release** of the HermesNET distributed agent runtime.

- **M1** was an internal proof-of-concept released exclusively for OTel baseline measurement
- **M2** is the first production-ready release available to the public

**If you used M1:** This guide covers the transition to M2.  
**If you're new:** Start with [Quick Start Guide](./quickstart.md) — no migration needed.

---

## For M1 Users

### What Changed?

M2 builds on M1's OTel instrumentation foundation with **production-ready features**:

| Component | M1 | M2 | Change |
|-----------|----|----|--------|
| **Profiles** | Single default | Multiple isolated | ✅ Feature added |
| **Sessions** | Basic store | Persistent + resumable | ✅ Enhanced |
| **Skill Registry** | N/A | Auto-discovery + validation | ✅ New |
| **REST API** | N/A | Full CRUD + streaming | ✅ New |
| **CLI** | Basic chat | 6 command groups | ✅ Expanded |
| **OTel** | Baseline only | Production-ready | ✅ Enhanced |

### Breaking Changes?

**None.** M1 was internal-only. No public API contracts to break.

However, if you had M1 code:

- **Database Location:** M1 used `hermes.db` in project root; M2 centralizes to user config directory
  - **Windows:** `%APPDATA%\Hermes\`
  - **Linux/Mac:** `~/.hermes/`
  
  **Action:** Move `hermes.db` to new location, or let M2 create a fresh database

- **appsettings.json:** M1 config format unchanged; M2 adds new optional sections for REST API host
  - **Action:** No change required if upgrading from M1; optional new sections are auto-configured

- **Skill Format:** M1 had no skill registry; M2 requires YAML front-matter
  - **Action:** If you have custom M1 skill code, convert to Markdown format with YAML front-matter
  - **See:** [Skill Authoring Guide](./skill-authoring.md)

### Step-by-Step Upgrade

1. **Uninstall M1:**
   ```bash
   dotnet tool uninstall -g hermesnet
   ```

2. **Install M2:**
   ```bash
   dotnet tool install -g hermesnet
   ```

3. **Verify Installation:**
   ```bash
   hermes --version  # Should show 2.0.0
   hermes profile list  # Should be empty (fresh start)
   ```

4. **(Optional) Migrate Data:**
   If you have M1 profiles/sessions you want to preserve:
   - Locate M1 database: `<project>/hermes.db`
   - Move to M2 location: `~/.hermes/` (or `%APPDATA%\Hermes\` on Windows)
   - Run `hermes profile list` to verify data is recognized

5. **Create New Profiles:**
   ```bash
   hermes profile create myprofile --description "M2 migration"
   hermes session create "Initial Session"
   ```

### Configuration Migration

**M1 config format:**
```json
{
  "Provider": "Ollama",
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama2"
  }
}
```

**M2 adds optional sections:**
```json
{
  "Provider": "Ollama",
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama2"
  },
  "Hermes": {
    "DatabasePath": "~/.hermes/hermes.db",
    "RestApi": {
      "Enabled": true,
      "Port": 5000
    }
  }
}
```

**Action:** Your M1 config still works; new settings are optional and auto-configured.

---

## For New Users

If you're installing HermesNET 2.0.0 for the first time, there's no migration to worry about!

**Get started:**

1. **Install:** `dotnet tool install -g hermesnet`
2. **Quick Start:** [docs/quickstart.md](./quickstart.md)
3. **Full Guide:** [docs/user-guide.md](./user-guide.md)
4. **API Reference:** [docs/api-reference.md](./api-reference.md)

---

## Troubleshooting Migration

### "Database not found" Error

**Problem:** After upgrading, M2 says database not found.

**Solution:**
- M2 uses `~/.hermes/hermes.db` (not project-local)
- If you have M1 `hermes.db`, move it to `~/.hermes/`
- Or let M2 create a fresh database: `hermes profile create default`

### "Skill not found" Error

**Problem:** M1 skills don't work in M2.

**Solution:**
- M2 requires skills in `config/skills/` directory
- Skills must be Markdown with YAML front-matter
- Convert custom M1 skills; see [Skill Authoring Guide](./skill-authoring.md)

### "Provider not configured" Error

**Problem:** OTel or provider settings from M1 lost.

**Solution:**
- Check `appsettings.json` location: M2 looks in `src/Hermes.Cli/`
- If upgrading from source, keep existing config
- If installing as global tool, create config at: `~/.hermes/appsettings.json`

---

## What's New in M2?

Excited to explore M2's new features? Check these out:

- **Multi-profile workflows:** [docs/user-guide.md#profiles](./user-guide.md#profiles)
- **Session management:** [docs/user-guide.md#sessions](./user-guide.md#sessions)
- **Skill discovery:** `hermes skill list`
- **REST API:** Start server with `hermes api start` and visit `http://localhost:5000/swagger`
- **Memory management:** `hermes memory show` / `hermes memory update`

---

## Support

- **Questions?** See [Troubleshooting Guide](./troubleshooting.md)
- **Ready to dive in?** Start with [Quick Start Guide](./quickstart.md)
- **Deep dive?** Read [User Guide](./user-guide.md)

**Welcome to HermesNET 2.0.0! 🚀**
