# Quality and operations

A browser game client fails in ways a backend test suite cannot see: stale caches, dropped frames, browser throttling, reconnects, input focus and bad device performance.

## The quality bar

- Every decoder and store transition has a deterministic unit test.
- A recorded server fixture drives rendering without a live API.
- Playwright covers the real auth → connect → map → move → interact → reconnect path.
- Frame and payload budgets are measured on a named reference machine.
- A failure names the layer: auth, connection, map, snapshot, interpolation, input, render or HUD.

## What operators need

- Client version and build stamp.
- Connection state and reconnect count.
- Map id/version and snapshot tick.
- Bytes per second and dropped/rejected updates.
- Intent rejection code and rate-limit events.
- No tokens, player PII or full payload dumps in logs.

## Why this belongs in the roadmap

Adding diagnostics after a playtest turns every bug into a guess. The debug overlay and structured client logs are part of the vertical slice, not post-launch polish.
