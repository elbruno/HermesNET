# HermesNET — .NET 10 Distributed Agent Runtime

[![NuGet Core](https://img.shields.io/nuget/v/ElBruno.Hermes.Core.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Core)
[![NuGet Adapters](https://img.shields.io/nuget/v/ElBruno.Hermes.Adapters.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Adapters)
[![NuGet Tool](https://img.shields.io/nuget/v/ElBruno.Hermes.Tool.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Tool)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.Hermes.Tool.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Hermes.Tool)
[![Build Status](https://github.com/elbruno/HermesNET/actions/workflows/ci.yml/badge.svg)](https://github.com/elbruno/HermesNET/actions/workflows/ci.yml)
[![Publish Status](https://github.com/elbruno/HermesNET/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/HermesNET/actions/workflows/publish.yml)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-yellow.svg?style=flat-square)](LICENSE)

A comprehensive runtime for building intelligent agent applications with session persistence, observability, and provider abstraction built on .NET 10.

## Installation

### As a Global .NET Tool

```bash
dotnet tool install -g ElBruno.Hermes.Tool
```

Then use:

```bash
hermesnet chat "solve 2+2"
hermesnet profile list
hermesnet session create my-session
```

### Upgrade to latest

```bash
dotnet tool update -g ElBruno.Hermes.Tool
```

## Project Structure

### Core Projects

- **`src/Hermes.Core/`** — Core runtime library
  - Session management and persistence
  - Chat service abstractions
  - Provider interface definitions
  - Telemetry and observability
  - Skill parsing and validation

- **`src/Hermes.Host/`** — Application host and dependency injection
  - Service registration and configuration
  - Provider factory implementation (IChatClient)
  - Application startup and lifecycle management
  - Settings management (appsettings.json)

- **`src/Hermes.Cli/`** — Command-line interface
  - System.CommandLine-based CLI
  - Chat command entry point
  - Session management commands

## Prerequisites

- **.NET 10.0** or later
- **Visual Studio 2025** or VS Code with C# DevKit (recommended)
- **Ollama** (optional, for local provider testing)

## Building

### Restore packages

```bash
dotnet restore
```

### Build the solution

```bash
dotnet build
```

### Build in Release configuration

```bash
dotnet build --configuration Release
```

## Running Tests

```bash
dotnet test
```

### Run tests with coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Architecture

### Solution-Wide Configuration

- **`Directory.Build.props`** — Shared build settings
  - C# 13 language version
  - Nullable reference types enabled
  - Implicit usings enabled
  - Warnings treated as errors for code quality

- **`Directory.Packages.props`** — Centralized NuGet versioning
  - Single source of truth for all package versions
  - Prevents version conflicts across projects

- **`global.json`** — .NET SDK version constraint
  - Enforces .NET 10.0 or later

## Dependencies

### Core Packages

- **Microsoft.Extensions.AI** — IChatClient abstraction for provider integration
- **Microsoft.Extensions.DependencyInjection** — Service container
- **Microsoft.Extensions.Configuration** — Settings management
- **Microsoft.EntityFrameworkCore** — Data persistence (EF Core)
- **System.CommandLine** — CLI framework
- **OpenTelemetry** — Observability baseline

### Testing

- **xUnit** — Testing framework
- **Coverlet.Collector** — Code coverage
- **Microsoft.NET.Test.Sdk** — Test runner

## Quick Start

### Running the CLI

```bash
# Start Ollama locally (if using local provider)
ollama serve

# In another terminal, run the Hermes CLI
dotnet run --project src/Hermes.Cli -- chat "What is 2+2?"
```

**Configuration:** The CLI uses `appsettings.json` in the Hermes.Cli project:
```json
{
  "Provider": "Ollama",  // Switch to "OpenAI" for cloud provider
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama2"
  }
}
```

### Build from Visual Studio

1. Open `HermesNET.slnx` in Visual Studio 2025
2. Build Solution (Ctrl+Shift+B)
3. All three projects compile without warnings

## Documentation

### Release Information
- **[Release Notes v0.1.0](./docs/release-notes-v0.1.0.md)** — First NuGet release, package metadata, and included components
- **[Publishing Guide](./docs/publishing.md)** — GitHub Release + NuGet trusted publishing (no API key)

### Getting Started
- **[Quick Start Guide](./docs/quickstart.md)** — 5-minute setup
- **[User Guide](./docs/user-guide.md)** — Core concepts (profiles, sessions, memory)
- **[CLI Reference](./docs/cli-reference.md)** — Complete command reference

### Development
- **[Skill Authoring](./docs/skill-authoring.md)** — Write custom skills
- **[API Reference](./docs/api-reference.md)** — REST API endpoints
- **[Troubleshooting](./docs/troubleshooting.md)** — Common issues and solutions

### Architecture & Quality
- **Architecture Decisions** — See `.squad/decisions.md`
- **Testing & Quality Gates** — See `docs/testing/`
  - [Test Framework Specification](./docs/testing/TEST-FRAMEWORK.md)
  - [M1 Quality Gates](./docs/testing/M1-QUALITY-GATES.md)
  - [Test Conventions](./docs/testing/TEST-CONVENTIONS.md)
  - [CLI Smoke Test](./docs/testing/CLI-SMOKE-TEST.md)
  - [M1 Task Acceptance Criteria](./docs/testing/M1-TASK-CRITERIA.md)
- **Benchmarks** — See `docs/benchmarks/`
- **M1 Baseline** — See `M1-BASELINE.txt` (OTel baseline measurements)

## OpenTelemetry Instrumentation

Hermes uses **OpenTelemetry** for distributed tracing and observability from Day 1. This enables early detection of performance regressions and overhead measurements.

### Architecture

The instrumentation strategy centers around three span types:

1. **Turn Span** (`hermes.chat.turn`) — Root span wrapping the entire user request
   - Tags: `turn.id`, `message.length`, `response.length`
   - Measures: End-to-end latency (user input → response returned)
   
2. **Provider Call Span** (`hermes.provider.call`) — Child span for ChatClient calls
   - Tags: `provider.name`, `provider.latency_ms`
   - Measures: Provider-specific latency (isolated from CLI overhead)
   
3. **Session Persist Span** (`hermes.session.persist`) — Async background span
   - Tags: `session.id`
   - Note: Not included in turn latency measurement (async, background operation)

### Usage

Instrumentation is accessed via `Hermes.Core.Telemetry.TelemetryProvider`:

```csharp
using Hermes.Core.Telemetry;

// Start a turn span
using (var turn = TelemetryProvider.StartTurnSpan(turnId))
{
    TelemetryProvider.SetMessageLength(turn, message.Length);
    
    // Start a provider call span
    using (var provider = TelemetryProvider.StartProviderCallSpan("Ollama"))
    {
        var response = await chatClient.CompleteAsync(messages);
        TelemetryProvider.SetProviderLatency(provider, elapsedMs);
    }
    
    TelemetryProvider.SetResponseLength(turn, response.Length);
}
```

### Configuration

OpenTelemetry is initialized in `Hermes.Cli/Program.cs`:

```csharp
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Hermes.Core")
    .AddConsoleExporter()  // M1: console logging for development
    .Build();
```

For production, replace `AddConsoleExporter()` with appropriate exporters (OTLP, Jaeger, etc.).

### Baseline Measurement

The M1 baseline establishes a performance reference point with OTel fully enabled:

- **P95 Turn Latency:** 55ms (local Ollama, console exporter active)
- **Target:** < 100ms with OTel ON
- **Results:** Committed to `M1-BASELINE.txt`

This baseline is the reference point for M2's "no >20% OTel overhead" regression gate.

## CLI Tool

HermesNET is available as a global .NET tool:

```bash
dotnet tool install -g hermesnet
hermes profile create myprofile
hermes chat --profile myprofile --message "Hello!"
```

**Get started:** See [Quick Start Guide](docs/quickstart.md) or the [Full CLI User Guide](docs/cli-guide.md)

---

## Contributing

- All code must build with zero warnings (`TreatWarningsAsErrors=true`)
- Use C# 13 features freely (latest language version)
- Maintain nullable reference type safety
- Cover critical paths with xUnit tests

## License

See LICENSE file in repository root.
