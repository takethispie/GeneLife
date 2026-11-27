# Kanban with an MCP surface, out of git — options and recommendation

**Author:** @roberto (Solution Researcher) · **Date:** 2026-11-27 · **Branch:** `research/roberto/kanban-mcp-options`
**Status:** research + recommendation. **Nothing deployed.** Every MCP claim below is sourced; a REST API that
somebody wrapped is labelled as that.

## Recommendation (short)

**Primary: Vikunja** — it is the only candidate that ships an **MCP server inside the product itself**
(`/api/v2/mcp`, from v2.7.0), so there is no third-party wrapper in the write path, and it has first-class kanban
buckets, labels, comments and per-token permission scopes. AGPL-3.0, Docker, Postgres/SQLite.

**Runner-up: Planka + `sierikov/planka-mcp`** — the best *kanban-shaped* MCP surface (41 tools, per-user API keys,
`move_card`, `create_label`, and `set_custom_fields` which models the "selected next" marker exactly), **but** the
MCP server is a community project and Planka's Community edition is **source-available under a Fair Use licence, not
OSI open source** — a governance call for you, not a technical one.

**Third: Kanboard + `ErnestoCorona/kanboard-mcp`** — the only pair where **both** halves are MIT, with 39 typed tools
covering columns, swimlanes and `move_task_position`; transport is **stdio only today** (HTTP is on that project's
own v0.4 roadmap), so each agent runs its own stdio server against the shared instance.

**Fourth: Plane** — the most capable MCP surface (official server, MIT, 33 tools / 233 actions) but its self-hosted
**Community Edition cannot use the OAuth transport** — Plane's own docs say CE has no OAuth app registration, so you
run stdio servers with personal access tokens — and the stack is by far the heaviest.

**Excluded, with evidence: Focalboard.** The standalone repo has moved to the `mattermost-community` org and its
README says so in its own words: *"This repository is currently not maintained. If you're interested in becoming a
maintainer please let us know here."* Last release **v8.0.0, 2024-06-13** (commits continue, but nothing shipped in
~2 years), and the community MCP servers for it are flagged inactive/unvalidated. Not a base to build the team board on.

---

## 1. Ranked shortlist

| # | Board | Board licence | MCP server | MCP licence | Transport / auth | Card→column move | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | **Vikunja** | AGPL-3.0 | **in-product** (`/api/v2/mcp`, v2.7.0+) | same as board (AGPL-3.0) | Streamable HTTP + `Authorization: Bearer <token>` | yes — `task-bucket-update` action | **recommend** |
| 2 | **Planka** | Fair Use (source-available) | `sierikov/planka-mcp` (community) | MIT | stdio or HTTP; per-user Planka API key | yes — `move_card` | strong, with licence caveat |
| 3 | **Kanboard** | MIT | `ErnestoCorona/kanboard-mcp` (community) | MIT | stdio only (HTTP on v0.4 roadmap) | yes — `update_task` / `move_task_position` | solid, all-MIT |
| 4 | **Plane** | AGPL-3.0 (CE) | **official** `makeplane/plane-mcp-server` | MIT | stdio (CE) with PAT; HTTP+OAuth needs Commercial/Cloud | yes — `workitem(action="update", …)` | capable, heaviest, CE auth caveat |
| — | Wekan | MIT | community only (`namar0x0309/wekan-mcp`, `omriza5`) | unverified | stdio | `moveCard` exists | fallback |
| — | Focalboard | — | community only | unverified | — | — | **excluded: upstream unmaintained** |

## 2. Per-candidate detail

### 2.1 Vikunja *(recommended)*

