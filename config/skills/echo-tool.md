# Skill ID: echo-tool
**Version:** 1.0
**Description:** Echoes input text back to the caller unchanged
**Type:** tool
**Category:** utility

## Metadata
- Input: { text: string }
- Output: { result: string }
- Callable: true

## Implementation Notes
Simple echo tool for testing and diagnostics. Accepts any text input and returns
it verbatim. Useful as a baseline integration test for tool-type skills.
