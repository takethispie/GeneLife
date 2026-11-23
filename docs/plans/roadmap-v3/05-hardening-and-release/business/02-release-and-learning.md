# Release and learning

V3 is a learning release: the goal is to discover whether the world is understandable and playable in a browser, not to ship a finished game.

## What a release should prove

- New players can connect and orient themselves.
- The map and buildings are legible.
- Movement feels responsive enough for a simulation client.
- Reconnect does not break the session.
- The client stays within reasonable frame and payload budgets.
- Failures are diagnosable without developer tooling on the player’s machine.

## What to measure

- Time to first rendered map.
- Time from click to accepted move.
- Disconnects per session and recovery success.
- Intent rejection rate and top rejection codes.
- Frame time and payload size at the target scale.
- Qualitative confusion reported by playtesters.

## What not to promise

- Native performance or Steam distribution.
- Client-side prediction.
- Mobile controls.
- Social or account features.

## Next roadmap trigger

If the browser slice is fun and understandable, the next decision is whether to deepen the browser client (prediction, audio, richer HUD) or start a Godot desktop track. Do not start both before the slice has evidence.
