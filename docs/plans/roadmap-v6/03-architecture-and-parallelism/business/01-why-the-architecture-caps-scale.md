# Why the Architecture Caps Scale

After tiers 1 and 2, two limits remain. The tick runs on a thread shared with web requests, and all world work is serial on one thread. Neither is a bug; both are architecture choices that cap how far the world can grow.

Tier 3 changes those choices:

- The simulation gets its own thread, so request load cannot delay the tick.
- Independent systems run in parallel, so more genoses fit in the same tick budget.
- Snapshots are built off the tick thread, so reporting does not compete with simulation.

This is the highest-risk tier. Parallelism can break determinism, and moving the tick off the thread pool changes timing. Every step must be proven on the replay suite before it is kept.

The payoff is headroom beyond 2,000 genoses without dropping ticks or cutting the world's depth.
