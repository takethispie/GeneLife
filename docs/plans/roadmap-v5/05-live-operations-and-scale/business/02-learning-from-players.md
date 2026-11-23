# Learning from players

V5 is the first roadmap where the player is a full participant. That makes the live preview a learning tool, not just a deployment target.

## What to learn

- Where players get stuck in the job, housing, shopping or family loop.
- Whether appearance and family resemblance are readable in a crowd.
- Whether utilities and grocery feel like constraints or noise.
- Whether chat and presence make the town feel inhabited or crowded.
- Whether the 32-player world holds up under real behavior.
- Which admin pages operators actually use during an incident.

## How to learn it

- Instrument the player action funnel: attempted, rejected, applied, abandoned.
- Record rejection codes and the most common reasons.
- Measure time-to-first-job, time-to-first-home and time-to-first-partnership.
- Measure chat and report volume per session.
- Capture frame time and payload size on representative hardware.
- Run a short playtest with a seeded preview and a structured feedback form.
- Review reports and moderation actions for abuse patterns.

## What not to do

- Do not tune the economy from a single player anecdote.
- Do not add a feature because one player asked for it.
- Do not use telemetry to expose private family, wallet or chat content.
- Do not treat a successful demo as a substitute for a soak test.

## Feedback loop

1. Observe the funnel and the reports.
2. Reproduce the issue in a seeded world.
3. Add a regression test or scenario.
4. Fix the smallest cause.
5. Record the change in the release notes.
6. Re-run the affected balance and performance scenarios.

## Success signal

The team can explain the top three player drop-off points with evidence and has a regression test for each fix, without collecting personal data.
