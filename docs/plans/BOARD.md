# GeneLife team board — MOVED

This board no longer lives in the repo. It is served by a self-hosted **Vikunja**
instance so agents read and write it directly — no local-file edit, no push.

| | |
|---|---|
| **Board** | http://localhost:3456 — project `GeneLife Team Board` (id 2), kanban view **8** |
| **Columns** | Waiting to run (bucket 4) · Currently running (5) · Done (6) |
| **Access** | MCP server `vikunja` (12 tools) — registered in every GeneLife agent profile |
| **Service** | `C:\Users\dustcat\genelife-kanban` (`docker compose up -d`) |
| **Ops notes** | `C:\Users\dustcat\genelife-kanban\README.md` (API + MCP gotchas) |

## Card conventions (unchanged)

- The **owner** and the card's current state note live in the task **description**.
- A member's selected/active task carries the label **`▶ next`**.
- Reference cards **by title** — ids renumber when a board is regenerated.

## Agent usage

| Want | Do |
|---|---|
| Read the board | `tasks_list` with `filter: "project_id = 2"` (returns `{"items":[…]}`) |
| Add a card | `tasks_create` |
| Update owner / state / notes | `tasks_update` |
| Move between columns | `find_action` → `do_action` (task-bucket update; there is no dedicated move tool) |

Rich-text fields are HTML by default — pass `format: "markdown"`.

---

*Retired 2026-11-27: the git-file `BOARD.json` (38 cards) was migrated into
Vikunja. Git history retains the old file; this doc replaces it.*
