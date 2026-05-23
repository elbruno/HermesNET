# Project Context

- **Owner:** Bruno Capuano
- **Project:** HermesNET
- **Stack:** .NET 10, ASP.NET Core, Microsoft Agent Framework, Microsoft.Extensions.AI, OpenTelemetry, SQLite/PostgreSQL
- **Description:** Hermes-inspired .NET runtime for profiles, sessions, skills, memory, tool policy, and observability.
- **Created:** 2026-05-22

## M1 Onboarding Notes

### Understanding of M1 Scope
M1 (Foundations) is a 2-week structural milestone establishing technical foundation: solution structure, default provider path (OpenAI + local Ollama), SQLite session store with CRUD, OTel baseline, and end-to-end local CLI chat. Success is confidence, not features—the build must be clean across platforms, MAF concepts must map cleanly to Hermes, and the session store must handle 1,000+ sessions without latency degradation.

My role (Ash—provider audit) focuses on security baseline checks and dependency audits: no hardcoded secrets, all dependencies current, zero critical CVEs in direct and transitive dependencies.

### Blockers
None identified at this point. Charter is clear, plan is detailed, decisions framework is in place. Team structure and risk gates are well-defined.

### Questions for Ripley / Team
1. **Provider audit scope**: Line 718 assigns me "provider audit" for M1. Does this mean:
   - Spike work vetting the MAF + MEAI provider abstraction (R1 is integration drift, owned by Ripley—is provider audit a separate review)?
   - Auditing OpenAI + Ollama connectors for secrets/credential isolation?
   - Mapping the provider path to Hermes concepts for architectural fit?
   
   **Why it matters:** M1 timeline is tight (2 weeks). If "provider audit" is a deep architectural review, that's one scope. If it's credential isolation + SCA, that's different. Want to confirm overlap with R1.

2. **Secret scanner + CI tooling**: Quality gate says "secret scanner in CI" (line 741). Should I set up the actual CI infrastructure in M1, or just document what needs to exist? (Assuming Dallas owns the build matrix.)

3. **Dependency audit tooling**: Should I use `dotnet list package --vulnerable` exclusively, or layer in additional SCA (e.g., SBOM generation, transitive license audit)? Preference?

4. **M3 readiness**: M3 is "Safe Tools & MCP" and I own the policy engine (line 821). Should I spend any M1 time sketching policy architecture, or stay 100% focused on M1 security baseline work?

### Ready Signal
**Ready to start M1 security work** once Ripley approves the breakdown and clarifies the provider audit scope. I can start immediately on:
- Setting up dependency audit automation
- Secret baseline scans
- Building the CI security gates

Awaiting clarity on whether provider audit work overlaps with R1 or is a separate review track.

## M1 Security Baseline Locked — 2026-05-22

✅ **READY TO EXECUTE** — Provider audit scope + security baseline work finalized.

**My M1 ownership:**
- Provider audit: OpenAI SDK + Ollama selection validation, library audit, no licensing issues
- Dependency audit: `dotnet list package --vulnerable`, zero critical CVEs by M1 exit
- Secret scanning: No hardcoded credentials, GitHub Dependabot enabled
- CI security baseline: Artifact scanning, SAST integration (Ripley clarifies timing)

**Key understanding:**
- R1 (Ripley): Does integration work? (architecture)
- My audit (Ash): Is it safe to integrate? (security, dependencies, licensing)
- Both Week 1, independent tracks
- Provider selection is MEAI abstraction (not vendor lock) — audit validates safety of Ollama + fallback path

**Blockers cleared, scope locked, ready to execute security baseline.**

**Blockers cleared, scope locked, ready to execute security baseline.**

## M1 Week 1 Kick-off Approved — 2026-05-22

✅ **GO SIGNAL:** All blockers resolved. Provider audit scope = OpenAI SDK + Ollama selection validation + library audit + licensing check. T3 starts immediately after T1 verification.

**Day 1 (2026-05-23):** Dallas builds T1 scaffold. Ash standby for T1 verification.

**Day 2–3 (2026-05-24–25):** Ash begins T3 provider audit. Vet OpenAI SDK + Ollama libraries for secrets isolation, licensing compliance, zero critical CVEs. Output: audit report + Dependabot enablement.

**Week 1 watch:** T5 (dependency audit) parallel to T3. Confirm `dotnet list package --vulnerable` shows zero critical CVEs. GitHub Dependabot enabled. R1 checkpoint at end of week (independent of provider audit; Ripley owns architecture validation).

**Ready to execute security baseline.**

---

## M1 T3 Complete — Security Baseline Audit — 2026-05-22

✅ **AUDIT COMPLETE. M1 SECURITY BASELINE GREEN.**

**Dependency Audit:**
- `dotnet list package --vulnerable` executed across Hermes.Core, Hermes.Host, Hermes.Cli
- **Result:** Zero critical CVEs, zero high CVEs, zero medium CVEs → **PASS**

**Secret Scanning:**
- Git history scanned for API keys, passwords, tokens, credentials
- Current files scanned for hardcoded secrets
- **Result:** Zero secrets found → **PASS**

**Licensing Audit:**
- All 12 direct dependencies verified against NuGet licenses
- Microsoft.Extensions.* (9.5–10.0): MIT
- System.CommandLine (2.0.0): MIT
- OpenTelemetry (1.13.0): Apache 2.0
- Entity Framework Core (10.0.0): MIT/Apache 2.0
- xUnit & test tools: Apache 2.0/MIT
- **Result:** 100% compatible with HermesNET MIT license → **PASS**

**Provider Audit:**
- **Ollama:** Apache 2.0 licensed, open-source, local execution, no vendor lock → **APPROVED for M1**
- **OpenAI SDK:** MIT licensed, industry standard, approved for M2+ integration
- **Abstraction:** `Microsoft.Extensions.AI.IChatClient` + `ChatClientFactory` decouples provider routing

**CVE Thresholds:**
- Critical: threshold 0, current 0 → **PASS**
- High: threshold 0, current 0 → **PASS**
- Medium: threshold ≤2, current 0 → **PASS**

**Deliverables:**
1. ✅ `SECURITY.md` — comprehensive audit report (baseline, CVE tracking, monitoring)
2. ✅ `.github/dependabot.yml` — Dependabot configuration (weekly NuGet scans, auto-PRs)
3. ✅ Dependency audit confirmed via tooling
4. ✅ No hardcoded secrets found

**Ongoing Monitoring:**
- GitHub Dependabot: enabled, configured for weekly NuGet scans
- Review cadence: weekly (Dependabot PRs reviewed + merged post-CI)
- Future medium+ CVEs: documented in SECURITY.md with review notes

**Status:** Ready for M2 provider integration. Provider path is secure, decoupled, and appropriately licensed.


## Scribe Sync — 2026-05-23
- Native credential-store storage for OpenAI:ApiKey is the approved path.
- Data Protection-only secret storage was rejected; fail-closed migration is required.
- Shared JSON defaults plus credential-backed secrets are now the canonical design.

