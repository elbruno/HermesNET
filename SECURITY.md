# Security Baseline — HermesNET M1

**Date:** 2026-05-22  
**Status:** ✅ APPROVED  
**Auditor:** Ash (Security/Policy)

---

## Executive Summary

HermesNET M1 has passed all security baseline checks. Zero critical/high CVEs identified. No hardcoded secrets found in codebase. All dependencies are compatible with MIT license. GitHub Dependabot enabled for ongoing monitoring.

---

## 1. Dependency Vulnerability Scan

**Tool:** `dotnet list package --vulnerable`  
**Date Scanned:** 2026-05-22  
**Result:** ✅ **ZERO critical/high vulnerabilities**

### Command Output
```
The given project `Hermes.Cli` has no vulnerable packages given the current sources.
The given project `Hermes.Core` has no vulnerable packages given the current sources.
The given project `Hermes.Host` has no vulnerable packages given the current sources.
```

### Findings
- **Critical CVEs:** 0
- **High CVEs:** 0
- **Medium CVEs:** 0
- **Low CVEs:** 0

**Conclusion:** All projects clear. No action required. Review cadence: weekly via Dependabot.

---

## 2. Dependency Inventory & Licensing

**Source:** `Directory.Packages.props` (centralized version management)

### Direct Dependencies

| Package | Version | License | Status |
|---------|---------|---------|--------|
| Microsoft.Extensions.AI.Abstractions | 9.5.0 | MIT | ✅ Compatible |
| Microsoft.Extensions.Configuration | 10.0.0 | MIT | ✅ Compatible |
| Microsoft.Extensions.Configuration.Json | 10.0.0 | MIT | ✅ Compatible |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | MIT | ✅ Compatible |
| Microsoft.EntityFrameworkCore | 10.0.0 | MIT/Apache 2.0 | ✅ Compatible |
| System.CommandLine | 2.0.0 | MIT | ✅ Compatible |
| OpenTelemetry | 1.13.0 | Apache 2.0 | ✅ Compatible |
| OpenTelemetry.Exporter.Console | 1.13.0 | Apache 2.0 | ✅ Compatible |
| xunit | 2.6.0 | Apache 2.0 | ✅ Compatible (test only) |
| xunit.runner.visualstudio | 2.5.4 | MIT | ✅ Compatible (test only) |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | ✅ Compatible (test only) |
| Coverlet.Collector | 6.0.0 | MIT | ✅ Compatible (test only) |

### License Compatibility Check
- **HermesNET License:** MIT
- **All direct dependencies:** MIT or Apache 2.0
- **Excluded licenses:** None (GPL, SSPL, AGPL all absent)
- **Conclusion:** ✅ **100% compatible. No licensing conflicts.**

---

## 3. Secret & Credential Scanning

**Method:** Git history inspection + file content scan

### Results
- **Hardcoded API Keys:** 0
- **Hardcoded Passwords:** 0
- **Hardcoded Tokens:** 0
- **Hardcoded Credentials:** 0
- **Database Passwords:** 0
- **Connection Strings:** 0

**Conclusion:** ✅ **No secrets found in codebase or git history.**

### Notes
- No `appsettings.json` secrets files committed
- No `.env` files in repository
- All configuration will be injected at runtime via environment variables or secure vaults (M2+)

---

## 4. Provider Audit

### Ollama (Local Provider)
- **Status:** ✅ **APPROVED for M1**
- **License:** Apache 2.0 (compatible with MIT)
- **Maintainability:** Open-source, community-supported
- **Vendor Lock:** None (local, can be replaced)
- **Security Posture:** Runs locally, no remote credential exposure risk
- **Integration:** Configuration-driven via `appsettings.json`

### OpenAI SDK (Cloud Provider)
- **Status:** ✅ **APPROVED for M2+**
- **License:** MIT (compatible)
- **Maintainability:** Official SDK, actively maintained by OpenAI
- **Vendor Lock:** Medium (requires OpenAI account), acceptable via configuration abstraction
- **Security Posture:** API keys managed separately, no hardcoding (enforced)
- **Integration:** Planned for M2, will use `IChatClient` abstraction