- **Deploy.** Official compose (`vikunja/vikunja` + postgres); port **3456**; needs a `files` dir chowned to uid
  1000, `VIKUNJA_SERVICE_SECRET`, `VIKUNJA_SERVICE_PUBLICURL`, `VIKUNJA_DATABASE_*`. Postgres, MySQL or SQLite.

  ```yaml
  services:
    vikunja:
      image: vikunja/vikunja
      environment:
        VIKUNJA_SERVICE_PUBLICURL: http://localhost:3456
        VIKUNJA_DATABASE_HOST: db
        VIKUNJA_DATABASE_TYPE: postgres
        VIKUNJA_DATABASE_USER: vikunja
        VIKUNJA_DATABASE_PASSWORD: changeme
        VIKUNJA_DATABASE_DATABASE: vikunja
        VIKUNJA_SERVICE_SECRET: <random secret>
      ports: ["3456:3456"]
      volumes: ["./files:/app/vikunja/files"]
      depends_on: [db]
    db:
      image: postgres:18-alpine
      environment: { POSTGRES_USER: vikunja, POSTGRES_PASSWORD: changeme, POSTGRES_DB: vikunja }
      volumes: ["./db:/var/lib/postgresql/data"]
  ```

- **MCP surface (first-party, in-process).** Endpoint `https://<host>/api/v2/mcp`, Streamable HTTP with a bearer
  token, **no separate MCP server to run**. Deliberately narrow tool surface: **two tools**, `find_action` and
  `do_action`, over a large **action catalogue** — `find_action` lists actions (optionally by prefix, e.g.
  `project_views`, `task_labels`), `find_action(action="…")` returns that action's full input schema, and
  `do_action` executes it. Actions relevant to our board, read from the source (`pkg/modules/mcp/exposure.go`):
  `buckets-list|create|update|delete`, **`task-bucket-update`** (move a card between columns), `labels-*`,
  `task-labels-*`, `task-comments-*`, `task-assignees-*`, `project-view-buckets-tasks-list`.
- **Auth model.** Per-user **API token with MCP scopes**, created in *Settings → MCP* with presets — *Read only*,
  *Typed tools read + write* (default: tasks, projects, labels, assignees, comments, user search), *Full access* —
  plus an expiry date, revocable by deleting the token. "The server lists only the tools your token authorizes."
  Good fit for multi-writer: **one token per agent**, so writes are attributable and revocable individually.
- **Persistence.** Postgres / MySQL / MariaDB / SQLite + a files volume.
- **Licence.** AGPL-3.0 (board and its MCP module).

### 2.2 Planka + `sierikov/planka-mcp`

- **Deploy.** `ghcr.io/plankanban/planka:v2.2.x` + postgres; port **1337**; `BASE_URL`, `SECRET_KEY`
  (`openssl rand -hex 32`), `DATABASE_URL`, `DEFAULT_ADMIN_*`. Requires **Planka v2.0+** — v1 has no per-user API
  keys, which is exactly what the MCP relies on.
- **MCP surface (community, unofficial).** **41 tools**, marked read / write / destructive; the ones we need are
  all present: `get_board` (renders the board as lists → cards), `create_card`, **`move_card`** (another list, board,
  or `list=archive`), `update_card`, `add_comment`, `create_label` / `update_label`, `set_custom_fields`,
  `add_tasks`. Registered in the MCP Registry as `io.github.sierikov/planka-mcp`.
- **Auth model — the best thought-through of the set.** Each request runs as the owner of the API key that carried
  it ("no shared service account"); in **HTTP mode the server refuses to start if a shared `PLANKA_API_KEY` is set**,
  so an exposed port can never act as one identity. Clients send their own `Authorization: Bearer <key>`. The README
  is also explicit that card text is untrusted input and that delete tools should stay behind client confirmation.
- **Persistence.** Postgres + a data volume.
- **Licence caveat.** The MCP wrapper is **MIT**, but Planka Community is **"fair-code distributed under the Fair Use
  License"** (GitHub reports the repo licence as `NOASSERTION`; several third-party guides still say AGPL-3.0 and are
  wrong). Personal and in-organisation use is permitted; reselling it as a hosted service is not. **This is a
  decision you own** — it is source-available, not OSI open source.

