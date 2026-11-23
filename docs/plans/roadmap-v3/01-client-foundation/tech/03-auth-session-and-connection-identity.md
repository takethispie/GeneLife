# F03 — Auth, session and connection identity

**Phase:** 3 — Identity
**Depends:** F01, F02
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Obtain a player JWT, keep it valid for the session, connect to `/hubs/world` with it, and expose auth/connection state to the rest of the client.

## Context

`POST /api/auth/token` accepts `{ playerId }` and returns:

```json
{
  "accessToken": "...",
  "tokenType": "Bearer",
  "expiresAt": "2026-11-22T23:00:00Z",
  "playerId": "...",
  "roles": ["Player"]
}
```

The hub reads `access_token` from the query string for `/hubs` requests. Unauthenticated hub connections are rejected.

## Design

### Session store

```ts
type SessionState =
  | { status: "anonymous" }
  | { status: "authenticating"; playerId: string }
  | { status: "authenticated"; playerId: string; token: string; expiresAt: number }
  | { status: "expired"; playerId: string }
  | { status: "rejected"; playerId: string; reason: string };
```

- Keep the token in memory.
- Optionally mirror to `sessionStorage` for reload recovery; clear on sign-out.
- Refresh when `expiresAt - now < refreshMargin` (start with 5 minutes).
- Never log the token.

### Hub connection

```ts
new HubConnectionBuilder()
  .withUrl(`${HUB_URL}?access_token=${encodeURIComponent(token)}`)
  .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
  .configureLogging(LogLevel.Warning)
  .build();
```

Connection states exposed to the UI: `disconnected`, `connecting`, `connected`, `reconnecting`, `failed`, `auth-expired`.

### CORS

Add the client dev origin (for example `http://localhost:5173`) to `Cors:AllowedOrigins` in `appsettings.Development.json`. Production uses the deployed origin.

## Tasks

- [ ] Implement the auth HTTP client and session store.
- [ ] Implement token refresh and expiry handling.
- [ ] Build the hub connection with `access_token`.
- [ ] Map SignalR connection events to explicit session states.
- [ ] Add the client dev origin to development CORS config.
- [ ] Add a sign-out path that clears token and connection.
- [ ] Add tests for token expiry and rejected auth.

## Files

- new `clients/browser/src/auth/session.ts`
- new `clients/browser/src/auth/token-client.ts`
- new `clients/browser/src/net/hub-connection.ts`
- `Genelife.API/appsettings.Development.json` (CORS origin only)
- new `clients/browser/src/auth/*.test.ts`

## Acceptance

- A valid player id obtains a token and connects.
- An expired token is refreshed before the hub call.
- A rejected token leaves the client in an explicit error state, not a retry loop.
- Sign-out clears the token and closes the connection.
- The token never appears in console logs or error reports.

## Testing

- Token response parsing.
- Expiry/refresh boundary.
- Hub connection URL includes the token.
- Unauthorized response handling.
- CORS smoke test from the dev origin.
