# Skill ID: rate-limit-policy
**Version:** 1.0
**Description:** Enforces a per-profile rate limit on chat requests
**Type:** policy
**Category:** policy

## Metadata
- MaxRequestsPerMinute: 60
- Scope: profile
- EnforcedBy: middleware

## Implementation Notes
Declares a rate-limiting policy for the chat pipeline. Evaluated before each
chat request. If the rate limit is exceeded, the request is rejected with a
429-equivalent error.

M3 note: Ash (T16) will review metadata format requirements for policy injection
before this skill is fully operative.
