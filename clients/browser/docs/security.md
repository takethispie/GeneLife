# Browser client security

## Trust boundaries

- The browser is untrusted. The server validates ownership, map bounds,
  walkability, path reachability, entry range, roles and rate limits.
- The client renders server state. It never applies a move or interaction locally
  before the server acknowledges it.
- `IntentResult` is admission feedback, not proof of application. The
  authoritative result is the next snapshot; `IntentRejected` explains failures
  that happen after admission.

## Token handling

- `POST /api/auth/token` returns a short-lived JWT. The client keeps it in memory.
- The token is mirrored to `sessionStorage` for same-tab reload recovery. This is
  an explicit XSS tradeoff:
  - Do **not** move it to `localStorage`; tokens should die with the tab.
  - Do **not** log it. `buildHubUrl` adds it to the hub query, and no logger prints
    the URL.
  - Sign-out clears the mirror.
- On reload, an expired mirrored token is discarded and the client signs in again.
- Refreshes happen inside a five-minute margin; a failed refresh moves the session
  to `expired` and requires a new sign-in.

## XSS

- Every server-provided string (map names, building names, districts, errors,
  notification text) is inserted with `textContent`/`createTextNode`. There is no
  `innerHTML` in the client.
- REST responses are treated as `unknown` and structurally validated.
- The production host should send a restrictive Content-Security-Policy. The app
  loads only its own bundle and assets; no third-party scripts are required.
- JSON payloads from the hub are validated before they mutate the store. Unknown
  tile flags, overlapping RLE runs, unsupported protocol versions and malformed
  deltas fail closed.

## Transport and CORS

- Use HTTPS/WSS in deployed environments.
- CORS keeps an explicit origin allowlist and allows credentials because SignalR's
  browser negotiate uses `credentials: include`. Do not combine
  `AllowCredentials()` with a wildcard origin.
- The hub requires a bearer token. The token is passed as `access_token` because
  browser WebSocket handshakes cannot set arbitrary headers.
- The API rate-limits snapshot requests, viewport changes and intents.

## Client-side hardening

- Configuration validation fails before any request when the API URL is missing
  or invalid.
- A protocol-version mismatch stops the connection and shows a visible error.
- Map and snapshot cache keys include versions. Gaps force a full refresh instead
  of rendering mixed generations.
- Sprite pools and frame buffers have fixed caps to avoid unbounded growth from a
  misbehaving server.
- The client never evaluates code from server payloads.

## Operational checks

- `npm audit` / dependency review in CI.
- Keep PixiJS and `@microsoft/signalr` current.
- Do not add analytics or error reporters that capture tokens, player ids or raw
  hub payloads without a privacy review.
- Restrict `/api/auth/local-admin` to loopback or remove it in production.
