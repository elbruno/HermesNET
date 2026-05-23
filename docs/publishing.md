# Publishing HermesNET to NuGet

HermesNET publishes NuGet packages through **GitHub Releases + NuGet Trusted Publishing**.

## Rule

- Use the GitHub Actions `publish.yml` workflow.
- Authenticate with NuGet via OIDC trusted publishing.
- Do **not** store or use a long-lived NuGet API key in the repo.
- Publish every packable package together: `Hermes.Core`, `Hermes.Adapters`, and the `hermesnet` global tool.
- For the v0.1.0 release, create the GitHub Release with tag `v0.1.0` or pass `version=0.1.0` to workflow dispatch.

## Setup

1. Add a trusted publisher entry on nuget.org for this repository.
2. Create a GitHub `release` environment.
3. Run the publish workflow from a published GitHub Release or manually with a version override.

## Workflow behavior

- Builds and tests the solution.
- Packs all NuGet-shippable projects.
- Pushes the produced `.nupkg` files to nuget.org with `--skip-duplicate`.

## Notes

- The host project is not packable.
- Package version is centralized from the repo build props.
