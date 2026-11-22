# Static layout and dynamic state

The map and the population change at completely different rates. Treating them as one payload makes every position update carry the whole city.

## Static layout

The static layout includes:

- map bounds and tile size;
- terrain, roads and zones;
- building footprints and entries;
- map identity and version.

It is large relative to a single sim update, but it changes rarely. Sending it once and caching it is the natural design.

## Dynamic state

Dynamic state includes:

- sim position, movement target and action;
- needs, health and stage;
- employment and housing summaries needed by the client;
- player presence.

It changes often and should be tiny. The existing full/delta snapshot path already supports this for sims and companies; V2 extends the same idea to tiles and movement.

## Versioning is the bridge

A `MapVersion` increments when the layout changes. A map delta moves the client from one version to the next. If a client reconnects with a version that is no longer in the retained map history, the server sends a full map snapshot.

This is the same reconnect logic as the world snapshot, applied to a slower-changing channel.

## Why not put buildings in the world snapshot?

It would work for a tiny map and fail at scale:

- every position tick would repeat the building list;
- delta comparison would scan buildings every tick;
- a reconnect would resend the map even when nothing changed;
- a building change would be mixed into movement updates and harder to cache.

A separate map channel keeps both payloads honest: the map is static and versioned, the world is dynamic and acknowledged.

## What the admin gets

The admin map console uses the same two facts:

- a static map read model for layout and footprints;
- live dynamic positions for players and sims.

That separation also makes the console useful when the simulation is stopped: it can render the last map and last known positions without pretending the world is still ticking.
