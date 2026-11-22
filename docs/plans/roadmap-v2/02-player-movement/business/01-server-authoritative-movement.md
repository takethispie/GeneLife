# Server-authoritative movement

A player-controlled sim is only meaningful if the server owns its position. If the client can declare where it is, the map, collision, interactions and replication all become suggestions.

## The client asks; the server decides

The client sends an intent: "I want to go to this tile." It does not send a new position, a velocity or a teleport. The server:

1. identifies the player and the controlled sim;
2. checks that the player owns that sim;
3. checks that the target is inside the map and walkable;
4. checks that the sim is alive and allowed to move;
5. computes a route or a step;
6. updates the authoritative position;
7. replicates the result.

Anything else is a request, not a fact.

## Why this matters beyond cheating

Server authority also gives consistent behaviour:

- two clients cannot disagree about where the same sim is;
- a reconnecting client can resynchronise from the server;
- collision is enforced even if a client has an old map;
- save/restore resumes from a trusted position;
- an admin can inspect the same state the game uses.

The client still feels responsive because it interpolates between server ticks and may show an immediate local echo. The echo is presentation only until the server confirms it.

## Rejections are part of the contract

A movement intent can fail for reasons the client should understand:

- unknown target sim;
- not owned by this player;
- target out of bounds;
- target blocked by a building or water;
- no path;
- sim dead or otherwise unavailable;
- rate limited.

Each rejection should be explicit. Silent dropping makes movement feel broken and hides real bugs.

## Tick-based authority

Movement advances on the simulation tick, using the fixed delta time. The server never uses wall-clock time for movement because that would make replays and save/restore non-deterministic.

This means:

- speed is expressed in tiles per second;
- movement progress is a function of ticks, not real time;
- a paused simulation pauses players too;
- a slow tick does not let a player jump farther than the per-tick cap.

## What the admin should see

- each player's controlled sim and last confirmed tile;
- current target and whether a path exists;
- rejected intents by reason;
- movement rate and pathfinding budget;
- clients whose acknowledged tick is far behind.
