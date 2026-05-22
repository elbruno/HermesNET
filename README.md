# HermesNET — .NET 10 Distributed Agent Runtime

A comprehensive runtime for building intelligent agent applications with session persistence, observability, and provider abstraction built on .NET 10.

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

- **Microsoft.Extensions.AI.Abstractions** — IChatClient abstraction
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
dotnet run --project src/Hermes.Cli -- chat --profile default --message "Hello"
```

### Build from Visual Studio

1. Open `HermesNET.slnx` in Visual Studio 2025
2. Build Solution (Ctrl+Shift+B)
3. All three projects compile without warnings

## Documentation

- **Milestone 1 Plan** — See `docs/research/plan.md`
- **Architecture Decisions** — See `.squad/decisions.md`
- **Benchmarks** — See `docs/benchmarks/`

## Contributing

- All code must build with zero warnings (`TreatWarningsAsErrors=true`)
- Use C# 13 features freely (latest language version)
- Maintain nullable reference type safety
- Cover critical paths with xUnit tests

## License

See LICENSE file in repository root.
