# Step 09 — Ownership and authorization

**Depends:** 08
**Goal:** Ensure a player can only act on entities they own.

## Tasks

- [ ] Add ownership (`PlayerId` on Sim or a player→Sim map).
- [ ] Validate every intent: target exists, is controlled by the sender, is in range.
- [ ] Reject spoofed or unknown `NetworkId`.
- [ ] Allow Admin override for moderation.
- [ ] Define spawn/assign/transfer ownership rules.
- [ ] Log authorization failures without leaking state.

## Files likely touched

- `Genelife.Api/Commands/*`, `Genelife.Api/Auth/*`
- `Genelife.Api/Services/SimulationEngine.cs`

## Acceptance

- Cross-player actions are rejected.
- Admin actions succeed where intended.
- No intent can mutate an entity the sender does not own.

## Testing

- Two-player isolation matrix (move/interact/use).
- Spoofed NetworkId test.
