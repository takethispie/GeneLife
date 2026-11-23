# H04 — Security, abuse and versioning

**Phase:** 4 — Safety
**Depends:** F03, T05, I02
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Keep tokens, server strings and player data safe; avoid client-side authority; and make client/server version mismatches explicit.

## Context

The browser client is an untrusted environment. The server already validates ownership, bounds, walkability, range and roles. The client must not weaken that boundary or leak secrets.

## Design

### Token handling

- Prefer memory-only token; `sessionStorage` only with a documented XSS tradeoff.
- Never put tokens in query strings outside the hub auth parameter, logs or analytics.
- Clear token and connection on sign-out and auth failure.
- Do not store admin tokens in the game client.

### Rendering safety

- Server strings are rendered with `textContent`, never `innerHTML`.
- Validate payload shape and bounds at the network boundary.
- Do not evaluate server strings as code or CSS.
- Treat map/building names as untrusted.

### Abuse

- Client-side rate limits are UX, not security.
- Do not retry rejected intents or snapshot requests in a tight loop.
- Respect `SnapshotRejected` and back off.
- Do not expose hidden state by requesting an oversized viewport; the server clamps it.

### Versioning

- Inject a build stamp and client protocol version.
- Log a clear warning when the server map/snapshot version is unsupported.
- The client must fail closed on unknown map versions, not render partial layouts.
- Document the minimum server commit/contract version for a release.

## Tasks

- [ ] Add token storage and sign-out review.
- [ ] Add untrusted-string tests.
- [ ] Add payload validation at the network boundary.
- [ ] Add backoff for rejections and reconnect.
- [ ] Add build/protocol version stamp.
- [ ] Add a dependency audit to CI.
- [ ] Document the version compatibility policy.

## Files

- `clients/browser/src/auth/session.ts`
- `clients/browser/src/net/validate.ts`
- `clients/browser/src/config/build-info.ts`
- `clients/browser/docs/security.md`

## Acceptance

- No token appears in logs, build output or analytics.
- Server strings cannot inject HTML.
- Rejections and reconnect use bounded backoff.
- Unknown map versions fail closed.
- The client reports its build/protocol version.
- Dependency audit is part of CI.

## Testing

- Token-leak grep.
- XSS/string-rendering test.
- Backoff test.
- Version-mismatch test.
- Dependency audit.