### 2.3 Kanboard + `ErnestoCorona/kanboard-mcp`

- **Deploy.** `kanboard/kanboard:v1.2.x` (pin a version — the docs recommend it), volumes
  `/var/www/app/data`, `/var/www/app/plugins`; SQLite by default or Postgres/MariaDB; healthcheck at
  `/healthcheck.php` since 1.2.46. Careful with the docs' own `-p 8080:80` example: **8080 is already taken on this
  machine by the Memanto on-prem server** (see §4).
- **MCP surface (community).** **39 typed tools in 9 groups**: columns (`list_columns`, `create_column`,
  `update_column`, `move_column`, `delete_column`), swimlanes (5), tasks (`list_tasks`, `get_task`, `create_task`,
  `update_task` — including a column move, `close_task`, `reopen_task`, **`move_task_position`**, `list_my_tasks`),
  comments, attachments, members, categories. JSON-RPC 2.0 batching (up to 100 tasks per round-trip).
- **Auth model.** Dual: a **personal token** (acts as you) or an **application token** using Kanboard's
  protocol-level `jsonrpc` user — the latter is a genuine service identity for CI/shared agents. Auth failure at
  startup exits rather than falling back to a default identity.
- **Transport limitation, stated plainly.** **stdio only today**; HTTP/SSE and per-user header auth are on that
  project's v0.4 roadmap. That is not a blocker — each agent can spawn its own stdio MCP server against the one
  shared Kanboard — but it means there is no single shared MCP endpoint.
- **Persistence.** SQLite file or Postgres/MariaDB in the data volume.
- **Licence.** Board **MIT**, wrapper **MIT** — the cleanest licence story of the four.

### 2.4 Plane + `makeplane/plane-mcp-server`

- **Deploy.** Plane Community Edition self-host (the official compose stack: web, api, worker, beat-worker, live,
  plus PostgreSQL, Redis, RabbitMQ and MinIO) — an order of magnitude heavier than a kanban needs. The MCP server
  itself is separate (`plane-mcp-server` via `uvx`, or the standalone compose/Helm options).
- **MCP surface (official).** **33 tools, one per resource, covering 233 actions**; the README is current and the
  docs page's "30 tools / 207 actions" is the older figure. Relevant actions: `workitem` → `list` · `search` ·
  `count` · `create` · `update` · `manage_assignee` · `manage_label`, `state` → `list` · `create` · `update` ·
  `delete`, `workitem_comment` → `create` · `update`, `workitem_property` → `set_value`, and `get_pql_reference` for
  Plane's query language (PQL), e.g. `workitem(action="list", pql='state__group = "started"')`. The three columns
  are `state` rows; moving a card is `workitem(action="update", …)`.
- **Auth model — the catch.** OAuth is available on Plane Cloud and Commercial Edition. Plane's own self-host page:
  *"Plane Community Edition does not include [OAuth app registration], so the OAuth transport cannot be used against
  a Community Edition instance. Community Edition users should run the server in local (stdio) mode with a personal
  access token instead."* So on CE: **stdio + one PAT per agent** (`PLANE_API_KEY`, `PLANE_WORKSPACE_SLUG`,
  `PLANE_BASE_URL`).
- **Persistence.** Postgres + MinIO (plus Redis/RabbitMQ) — the most moving parts by far.
- **Licence.** Plane CE AGPL-3.0; the MCP server MIT; OAuth-based features are a Commercial/Cloud capability.

## 3. Where no MCP integration exists (plainly)

- **Every tool on the shortlist has an MCP server, but only two are first-party.** Vikunja's lives inside the
  product; Plane's is published by Plane. **Planka's and Kanboard's are unaffiliated community projects** — MIT, but
  maintained by individuals, so pin image digests / npm versions and treat them as a supply-chain dependency.
- **Focalboard has no live upstream to build on** (repo gone from GitHub, last release June 2024) — its community
  MCP servers should not be adopted.
