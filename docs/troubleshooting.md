# HermesNET Troubleshooting Guide

Common issues organized by category. For each problem: **Cause → Solution → Example**.

---

## General

### "Command not found: hermes" / "hermesnet not recognized"

**Cause:** The CLI is not installed or not in your `PATH`.

**Solution:**
1. Verify installation: `dotnet tool list -g`
2. If not listed, install: `dotnet tool install -g hermesnet`
3. If installed but not found, your dotnet tools path may be missing from `PATH`:
   - Windows: Add `%USERPROFILE%\.dotnet\tools` to your `PATH` environment variable
   - Linux/Mac: Add `~/.dotnet/tools` to your `PATH` in `~/.bashrc` or `~/.zshrc`
4. Open a **new** terminal window and retry
5. If still missing, force-reinstall: `dotnet tool install -g hermesnet --force`

---

### "Port 5000 is already in use"

**Cause:** Another application is bound to port 5000 (the default API port).

**Solution:**
1. Stop the conflicting application, or
2. Run HermesNET on a different port:
   ```bash
   # Windows
   set ASPNETCORE_URLS=http://localhost:5001
   hermes serve

   # Linux/Mac
   ASPNETCORE_URLS=http://localhost:5001 hermes serve
   ```

---

### "Permission denied" (Linux/Mac)

**Cause:** The CLI binary does not have execute permissions.

**Solution:**
```bash
chmod +x ~/.dotnet/tools/hermes
```

If the issue persists, reinstall:
```bash
dotnet tool uninstall -g hermesnet
dotnet tool install -g hermesnet
```

---

### "dotnet SDK not found"

**Cause:** The .NET SDK is not installed or not in PATH.

**Solution:**
1. Install the .NET SDK from https://dotnet.microsoft.com/download (version 8.0 or later)
2. Verify: `dotnet --version`
3. Restart your terminal and retry

---

## Profiles & Sessions

### "Profile not found: {name}"

**Cause:** You referenced a profile that doesn't exist.

**Solution:**
1. List all profiles: `hermes profile list`
2. Check spelling — profile names are case-sensitive
3. Create the profile if needed: `hermes profile create {name}`

**Example:**
```bash
# Wrong — typo in name
hermes profile switch devlopment

# Check what exists
hermes profile list
# → dev, prod, research

# Correct
hermes profile switch dev
```

---

### "Session already exists"

**Cause:** A session with that name already exists in the active profile.

**Solution:**
1. List sessions to find the existing one: `hermes session list`
2. Resume the existing session: `hermes session switch {id}`
3. Or delete it first and recreate: `hermes session delete {id}`

---

### "No active profile" / "Profile or session not specified"

**Cause:** No profile or session is currently active, so HermesNET doesn't know where to operate.

**Solution:**
```bash
# 1. Create (or switch to) a profile
hermes profile create myprofile
# or: hermes profile switch myprofile

# 2. Create a session
hermes session create "My Work"

# 3. Switch to the session
hermes session switch {session-id}
```

---

### Sessions disappear after restart

**Cause:** Sessions are stored in the database. If the database path changed or was deleted, sessions are lost.

**Solution:**
1. Check database location in your config
2. Ensure the data directory has not been moved or cleaned
3. Restore from a memory export if you have one: `hermes memory update --file backup.txt`

---

### "Cannot delete active session"

**Cause:** You are trying to delete the session that is currently active.

**Solution:**
1. Switch to a different session first: `hermes session switch {other-id}`
2. Then delete the target session: `hermes session delete {id}`

---

## Skills

### "Skill not found: {name}"

**Cause:** The skill name is wrong, misspelled, or the skill file doesn't exist in `config/skills/`.

**Solution:**
1. List all loaded skills: `hermes skill list`
2. Check the exact skill ID (format is `namespace/name`, e.g., `math/sum`)
3. Inspect a skill: `hermes skill show {name}`
4. If missing, add the skill file to `config/skills/`:
   ```bash
   # Copy from samples as a starting point
   copy samples\skills\math-sum.md config\skills\math-sum.md
   ```

---

### "Skill failed to load"

**Cause:** The skill Markdown file is malformed or missing required YAML front-matter fields.

**Solution:**
1. Open the skill file: `config/skills/{name}.md`
2. Ensure the front-matter is valid YAML and includes all required fields:

   ```markdown
   ---
   id: namespace/name
   name: Display Name
   version: 1.0.0
   description: What this skill does
   tags:
     - tag1
   ---

   (skill body below)
   ```

3. Common issues:
   - Missing `---` delimiters around the front-matter
   - Invalid YAML (tabs instead of spaces, unquoted special characters)
   - Missing required fields (`id`, `name`, `version`, `description`)

4. Validate against the samples in `samples/skills/` to compare structure.

---

### "Skill execution failed"

**Cause:** The skill ran but encountered a runtime error (bad input, unavailable resource, etc.).

**Solution:**
1. Re-read the skill description: `hermes skill show {name}`
2. Check required inputs — are all required parameters provided?
3. Check the HermesNET console output for the full error message
4. Run with verbose output if available: `hermes --verbose skill ...`

