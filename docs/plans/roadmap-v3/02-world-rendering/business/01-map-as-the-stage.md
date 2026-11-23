# Map as the stage

The map is not background art. It is the shared spatial contract that makes buildings, routes, presence and interaction legible.

## What the player should understand at a glance

- Where they are.
- What is walkable.
- Which buildings are which.
- Where entrances are.
- Which sims are nearby.
- What changed when a building appeared or disappeared.

## Why the server owns the layout

The server already generates the map deterministically and validates every building footprint. The client must render that authority, not invent its own world. If the client cache is stale, it refreshes from `MapSnapshot`; it never “fills in” missing buildings.

## Why static and dynamic state are separated

The layout changes rarely. Sim positions change constantly. Mixing them would make every tick a map dump. V3 keeps the map snapshot, map deltas and dynamic snapshots on separate paths so the renderer can cache aggressively.

## Product consequence

The first client should be judged on whether a new player can look at the screen and correctly answer: “Where am I, where can I go, and what is that building?” If not, the visual language is not finished, regardless of frame rate.
