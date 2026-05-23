# HermesNET 0.1.0 — NuGet Release

**Release Date:** 2026-05-23T11:11:39.297-04:00  
**Version:** 0.1.0

---

## Headline

HermesNET 0.1.0 is the first NuGet-published release of the current HermesNET toolchain. It ships the `ElBruno.Hermes.Tool` global tool package and the reusable runtime packages under trusted publishing.

---

## Included Packages

- `ElBruno.Hermes.Core`
- `ElBruno.Hermes.Adapters`
- `ElBruno.Hermes.Tool` global tool

## Release Notes

- Centralized versioning now publishes `0.1.0` from `Directory.Build.props`
- GitHub Actions uses NuGet Trusted Publishing (OIDC)
- The host project remains non-packable
