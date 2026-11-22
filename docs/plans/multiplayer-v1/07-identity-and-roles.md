# Step 07 — Identity and roles

**Depends:** none
**Goal:** Authenticate players and separate player from admin capabilities.

**Status:** Done

## Auth decision

**JWT bearer tokens (HMAC-SHA256)** issued and validated by the API. This keeps the
minimal API self-contained: no external identity provider, no database, and no
cookie/UI flow is needed yet.

- Configuration lives under `Auth:Jwt` (`Issuer`, `Audience`, `Key`, `AdminSecret`,
  `TokenLifetimeMinutes`). `Key` must be at least 32 bytes; when it is missing an
  ephemeral key is generated so the app still runs locally, but tokens become invalid
  after a restart.
- Tokens carry `sub` and `player_id` claims plus a `role` claim of `Player` or `Admin`.
  Admin tokens also carry `Player`, so admins can use read endpoints.
- `POST /api/auth/token` is a development bootstrap endpoint: it issues a `Player`
  token for any `playerId`, and only issues `Admin` when the request supplies the
  configured `AdminSecret`. Replace it with a real identity provider before production.
- Every endpoint requires an authenticated player by default (fallback policy). Read
  endpoints require the `Player` policy; mutation/admin endpoints require `Admin`.
  The token endpoint is the only `AllowAnonymous` endpoint.
- `IConnectionRegistry` is a singleton map of transport connection id ↔ `PlayerId` for
  the later SignalR transport step. `ClaimsPrincipal.GetPlayerId()` exposes the player
  id to endpoints, commands, and the future hub.
- JWT challenge/forbidden events return consistent JSON bodies for `401` and `403`.

## Tasks

- [x] Choose auth (ASP.NET Identity, JWT, or OIDC); document the choice.
- [x] Add `PlayerId` as a claim/principal value.
- [x] Map connection/session → `PlayerId`.
- [x] Add roles `Player` and `Admin`.
- [x] Protect start/stop/add/delete admin endpoints with the Admin role.
- [x] Return 401/403 consistently.
- [x] Add a connection registry (PlayerId → active connections).

## Files likely touched

- `Genelife.Api/Program.cs`
- new `Genelife.Api/Auth/*`
- `Genelife.Api/Endpoints/*`

## Acceptance

- Unauthenticated requests are rejected.
- Admin-only operations require Admin.
- `PlayerId` is available to commands and the hub.

## Testing

- Authorization matrix: anonymous / Player / Admin per endpoint.
- Implemented in `Genelife.UnitTests/Auth/AuthorizationTests.cs` using
  `WebApplicationFactory<Program>`, plus registry and claim-helper unit tests.
