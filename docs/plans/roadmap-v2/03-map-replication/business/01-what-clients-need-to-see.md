# What clients need to see

A client needs two different kinds of map information, and mixing them is the fastest way to make replication expensive.

## The map is mostly static

Terrain, roads, lots and building footprints change rarely. A client should receive them once, cache them by map identity and version, and update them only when something actually changes.

A building placed by an admin, completed by construction or removed after demolition is a real change. A sim walking across a tile is not.

## Sims are dynamic

Every tick can change a sim's position, action and needs. Those updates need to be small, frequent and filtered to what the client can plausibly see.

If the full building list traveled with every position update, bandwidth would be dominated by data that did not change. If the static map were never updated, clients would see buildings that no longer exist.

## Two channels

- **Map channel** — full snapshot on connect, versioned deltas on building changes.
- **World channel** — full snapshot on connect, then position/action deltas on acknowledgement.

The channels share identifiers (`NetworkId`) so the client can join a building position to a sim's current location.

## What a client must be able to do

- render the map after one map snapshot;
- know whether its cached map is current;
- request a fresh map after a version gap;
- receive its own sim even if the viewport is empty;
- drop removed buildings and sims;
- interpolate movement between ticks;
- reconnect and resume without a full state reset when possible.

## What a client must not need

- a per-tick map payload;
- every sim in the world when only a small area is visible;
- a direct database or `World` connection;
- authority to decide its own position.

The server owns the truth. The client owns presentation and interpolation.