- **If we ever pick a board with no MCP server, the wrapper is small but real.** It would be a thin stdio/HTTP MCP
  server over that product's REST API exposing roughly **six tools** — `list_boards`, `get_board`, `list_cards`,
  `create_card`, `move_card`, `add_comment` — plus a credential per agent. That is exactly what the community
  servers above are; it is a few hundred lines, not a project, **but it is code we would own forever**, which is why
  I ranked the two first-party options first.
- **A wrapped REST API is not an MCP server.** Vikunja's and Plane's are MCP servers shipped by the vendor; the
  Planka and Kanboard ones are third-party MCP servers over REST APIs; a bare REST API with no MCP layer would need
  the wrapper described above.

## 4. Environment facts checked on this machine

- **Docker 29.9.3**, **Docker Compose v5.7.1** — available.
- Running containers: `moorcheh-onprem-server` (Memanto backend) on **0.0.0.0:8080**, `moorcheh-ollama` on
  **11434**, and a `postgres:18.6` on 127.0.0.1:56504.
- **Port 8080 is occupied** — do not reuse it for the board (Kanboard's own docs example uses `-p 8080:80`; that
  would collide with Memanto). Free ports that fit the defaults: Vikunja **3456**, Planka **1337**.
- The on-prem pattern already in use here (one Postgres container per app) matches how all four boards deploy.

## 5. Migration path from `docs/plans/BOARD.json`

> **STATUS - SUPERSEDED the same day (2026-11-27).** Vikunja was stood up as described below, then retired in favour of the built-in **Hermes kanban**, which was the better fit all along: it is profile-native (assignment is a profile name), has first-class dependencies, dispatches cards as worker processes in isolated worktrees, and needs no service, container or token. The live board is now `hermes kanban` (board slug `genelife`) - see `docs/plans/BOARD.md`. The Vikunja MCP was removed from every profile and the containers are down. Read the rest of this section as the record of an option that was tried, not as the current design.
> Vikunja was the pick and is running at `http://localhost:3456`. All 38 cards migrated by
> script and were verified by reading the board back (9 waiting / 4 running / 25 done) and by
> DB-level bucket counts - not just a write 200. Two deliberate deviations from the plan below:
> (1) **one shared MCP token** for all agents rather than a credential per agent, so attribution
> is weaker than step 2 recommends and is worth revisiting; (2) `BOARD.json` was **replaced
> outright** by `BOARD.md` rather than kept uncommitted (step 6), per the owner's call.

Today's semantics to preserve: three states, per-card `title` + `owner` + `note`, and a per-member `selected` "next"
marker. Mapping (example for the recommended pair; the others are the same shape with different tool names):

| BOARD.json | Vikunja | Planka (`planka-mcp`) |
|---|---|---|
| `states[0] waiting` | bucket **"Waiting to run"** in the kanban view | list **"Waiting to run"** |
| `states[1] running` | bucket **"Currently running"** | list **"Currently running"** |
| `states[2] done` | bucket **"Done"** | list **"Done"** |
| `cards[].title` | task title | card title |
| `cards[].owner` | label `owner:<handle>` (and/or assignee) | label `owner:<handle>` (and/or assignee) |
| `cards[].note` | task description / first comment | card description / comment |
| `cards[].selected` | label `next` + label `next:<handle>` | **custom field** `next_by = <handle>` (or the same labels) |

Steps, in order:

1. **Deploy the board beside the git board**, empty; do **not** touch `BOARD.json` yet. Keep it off git entirely —
   no compose file in the repo if that is the point, or in a separate `ops/` repo if you want it versioned.
2. **Create one credential per agent** (Vikunja MCP token, or Planka API key) plus the user account it belongs to,
   so every write is attributable and individually revocable. Never hand an agent a shared service account.
