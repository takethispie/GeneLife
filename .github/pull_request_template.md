## What

<!-- One or two sentences: what does this change? -->

## Why

<!-- The task / board card (docs/plans/BOARD.json) or bug this closes. -->

## Verification

<!-- The exact commands you ran and what they returned. Not "tested locally". -->
- `dotnet build GeneLife.sln -c Release` → ___ errors
- Tests: ___ (note: run test projects with `dotnet run --project <csproj>`, not `dotnet test`)
- Other evidence (browser check, screenshot path, perf numbers):

## Checklist

- [ ] Branched off latest `master` — did **not** commit directly to `master`
- [ ] Branch name follows `<type>/<handle>/<slug>`
- [ ] Rebased on current `master` before opening this PR
- [ ] CI (`.github/workflows/ci.yml`) green
- [ ] Board card / task referenced in "Why"
- [ ] Merged only by @tech-leader
