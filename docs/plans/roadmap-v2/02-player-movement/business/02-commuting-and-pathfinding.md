# Commuting and pathfinding

Not every sim needs to walk every tile. A believable simulation needs NPCs to be in the right place at the right time, while a player needs fine-grained movement that responds immediately.

## Two movement modes

### Player movement

A player-controlled sim needs real movement:

- a target chosen by the player;
- collision against buildings and blocked terrain;
- a route through walkable tiles;
- arrival at an entrance or interaction point;
- frequent position updates to the client.

### NPC commuting

An NPC mainly needs to be at home, at work, at school or at a service. V1 already treats commuting as abstract. V2 keeps that abstraction for most NPCs:

- at shift start, place the NPC at the office entry;
- at shift end, place it at the home entry;
- if the NPC is visible to a client, optionally animate the last leg;
- never run A* for thousands of NPCs every tick.

This is not a compromise in realism; it is what keeps the simulation affordable. The important part is that the entry tiles are real, validated and consistent with the same map players use.

## Pathfinding rules

- A* on the walkable tile grid.
- Four-direction movement in V2 unless diagonal movement is explicitly approved; fewer edge cases and simpler collision.
- Buildings, water and reserved blocked tiles are not walkable.
- Path requests are budgeted per tick, with a queue and a deterministic request order.
- Routes to common destinations such as building entries are cached.
- A failed path request leaves the sim in place and produces a rejection reason.
- Path state lives in a derived index, not inside ECS components with managed lists.

## When to switch modes

A practical rule set:

- player-controlled sims always use fine movement;
- NPCs use entry teleports for off-screen shifts;
- NPCs that enter a client viewport may be interpolated to their entry without pathfinding;
- a future feature can promote visible NPCs to full pathfinding when the performance budget allows.

The existing `ScalingTriggers` already treats pathfinding and decision cost as scaling signals. V2 should measure them rather than guess.

## Why entries matter

A path should end at a door, not at an arbitrary tile inside a building. Entries give movement, interactions and replication one shared target. They also make the visual claim "this sim is at work" semantically true.