3. **Seed through the MCP surface, not the vendor REST API**, by driving it with a script that reads
   `BOARD.json`: create the three columns, then create each card in its state with owner label + note. Doing the
   seed through MCP is the cheapest end-to-end proof that create/move/query actually work for us **before** git is
   retired.
4. **Verify the seed by reading back** — list the board per state and diff the card set and owner labels against
   `BOARD.json`. Do not cut over on a successful write.
5. **Dual-run for one working day**: agents write to the board, the board is exported back to `BOARD.json` by the
   same script in reverse so the git file stays truthful while trust builds. Any card that goes missing or lands in
   the wrong column is a bug in the mapping, not in the board.
6. **Cut over**: add a header to `BOARD.json` recording it as retired, with the board URL, the credential model and
   the date; keep the file for history but stop committing it. Update the team memo/board-process skill to point at
   the endpoint, and record the decision (and the licence caveat, if Planka) in Memanto.
7. **Keep an export on a schedule.** The board becomes the source of truth, so a nightly JSON export (the script
   already has the mapping) is the backup — this is the risk we actually take on by leaving git.

Two things to decide before step 1, both yours: **the licence call on Planka** (source-available vs Vikunja's
AGPL-3.0) and **whether the board must expose a single shared HTTP endpoint** (Vikunja and Planka-HTTP give that;
Kanboard and Plane-CE are per-agent stdio).

## Sources

**Board products** — [go-vikunja/vikunja](https://github.com/go-vikunja/vikunja) (AGPL-3.0, licences read from the
GitHub API); [Vikunja full docker example](https://vikunja.io/docs/full-docker-example/);
[plankanban/planka](https://github.com/plankanban/planka) (licence `NOASSERTION`; README states fair-code under the
Fair Use License); [Planka self-host compose](https://selfhosting.sh/apps/planka/);
[kanboard/kanboard](https://github.com/kanboard/kanboard) (MIT); [Kanboard Docker image usage](https://docs.kanboard.org/v1/admin/docker/);
[makeplane/plane](https://github.com/makeplane/plane) (AGPL-3.0).

**MCP servers** — [Vikunja MCP help page](https://vikunja.io/help/mcp/) (endpoint `/api/v2/mcp`, v2.7.0+, token
presets); Vikunja source `pkg/modules/mcp/exposure.go` (action catalogue incl. `task-bucket-update`, `buckets-*`) and
`pkg/modules/mcp/catalog.go` (`find_action`, `do_action`);
[makeplane/plane-mcp-server](https://github.com/makeplane/plane-mcp-server) (MIT, 33 tools/233 actions) and its
[`plane_mcp/tools/README.md`](https://github.com/makeplane/plane-mcp-server/blob/main/plane_mcp/tools/README.md)
(action tables: `workitem`, `state`, `workitem_comment`, `workitem_property`, PQL);
[Plane: self-host the MCP server](https://developers.plane.so/dev-tools/mcp-server-self-host) (CE has no OAuth app
registration → stdio + PAT); [Plane MCP server docs](https://docs.plane.so/ai/mcp-server);
[sierikov/planka-mcp](https://github.com/sierikov/planka-mcp) (MIT, 41 tools, per-user keys, `move_card`,
`set_custom_fields`); [ErnestoCorona/kanboard-mcp](https://github.com/ErnestoCorona/kanboard-mcp) (MIT, 39 tools,
stdio, app/personal token); also seen but not selected: `namar0x0309/wekan-mcp`, `omriza5/wekan-mcp-server`,
`bivex/kanboard-mcp`, `0xK3vin/vikunja-mcp`, `democratize-technology/vikunja-mcp`.

**Exclusions** — Focalboard: repo now `mattermost-community/focalboard` (GitHub returns a 301 from the old path);
its [README](https://github.com/mattermost-community/focalboard) states *"This repository is currently not
maintained"* and asks for maintainers; latest release **v8.0.0** published 2024-06-13 (GitHub releases API). Its MCP
servers are community projects whose directory listings report inactive/unvalidated.
