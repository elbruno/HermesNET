# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Runtime architecture and scope | Ripley | Phasing, trade-offs, subsystem boundaries, reviewer gates |
| Core runtime and API implementation | Dallas | Sessions, orchestration, REST/CLI surfaces, integration wiring |
| Security and policy controls | Ash | Tool approvals, policy enforcement, privacy/security constraints |
| Memory and storage design | Parker | Curated memory files, retrieval strategy, persistence model |
| Quality and verification | Lambert | Test strategy, edge cases, CI validation, acceptance checks |
| Code review | Ripley | Review PRs, check quality, suggest improvements |
| Testing | Lambert | Write tests, find edge cases, verify fixes |
| Scope & priorities | Ripley | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:ripley` | Lead/architecture tasks and triage | Ripley |
| `squad:dallas` | Runtime/backend implementation tasks | Dallas |
| `squad:ash` | Security and policy tasks | Ash |
| `squad:parker` | Memory/data model tasks | Parker |
| `squad:lambert` | Testing and QA tasks | Lambert |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
