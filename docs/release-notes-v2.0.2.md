# Release Notes v2.0.2

## Security

- OpenAI API keys now stay out of JSON config and are stored in the native OS credential store.
- Legacy plaintext API keys migrate on first load/save.
- `doctor` masks secret presence and the host/CLI read the same secret key path.

## Docs

- Updated CLI guide, CLI reference, and README for secure-at-rest configuration.
