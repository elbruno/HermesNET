# Skill ID: summarise-memory
**Version:** 1.0
**Description:** Summarises the current memory context for the active profile
**Type:** memory
**Category:** memory

## Metadata
- Scope: profile
- Requires: memory-service

## Implementation Notes
Reads the active profile's curated memory store and produces a concise summary.
Output can be injected as context into subsequent chat requests.

M3 hook: Memory context scoping (per Parker T15) will influence how this skill
accesses data once profile-isolated memory queries are fully wired.
