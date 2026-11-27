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
