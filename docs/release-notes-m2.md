# HermesNET 2.0.0 — Production Release

**Release Date:** 2026-05-23  
**Version:** 2.0.0 (First Public Release)

---

## Headline

HermesNET 2.0.0 is the **first production-ready release** of the HermesNET distributed agent runtime. After extensive development, quality assurance, and observability validation, HermesNET is ready for public use.

---

## What's New

### Multi-Profile & Session Management

- **Profile Isolation:** Create isolated workspaces for different projects, environments, or use cases
- **Hard Foreign Key Isolation:** Profiles and sessions are enforced at the database level — no cross-profile data leakage
- **Session Persistence:** Sessions survive restarts; resumable across CLI invocations
- **Memory Management:** Each profile maintains independent context and memory

**Use Case:** Develop in one profile, monitor production in another — completely isolated workspaces.

### Skill Registry

- **Auto-Discovery:** Skills are automatically discovered from `config/skills/` directory
- **Markdown-Based Skills:** YAML front-matter defines skill metadata, Markdown body describes capability
- **Skill Validation:** Compile-time validation ensures skills are well-formed and documented
- **Skill Invocation:** Call skills by name with integrated parameters and context injection

**Example:**
```bash
hermes skill list          # Discover available skills
hermes skill show math     # Inspect skill metadata
hermes skill invoke math --op sum --nums "1,2,3"
```

### Full REST API

Production-ready REST API with OpenAPI documentation:

- **Profile Management:** Create, list, switch, and delete profiles
- **Session Management:** Create, resume, list, and delete sessions
- **Memory Operations:** Store, retrieve, and update context per profile
- **Chat Endpoint:** Send messages and stream responses with full turn history
- **Health & Status:** Liveness and readiness probes for orchestration

All endpoints documented in **OpenAPI 3.0 spec** (`docs/openapi.yaml`) with JSON schema support.

### CLI Tool (6 Command Groups)

Comprehensive command-line interface available as a global .NET tool:

| Command | Purpose |
|---------|---------|
| `hermes profile` | Create, list, switch profiles |
| `hermes session` | Create, resume, list, delete sessions |
| `hermes skill` | Discover and invoke skills |
| `hermes memory` | View, update, clear profile context |
| `hermes tool` | List available tools and functions |
| `hermes chat` | Send messages and interact with agent |

**Install via NuGet:**
```bash
dotnet tool install -g hermesnet
hermes --version  # Verify installation
```

### Observability: OpenTelemetry Instrumentation

Built-in observability from Day 1:

- **Distributed Tracing:** Every user request generates a trace with parent-child span relationships
- **Automatic Instrumentation:** All M2 code paths instrumented (turn spans, provider calls, persistence)
- **Low Overhead:** OTel overhead measured at **<1%** (target: <20%)
- **Standards-Based:** Integrates with OTLP, Jaeger, Zipkin, and major observability platforms

Spans capture:
- Turn ID, message length, response length
- Provider name and latency
- Session persistence latency

See `docs/user-guide.md` for configuration examples.

### Comprehensive Documentation

- **Quick Start Guide** (`docs/quickstart.md`) — 5-minute setup
- **User Guide** (`docs/user-guide.md`) — Core concepts and workflows
- **CLI Reference** (`docs/cli-reference.md`) — Complete command reference
- **API Reference** (`docs/api-reference.md`) — REST endpoint documentation
- **Skill Authoring** (`docs/skill-authoring.md`) — Write custom skills
- **Troubleshooting** (`docs/troubleshooting.md`) — Common issues and solutions
- **Sample Projects** (`samples/`) — Real-world examples

---

## Quality Metrics

### Test Coverage
- **Total Tests:** 264 unit and integration tests passing
- **Code Coverage:** 90.1% across all M2 code paths
- **Quality Gates:** All 8 gates GREEN ✅

