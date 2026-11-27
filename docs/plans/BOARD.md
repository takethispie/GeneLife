# GeneLife team board — hermes-kanban

The team board is the **Hermes kanban** (`hermes kanban`): a durable, SQLite-backed
board shared across every profile, with real assignment, first-class dependencies,
and a dispatcher that runs cards as agent processes.

| | |
|---|---|
| **Board** | slug `genelife` — display name "GeneLife" |
| **Database** | `~/.hermes/kanban/boards/genelife/kanban.db` |
| **View it** | the **Kanban page in the Hermes desktop app**, or `hermes kanban list` |
| **Default workdir** | `G:\dev\GeneLife` — each card runs in its own worktree |

## What it changes

- **Assignment is a profile name** (`--assignee main-dev`), not a label or an
  `**Owner:**` line in the body.
- **Dependencies are first-class**: `hermes kanban link <parent> <child>`. A child
  stays in `todo` while its parents are open.
- **The dispatcher runs the cards.** A card in `ready` is spawned as a worker process
  for its assignee, in its own workspace; the worker reads the card, does the work and
  completes it. Cards in `blocked` never run.
- Statuses: `triage -> todo -> ready -> running -> review -> done`, plus `blocked` and `scheduled`.

## Driving it

| Want | Do |
|---|---|
| See the board | `hermes kanban list` (or `stats`, or the desktop Kanban page) |
| Read one card | `hermes kanban show <id>` |
| Add a card | `hermes kanban create "<title>" --assignee <profile> --body-file <file> --workspace worktree` |
| Assign / reassign | `hermes kanban assign <id> <profile>` |
| Park a card so it cannot run | create it with `--initial-status blocked` |
| Release it to the dispatcher | `hermes kanban unblock <id>` |
| Add a dependency | `hermes kanban link <parent> <child>` |
| Comment on a card | `hermes kanban comment <id> "..."` |
| Finish a card | `hermes kanban complete <id>` |

**On a dispatched card, agents use the `kanban_*` tools** (`kanban_show`,
`kanban_comment`, `kanban_complete`, `kanban_block`, `kanban_heartbeat`, ...) rather
than the CLI — the dispatcher injects them plus the worker lifecycle guidance.

**A card in `ready` costs a spawned agent.** Park work with `--initial-status blocked`
and release it deliberately; never leave a card `ready` unless it should run now.

## History

This replaces two earlier boards: the git file `docs/plans/BOARD.json` (retired
2026-11-27) and a self-hosted Vikunja instance stood up the same day, then retired in
favour of this one. Shipped history from both remains in git, and the Vikunja cards were
dumped before that teardown.
