# Player presence and spawning

A player joining the world needs a body in a believable place. Dropping every player at the origin is the spatial equivalent of starting every story on the same square.

## A player is a sim

The existing model already points in the right direction: `PlayerControlled` carries a `PlayerId`, and the sim entity owns needs, money, housing and employment. V2 should keep that model.

- The player identity is `PlayerId`.
- The spatial presence is the controlled sim.
- Ownership determines who may move it.
- There is no separate player-position entity to keep in sync.

This means a player inherits all the simulation meaning of being a sim: if the sim is at work, the player is at work; if the sim is homeless, the player has no home entry.

## Spawn resolution

A joining player should spawn at the residence entry when possible:

1. resolve the controlled sim;
2. resolve its household and housing unit;
3. resolve the property placement and entry;
4. if the entry is walkable and free, spawn there;
5. otherwise choose a deterministic nearby walkable tile;
6. if the sim has no residence, use a designated map spawn point near the centre;
7. if that is blocked, use the nearest free walkable tile in stable order.

The fallback is deterministic so reconnects and replays produce the same result.

## Joining, leaving and reconnecting

- On first join, bind the player to the assigned sim and place it if needed.
- On disconnect, keep the sim in the world; do not delete it.
- On reconnect, reuse the same sim and resume from its saved position.
- If the player has no assigned sim, the server assigns one or rejects the connection with an explicit reason.
- Spawning must not move an existing sim that is already in a valid position.

## Multiple players

V2 targets 32 connected players on one world. Spawn resolution must therefore avoid stacking everyone on the same tile:

- a claimed entry tile is occupied by that player's sim for spawn purposes;
- simultaneous spawns are serialized at tick start;
- if two players resolve to the same tile, the later one takes the nearest free tile in stable order;
- a player always sees its own sim even if another player is nearby.

## What the player should experience

- a visible, validated starting position;
- responsive movement confirmed by the server;
- no teleporting through buildings;
- a stable position after reconnect;
- clear feedback when an intent is rejected.
