# HermesNET — .NET 10 Distributed Agent Runtime

| Library | NuGet | Downloads |
|---|---|---|
| ElBruno.Hermes.Core | [![NuGet](https://img.shields.io/nuget/v/ElBruno.Hermes.Core.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Core) | [![Downloads](https://img.shields.io/nuget/dt/ElBruno.Hermes.Core.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Core) |
| ElBruno.Hermes.Adapters | [![NuGet](https://img.shields.io/nuget/v/ElBruno.Hermes.Adapters.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Adapters) | [![Downloads](https://img.shields.io/nuget/dt/ElBruno.Hermes.Adapters.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Adapters) |
| ElBruno.Hermes.Tool | [![NuGet](https://img.shields.io/nuget/v/ElBruno.Hermes.Tool.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Tool) | [![Downloads](https://img.shields.io/nuget/dt/ElBruno.Hermes.Tool.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Tool) |
[![Build Status](https://github.com/elbruno/HermesNET/actions/workflows/ci.yml/badge.svg)](https://github.com/elbruno/HermesNET/actions/workflows/ci.yml)
[![Publish Status](https://github.com/elbruno/HermesNET/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/HermesNET/actions/workflows/publish.yml)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-yellow.svg?style=flat-square)](LICENSE)

A comprehensive runtime for building intelligent agent applications with session persistence, observability, and provider abstraction built on .NET 10.

---

## Quick Start

Get running in three steps:

### 1. Install the .NET tool

```bash
dotnet tool install -g ElBruno.Hermes.Tool
```

### 2. Configure your provider

```bash
hermesnet config
```

This creates a config file at:
- **Windows:** `%APPDATA%\Hermes\appsettings.json`
- **macOS/Linux:** `~/.hermes/appsettings.json`

### 3. Run a sample prompt

```bash
hermesnet chat --profile default --message "Hello! What is 2+2?"
```

> **Note:** First run creates a default profile automatically. See [Quick Start Guide](./docs/quickstart.md) for more examples and workflows.

---

## Documentation

### Getting Started
- **[Quick Start Guide](./docs/quickstart.md)** — Detailed setup and first commands
- **[CLI User Guide](./docs/cli-guide.md)** — Complete workflows and commands

### Reference
- **[CLI Reference](./docs/cli-reference.md)** — Exhaustive command documentation
- **[Troubleshooting](./docs/troubleshooting.md)** — Common issues and solutions

### Development & Architecture
- **[Skill Authoring](./docs/skill-authoring.md)** — Build custom skills
- **[API Reference](./docs/api-reference.md)** — REST API endpoints
- **[User Guide](./docs/user-guide.md)** — Core concepts (profiles, sessions, memory)

### Release & Publishing
- **[Release Notes v2.0.1](./docs/release-notes-v2.0.1.md)** — Latest features
- **[Publishing Guide](./docs/publishing.md)** — Release & NuGet publishing

### Architecture & Testing
- **Architecture Decisions** — See `.squad/decisions.md`
- **Testing & Quality** — See `docs/testing/` folder
- **Benchmarks** — See `docs/benchmarks/` folder
- **M1 Baseline** — See `M1-BASELINE.txt` (telemetry baseline measurements)

---

## Prerequisites

- **.NET 10.0** or later
- **Visual Studio 2025** or VS Code with C# DevKit (recommended)
- An LLM provider:
  - **Local:** [Ollama](https://ollama.ai/) (free, runs locally)
  - **Cloud:** OpenAI or compatible API

---

## Development

### Building from source

```bash
dotnet restore
dotnet build
```

### Running tests

```bash
dotnet test
```

See [Building & Testing Docs](./docs/cli-guide.md#building) for detailed build instructions.

---

## Project Overview

### Core Projects

- **`src/Hermes.Core/`** — Core runtime library (session, chat abstractions, telemetry)
- **`src/Hermes.Host/`** — Application host (DI, provider factory, configuration)
- **`src/Hermes.Cli/`** — Command-line interface (System.CommandLine-based)

### Architecture Highlights

- **Session Persistence** — Built-in conversation and profile storage
- **Provider Abstraction** — Pluggable LLM providers (Ollama, OpenAI, custom)
- **OpenTelemetry** — Distributed tracing and observability from day one
- **C# 13** — Modern language features with nullable safety

---

## Contributing

- All code must build with zero warnings (`TreatWarningsAsErrors=true`)
- Use C# 13 features freely (latest language version)
- Maintain nullable reference type safety
- Cover critical paths with xUnit tests

---

## License

See LICENSE file in repository root.
