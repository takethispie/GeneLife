# Interest management and scale

Sending every sim to every player is fine for a demo and expensive for a populated world. Interest management is how the backend keeps multiplayer replication proportional to what each client can actually see.

## The problem

At 2,000 NPCs and 32 players:

- a full sim list is large;
- most sims are irrelevant to a given player;
- position deltas every tick add up quickly;
- reconnects can trigger full payloads at the worst time.

The existing `ScalingTriggers` already recommends `InterestManagement` when bytes per client per second exceed 64 KB/s. V2 should make that recommendation actionable.

## The approach

Each connection has a viewport:

- a center tile and a radius;
- a default derived from the player's sim;
- an explicit subscription when the client pans or zooms.

The server filters:

- sims inside the radius;
- buildings intersecting the viewport;
- the player's own sim, always;
- optionally nearby players as a priority set.

A player should not receive hidden NPC details just because the server knows them.

## Hysteresis and refresh

A hard radius causes entities to pop in and out at the boundary. A small hysteresis band and a refresh cadence smooth this:

- enter at `radius`;
- leave at `radius + margin`;
- refresh the visible set on a bounded cadence, not every tick.

The exact policy belongs in configuration, not in the client.

## Cost control

- Group connections by viewport and acknowledged tick so one filtered delta serves many clients.
- Use `SpatialIndex` for sims and `WorldMap` for buildings.
- Never scan the entire world per client per tick.
- Measure bytes per client and fall back to a smaller radius if a client is over budget.
- Keep the own-player sim in every viewport, even if the radius is zero.

## What this enables

- more players without linear bandwidth growth;
- stable frame-rate-friendly payload sizes;
- a map console that can request a viewport instead of the whole world;
- a clear performance story for the next roadmap.
