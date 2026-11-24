# Why the World Lock Matters

The simulation keeps one shared world. A single lock guards that world, and the tick thread holds it for the whole tick. Request threads and the broadcast service also need the world, so they wait on the same lock.

When the tick is long, request threads wait. When requests are heavy, the tick waits. Either way, the loop can fall behind and drop ticks. The lock is the place where server load and simulation load meet.

Tier 2 work reduces that meeting point:

- The tick mutates under the lock and publishes outside it, so readers never block the tick.
- Read-only accessors stop serializing against each other.
- Monthly work is spread across the month instead of landing on one tick.
- Repeated world passes are merged.
- Metrics and pathfinding stop adding avoidable cost.

The goal is not to remove the lock. It is to make the time spent holding it short and predictable, so the tick keeps its 20 Hz cadence under load.
