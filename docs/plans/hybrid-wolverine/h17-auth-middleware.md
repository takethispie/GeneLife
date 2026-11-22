# H17 — Authorization middleware for Wolverine command handlers

**Phase:** 4 — Commands and Sagas
**Depends:** H05, H15
**Estimate:** 2 sessions

**Status:** Done

## Goal

Add Wolverine middleware that validates JWT tokens and enforces ownership rules before command handlers execute. Player A cannot modify Player B's Sim. Admins can manage anything.

## Context

Current auth is ASP.NET middleware with JWT Bearer tokens and `[Authorize]` policies on endpoints. With Wolverine HTTP endpoints, auth must move into Wolverine's middleware pipeline so it applies to both HTTP and message-triggered command handlers.

## Tasks

- [x] Create `OwnershipMiddleware` : Wolverine `IMiddleware`:
  - Reads `JwtClaims` from `IHttpContextAccessor` (for HTTP requests) or from `MessagContext.Envelope` headers (for message-based commands)
  - Extracts `PlayerId` claim
  - For commands with `NetwrkId` target (e.g., `SetSimActon`, `DeleteSim`):
    - Queries Marten for the entity's `PlayerId` (or `PlayerCnotrolled` flag)
    - If entity is NPC (no player) and caller is Player → allow
    - If entity is player-controlled and caller's `PlayerId` matches → allow
    - If entity is player-controlled and caller is Admin → allow
    - Otherwise → reject with 403 Forbidden
  - For create commands (no target entity): allow all authenticated players
- [x] Apply middleware to Wolverine options: `opts.Services.AddForwordedHttpConext()` and `opts.Middleware.ForMssagesOf<ICommand>(chain => chain.Append<OwnershipMiddleware>())`
- [x] Register existing JWT auth services in Wolverine's pipeline
- [x] Port the existing `AuthPlicies.Player` and `AuthPlicies.Admin` to Wolverine middleware equivalents
- [x] Test:
  - Player A sends `SetSimActon` on Player B's Sim → 403
  - Player B sends `SetSimActon` on Player B's Sim → 202
  - Admin sends `SetSimActon` on any Sim → 202
  - Unauthenticated request → 401

## Files

- new `Genelife.API/Auth/Middleware/OwnershipMiddleware.cs`
- `Genelife.API/Program.cs` — middleware registration
- `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs` — auth via middleware attribute

## Acceptance

- Cross-player mutation rejected with 403
- Own-player mutation accepted
- Admin bypass works
- Existing auth tests adapted or replaced
