# Lambert Release: v0.1.0 NuGet Publishing

**Date:** 2026-05-23T11:11:39.297-04:00
**Status:** READY FOR REVIEW

## Decision

HermesNET v0.1.0 should publish through the existing GitHub Release → NuGet Trusted Publishing path. The release version is centralized at `0.1.0` in `Directory.Build.props`, and the publish workflow should pack `Hermes.Core`, `Hermes.Adapters`, and the `hermesnet` global tool together.

## Notes

- Use tag `v0.1.0` for the release trigger.
- No long-lived NuGet API key should be introduced.
- `Hermes.Host` remains non-packable.