### Abstraction & Provider Routing
- **Framework:** `Microsoft.Extensions.AI.IChatClient` (vendor-neutral abstraction)
- **Factory:** `ChatClientFactory.cs` routes based on `appsettings.json` (`Provider: "Ollama" | "OpenAI"`)
- **DI Registration:** `Program.cs` injects provider at runtime, no code changes needed to swap providers
- **Verdict:** ✅ **Decoupled design prevents vendor lock-in**

---

## 5. CVE Threshold & Acceptance Criteria

| Severity | Threshold | Current | Status |
|----------|-----------|---------|--------|
| Critical | 0 | 0 | ✅ PASS |
| High | 0 | 0 | ✅ PASS |
| Medium | ≤ 2 | 0 | ✅ PASS |
| Low | Unlimited | 0 | ✅ PASS |

**Decision:** All thresholds met. No medium CVEs to review. Ready for M2.

---

## 6. Ongoing Monitoring

### GitHub Dependabot
- **Status:** ✅ **Enabled**
- **Scope:** All NuGet packages, direct and transitive
- **Alerts:** Enabled (GitHub security tab monitors CVE advisories)
- **Auto-Updates:** Review PRs weekly; merge after passing CI tests
- **Cadence:** Dependabot scans continuously; alerts triggered on NuGet advisor updates

### Review Process
1. **New Dependabot alert:** GitHub notifies repo maintainers
2. **Initial assessment:** Review CVE severity, CVSS score, patch availability
3. **Testing:** Run `dotnet build && dotnet test` on Dependabot branch
4. **Merge decision:** Approve + merge if tests pass and no breaking changes detected
5. **Documentation:** Update `SECURITY.md` with medium+ CVE reviews and decisions

### Tools in Use
- `dotnet list package --vulnerable` (weekly local audit)
- GitHub Dependabot (continuous monitoring)
- GitHub security alerts (automatic advisory ingestion)

---

## 7. Known Constraints & Assumptions

### Applied Constraints
- **Zero tolerance for critical CVEs:** None detected; constraint satisfied
- **Max 2 medium CVEs with review:** 0 detected; constraint satisfied
- **No GPL/AGPL/SSPL dependencies:** Verified; constraint satisfied
- **All dependencies MIT/Apache 2.0 compatible:** Verified; constraint satisfied
- **Hardcoded secrets forbidden:** Verified; constraint satisfied

### Assumptions
- GitHub Dependabot security alerts are configured and active
- NuGet.org security advisory data is current and complete
- Future dependency additions will follow same licensing + CVE standards

---

## 8. Audit Completion Checklist

- [x] `dotnet list package --vulnerable` executed → zero critical/high vulnerabilities
- [x] Git history scanned for secrets → none found
- [x] Current files scanned for hardcoded credentials → none found
- [x] All direct dependencies verified for MIT/Apache 2.0 compatibility → all pass
- [x] Ollama provider audited → OpenSource, Apache 2.0, approved for M1
- [x] OpenAI SDK path validated → MIT, approved for M2+
- [x] GitHub Dependabot enabled → configured and monitoring
- [x] CVE thresholds documented → critical: 0, high: 0, medium: 0/2
- [x] SECURITY.md created → this document
- [x] History updated → audit logged in `.squad/agents/ash/history.md`

---

## 9. Recommendations for Future Milestones

### M2 (Provider Integration)
- Integrate OpenAI SDK via NuGet when M1 exits
- Verify OpenAI SDK has zero critical CVEs at integration time (re-scan)
- Implement credential manager (e.g., Azure KeyVault) for API key injection
- Document API key rotation policy

### M3+ (Ongoing)
- **Monthly dependency audits** (vs. weekly)
- **Quarterly security training** for team
- **Establish incident response plan** for CVE patches (SLA: critical = 24 hours)
- **Add SBOM generation** to CI pipeline (software bill of materials)

### M4+ (Advanced Security)
- Add SAST (static analysis security testing) to CI pipeline
- Implement container image scanning (if moving to Docker)
- Add secrets scanning to pre-commit hooks
- Conduct annual penetration test

---

## Sign-Off

**Auditor:** Ash (Security/Policy Dev)  
**Date Completed:** 2026-05-22  
**Next Review:** 2026-05-29 (weekly cadence via Dependabot monitoring)  
**Status:** ✅ **M1 SECURITY BASELINE GREEN**

---

*This document is part of HermesNET M1 security governance. Updates required when dependencies change or Dependabot alerts are resolved.*
