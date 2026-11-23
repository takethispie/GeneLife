# Controls, feedback and onboarding

The first browser slice needs to teach movement and interaction without a tutorial screen.

## The minimum control set

- Camera pan and zoom.
- Select a tile, sim or building.
- Move to a tile.
- Interact with a building when in range.

## Feedback rules

- Show a target marker before the server accepts the move.
- Show a short “moving”, “blocked”, “out of range” or “no path” result.
- Do not fake success for an interaction; wait for the server result.
- Keep the local player and current target distinguishable from nearby sims.

## Onboarding

The HUD should make the first minute self-explanatory:

- “Click a tile to move.”
- “WASD to pan. Scroll to zoom.”
- “Click a building to inspect.”
- A visible connection status and player id.

## Product consequence

If a player needs the debug overlay to understand why a click did nothing, the feedback layer failed. The debug overlay is for developers; the HUD is for players.
