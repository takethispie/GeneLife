# Why browser first

V3 starts with a browser client because it is the cheapest way to test whether the simulated world is actually playable.

## The problem

The server already produces a map, moves sims, replicates snapshots and accepts intents. What it has never had is a player-facing view. Building a native client first would make us pay for engine integration, platform packaging and distribution before we know whether the map, movement and interaction loop are fun.

## Why the browser wins for the first slice

- The official SignalR JavaScript client removes transport work.
- The existing JWT flow and HTTP API are directly usable; a browser origin only needs CORS configuration.
- PixiJS gives us WebGL rendering without an engine or export pipeline.
- A static build deploys in seconds and can be previewed by anyone with a link.
- Iteration is fast: edit, refresh, inspect, repeat.
- The same client can later be wrapped by Tauri or Electron if a packaged desktop app is enough.

## What the browser does not solve

- Native controller support, Steam integration and OS-level distribution.
- High-end 2D presentation and animation tooling that Godot provides.
- Guaranteed frame pacing across every machine.

Those are reasons to revisit a native client later, not reasons to start there.

## Decision

V3 is browser-only. Godot remains a future option after the vertical slice proves the gameplay and the contracts.