---

## Memory

### "Memory not found for profile"

**Cause:** The active profile has no memory stored yet.

**Solution:**
```bash
# Create initial memory from a file
hermes memory update --file notes.txt

# Or via REST API
curl -X PUT "http://localhost:5000/api/memory?profileId={id}" \
  -H "Content-Type: application/json" \
  -d '{"content": "Initial memory content"}'
```

---

### "Memory content is too large"

**Cause:** The memory content exceeds the size limit (default: 10 MB).

**Solution:**
1. Export the current memory: `hermes memory show > archive-$(date +%Y%m%d).txt`
2. Trim the memory to only current/relevant content
3. Update with the trimmed version: `hermes memory update --file trimmed.txt`
4. Keep archives externally for reference

---

### Memory changes not persisting

**Cause:** Update command ran but the database write failed silently, or you are viewing a different profile's memory.

**Solution:**
1. Verify the active profile: `hermes profile list`
2. Confirm you are on the correct profile: `hermes profile switch {name}`
3. Re-run the update: `hermes memory update --file notes.txt`
4. Check database write permissions on the data directory

---

## REST API

### "Connection refused: localhost:5000"

**Cause:** The API server is not running, or it started on a different port.

**Solution:**
1. Start the server: `hermes serve`
2. Confirm the port in the startup output (look for `Listening on http://...`)
3. If using a custom port, adjust your requests accordingly:
   ```bash
   curl http://localhost:5001/api/profiles
   ```
4. Check that a firewall or VPN isn't blocking loopback connections

---

### "404 Not Found" on API call

**Cause:** The endpoint path or HTTP method is incorrect.

**Solution:**
1. Check the API reference: [`docs/openapi.yaml`](openapi.yaml)
2. Verify the exact path — all API routes are prefixed with `/api/`
3. Verify the HTTP method (GET, POST, PUT, DELETE)

**Examples:**
```bash
# Correct
GET  /api/profiles
POST /api/profiles
GET  /api/sessions?profileId={id}

# Wrong — missing /api/ prefix
GET  /profiles

# Wrong — wrong plural/singular
GET  /api/profile
```

---

### "400 Bad Request" on API call

**Cause:** The request body is missing required fields, has wrong field names, or contains invalid JSON.

**Solution:**
1. Check the API reference for required fields: [`docs/openapi.yaml`](openapi.yaml)
2. Validate your JSON is well-formed (use `hermes skill show json/validate` or an online tool)
3. Verify field names match the spec exactly (case-sensitive)

**Example:**
```json
// POST /api/profiles

// ✅ Correct
{ "name": "myprofile" }

// ❌ Wrong field name
{ "profileName": "myprofile" }

// ❌ Missing required field
{}
```

---

### "500 Internal Server Error"

**Cause:** An unhandled exception in the server — could be a data integrity issue, a missing dependency, or a bug.

**Solution:**
1. Check the server console output for the full stack trace
2. Restart the server: stop and re-run `hermes serve`
3. If the error mentions the database, check data directory permissions
4. If reproducible, open a bug report (see "Still Having Issues?" below)

---

## Performance

### Slow response times

**Cause:** Large memory content, many accumulated sessions, or system resource constraints.

**Solution:**
1. Archive and trim memory (see Memory section above)
2. Delete sessions you no longer need: `hermes session delete {id}`
3. Check system resources — HermesNET needs adequate RAM and disk I/O
4. On Windows, check Task Manager; on Linux/Mac, use `top` or `htop`

---

### High CPU or memory usage

**Cause:** Multiple HermesNET instances running simultaneously, or an expensive skill in a loop.

**Solution:**
1. Check for multiple instances:
   ```bash
   # Windows
   Get-Process dotnet

   # Linux/Mac
   ps aux | grep dotnet
   ```
2. Stop unneeded instances by their PID:
   ```bash
   # Windows PowerShell
   Stop-Process -Id {PID}

   # Linux/Mac
   kill {PID}
   ```
3. Restart a single clean instance: `hermes serve`
4. If usage stays high with a single instance, report as a bug

---

## Still Having Issues?

Work through this checklist before opening a report:

1. **Check the logs** — HermesNET logs to the console. Scroll up for the full error output.
2. **Check the CLI reference** — [`docs/cli-guide.md`](cli-guide.md) — verify command syntax.
3. **Check the API reference** — [`docs/openapi.yaml`](openapi.yaml) — verify endpoint and payload.
4. **Check the quickstart** — [`docs/quickstart.md`](quickstart.md) — re-run from scratch to isolate the issue.
5. **Search existing issues** — https://github.com/elbruno/HermesNET/issues

**To open a new bug report**, include:

```
HermesNET version:  hermes --version
.NET SDK version:   dotnet --version
Operating system:   (Windows 11 / Ubuntu 22.04 / macOS 14 / etc.)
Steps to reproduce: (numbered, minimal)
Expected behavior:  
Actual behavior:    
Full error output:  (paste the complete console output)
```

Open a new issue at: **https://github.com/elbruno/HermesNET/issues/new**

---
