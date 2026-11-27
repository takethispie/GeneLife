# Contributing to GeneLife

## Git workflow — pull requests required

`master` is protected: **direct pushes are rejected**. All work lands via a pull request.

### The loop

```bash
# 1. Branch off the latest master
git fetch origin
git checkout -b <type>/<handle>/<slug> origin/master     # e.g. fix/ricardo/admin-a11y-form-labels

# 2. Work, commit (keep the branch focused on one task)

# 3. Push the branch
git push -u origin <type>/<handle>/<slug>

# 4. Open the pull request (fills in from the template)
gh pr create --fill

# 5. @tech-leader merges — see below
```

`type` ∈ `feat` | `fix` | `test` | `docs` | `chore` | `perf` | `refactor`.

### Who merges

**Only @tech-leader merges into `master`:**

```bash
gh pr merge <number> --squash --delete-branch
```

Agents never merge their own PR. After a merge, refresh your worktree with
`git fetch origin && git rebase origin/master` (or re-create it) before starting the
next branch — `master` moves often.

### Rules

- Never push to `master` directly; the branch protection ruleset rejects it.
- Never force-push a branch someone else may be reviewing.
- Keep the branch green: the `.github/workflows/ci.yml` checks must pass before merge.
- One task per branch; if you find an unrelated defect, take it back to the board.

## CI

Two workflows run under `.github/workflows/`:

- `ci.yml` — domain/web/API builds and test projects.
- `performance.yml` — performance suite.

Test projects use **Microsoft.Testing.Platform** (xunit v3), so run them with
`dotnet run --project <test>.csproj`, **not** `dotnet test` — on the .NET 10 SDK the
VSTest path is rejected.

## Metrics and reports — name the harness and the aggregation

Every metric quoted in a document, a report, an assertion message or a commit must name the
**harness** that produced it and the **aggregation** applied. A bare number is not a
measurement: the same quantity has different values under different harnesses, so a number
without its harness cannot be reproduced and silently rots.

- **Harness** — the exact test or scenario that produced it (e.g.
  `BalanceScenarioTests.Ten_year_balance_report`).
- **Aggregation** — the filter and the instant (e.g. "`Warn` + `Fail` checks, final year" vs
  "`Warn`-only checks, single instant").

Worked example: the "health warnings" figure was quoted as **61** while the balance gate
reports **3**. They are not the same measurement — `FamilyReleaseScenarioTests` counts
`Warn`-only at one instant, `BalanceScenarioTests` counts `Warn` + `Fail` at the final year —
and the 61 does not reproduce at all today (the family harness reports **18**). The fix is not
to pick one number but to name both: harness + aggregation. `DocsConsistencyTests` guards the
band table in `docs/plans/roadmap-v7/STATUS.md` against a row that names no source.

## Review and enforcement — current model ("Model A")

The team currently runs under a **single GitHub identity** (`@takethispie`), which every
agent commits as. GitHub therefore **cannot tell agents apart**, and it **blocks approving
your own pull request**. Two consequences:

- Review separation ("only the tech-lead merges") is enforced by **convention** — this file
  and `AGENTS.md` — not by GitHub.
- "Require approvals" must stay **off** on the `master` ruleset: with one account it would
  deadlock every merge. The ruleset enforces process instead: *pull request required*,
  *status checks required*, *no force-push*, *no deletion*.

Moving to per-agent (or a shared bot) account for agents plus a separate tech-lead account
("Model B") is what would turn the review step into a real technical control.
