# Step 07 — Identity and roles

**Depends:** none
**Goal:** Authenticate players and separate player from admin capabilities.

## Tasks

- [ ] Choose auth (ASP.NET Identity, JWT, or OIDC); document the choice.
- [ ] Add `PlayerId` as a claim/principal value.
- [ ] Map connection/session → `PlayerId`.
- [ ] Add roles `Player` and `Admin`.
- [ ] Protect start/stop/add/delete admin endpoints with the Admin role.
- [ ] Return 401/403 consistently.
- [ ] Add a connection registry (PlayerId → active connections).

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
