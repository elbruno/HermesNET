---
name: "nuget-trusted-publishing"
description: "Publishing HermesNET NuGet releases through GitHub Releases and OIDC trusted publishing"
domain: "release-management"
confidence: "high"
source: "earned — v0.1.0 release preparation"
---

## Context

Use this pattern when shipping HermesNET packages to nuget.org. The release version is centralized, and the publish workflow derives the package version from the GitHub Release tag or a workflow override.

## Patterns

- Keep the package version in `Directory.Build.props`.
- Use `publish.yml` with `NuGet/login@v1` and the `release` environment.
- Publish all packable outputs together: `Hermes.Core`, `Hermes.Adapters`, and `hermesnet`.
- Keep `Hermes.Host` non-packable.

## Examples

- Tag the release `v0.1.0` to publish version `0.1.0`.
- Use `workflow_dispatch` with `version=0.1.0` when manually overriding the release version.

## Anti-Patterns

- Do not add a long-lived NuGet API key to the repo.
- Do not publish packable projects independently.
- Do not duplicate the version in multiple project files.