| Gate | Metric | Target | Result |
|------|--------|--------|--------|
| R1 | Build warnings | Zero | ✅ Pass |
| R2 | Test count | ≥200 | ✅ Pass (264) |
| R3 | Code coverage | ≥85% | ✅ Pass (90.1%) |
| R4 | Latency baseline | ≤100ms P95 | ✅ Pass (47ms) |
| R6 | OTel overhead | <20% | ✅ Pass (<1%) |
| R7 | Load test (1K sessions) | No regression | ✅ Pass |
| R8 | API response time | ≤500ms | ✅ Pass |
| R9 | Memory growth | Stable <500MB | ✅ Pass |

### Performance Baseline
- **P95 Turn Latency:** 47ms (CLI input → response, OTel enabled)
- **OTel Overhead:** <1% (measured against M1 baseline)
- **Load Test:** 1,000 sessions concurrent, stable latency
- **Memory Profile:** Stable allocation, <500MB peak

---

## Known Limitations

### Current Constraints

1. **Single Skill Version per ID**
   - Only one version of each skill can be active at a time
   - No side-by-side skill versioning yet
   - Plan: Multi-version support in M4+

2. **Skill Format: Markdown with YAML Front-Matter**
   - Skills must be written as Markdown files with YAML front-matter
   - No binary or JSON-only skill formats yet
   - Improves human readability and version control integration

3. **No Skill Namespacing**
   - Skills use flat naming: `math`, `string`, `web` 
   - Hierarchical names like `math/sum` not supported yet
   - Plan: Namespace support in M3C (MAF refactor phase)

4. **Provider Factory Uses Custom Registry**
   - Using custom `SkillRegistry` implementation during M2
   - Plan: Migrate to Microsoft.Extensions.AI Framework (MAF) in M3C
   - Framework adoption enables provider ecosystem expansion

---

## Breaking Changes

**None.** HermesNET 2.0.0 is the first public release. No M1 migration or breaking API changes.

See [M1 Baseline](../M1-BASELINE.txt) for reference: M1 was an internal proof-of-concept used to establish OTel measurement baselines.

---

## Installation & Getting Started

### Install as Global .NET Tool

```bash
dotnet tool install -g hermesnet
```

Requires `.NET 10.0` or later. Verify installation:

```bash
hermes --version
hermes profile list  # Should be empty on first run
```

### Quick Start

Complete setup in 30 seconds:

```bash
# 1. Create a profile
hermes profile create myprofile

# 2. Create a session
hermes session create "Demo"

# 3. Start chatting
hermes chat --message "What is 2+2?"
```

### Next Steps

1. **Read the Quick Start Guide:** [docs/quickstart.md](./quickstart.md)
2. **Explore the CLI:** [docs/cli-guide.md](./cli-guide.md)
3. **Integrate via REST API:** [docs/api-reference.md](./api-reference.md)
4. **Author Custom Skills:** [docs/skill-authoring.md](./skill-authoring.md)

---

## Acknowledgments

HermesNET 2.0.0 represents the culmination of effort by the M2 release team:

- **Dallas** — Architecture, session persistence, and profile isolation
- **Lambert** — REST API design, OpenAPI spec, and HTTP host
- **Parker** — Release engineering, quality gates, and observability metrics
- **Ripley** — CLI implementation, skill registry, and documentation

Special thanks to the broader development community for feedback, testing, and contributions.

---

## Next Steps & Roadmap

### M3C (MAF Refactor Phase) — Q3 2026
- Migrate to Microsoft.Extensions.AI Framework (MAF)
- Add skill namespacing support (e.g., `math/sum`)
- Improve provider discovery and extensibility
- Extended skill format support

### M4+ (Cloud & Advanced Features) — Q4 2026+
- Cloud deployment patterns (Azure Container Apps, ACI)
- Advanced provider orchestration and failover
- Distributed tracing with external exporters
- Premium provider integrations (GPT-4, Claude, etc.)

---

## Getting Help

- **Installation Issues?** See [Troubleshooting](./troubleshooting.md#installation)
- **API Questions?** Check the [REST API Reference](./api-reference.md)
- **Skill Development?** Read [Skill Authoring Guide](./skill-authoring.md)
- **Bug Report?** Open an issue on GitHub

---

## License

See `LICENSE` file in repository root.

**Happy agent building! 🚀**
