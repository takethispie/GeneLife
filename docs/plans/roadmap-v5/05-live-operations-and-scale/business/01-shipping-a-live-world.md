# Shipping a live world

A roadmap is not finished when the features compile. It is finished when a player can join a seeded world, live a life, meet someone, survive a restart and get help when something goes wrong.

V5 treats release as a feature with its own acceptance criteria.

## What shipping requires

- **One architecture path.** The hybrid ECS/Wolverine split must not leave two command paths, two read paths or two auth paths.
- **Docs that match the code.** A status page that says intents are not drained when they are is a trap for the next engineer.
- **A real preview.** A seeded deployment with the browser client, auth, database and live Playwright suite.
- **Measured balance.** The 1k–2k economy must be reported, not assumed.
- **Measured performance.** 2,000 sims and 32 players must fit the tick and frame budgets.
- **A migration and rollback plan.** Schema 11 → 15 must be rehearsed on a copy of a real save.
- **Operable social and admin surfaces.** Reports, moderation, family, player and consumption pages must work after deploy.
- **A signed checklist.** Every claim points to evidence.

## What shipping does not require

- Every deferred feature from V1–V4.
- A perfect balance score on every metric.
- A Godot client.
- A full social platform.
- Zero warnings when the warnings are documented and non-blocking.

## Release philosophy

- Prefer a smaller, honest release over a broad, unverified one.
- Record known gaps instead of hiding them.
- Do not flip a default in the same commit that first measures it.
- Do not call a feature done because the happy path works.
- Keep the previous build and the pre-migration snapshot until the new one is proven.

## Success signal

A new player can open the preview, create a life, meet another player, survive a restart and report a problem, and an operator can inspect the world and roll back if needed.
