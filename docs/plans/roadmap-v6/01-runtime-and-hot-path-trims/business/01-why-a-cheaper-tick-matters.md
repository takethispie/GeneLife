# Why a Cheaper Tick Matters

The world runs at 20 ticks per second. Every tick has 50 ms to do its work. When a tick takes longer, the loop falls behind and drops ticks — simulated time is lost. Dropped ticks are the earliest signal that the server cannot keep up.

Tier 1 work is about removing waste that has no gameplay value:

- Build settings that let the runtime handle allocation pressure better.
- Overrun logging that does not make the overrun worse.
- A company snapshot that queries offices once instead of once per company.
- Appearance that is derived only when it changes, not every tick.
- Event lists that are reused instead of reallocated.

None of these change what players see. They reduce the cost of producing the same world. That headroom is what lets the 2,000-genos world and the 32-player load stay inside the budget without cutting features.

The measure of success is simple: lower allocation per tick, lower p95 and p99, and no dropped ticks at the target load.
