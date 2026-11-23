# Server authority and smooth presence

The client should feel responsive without pretending it owns the world.

## The contract

- The client sends targets and intents.
- The server computes movement, collision, arrival and interaction.
- The client receives snapshots and deltas and interpolates between them.

## Why interpolate instead of predict

Prediction is valuable for twitch games and expensive for a simulation with pathfinding, building collision and server-side interactions. V3 starts with a short interpolation buffer: the client renders slightly in the past so it can move smoothly between two authoritative ticks.

## What smoothness means here

- Sims do not teleport between 40 ms updates.
- The local player remains visible and prioritised.
- A large correction snaps rather than gliding through walls.
- A full snapshot resets state cleanly.

## What the player should never see

- A position the server never sent.
- A ghost sprite after `RemovedSims`.
- A building that was removed still blocking the view.
- A client-side “success” for an interaction the server rejected.

## Product consequence

Responsiveness comes from input feedback and camera feel first, then from interpolation. Client-side prediction is a later, measured feature, not a V3 requirement.
