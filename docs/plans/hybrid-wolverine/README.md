# Hybrid ECS + Wolverine — Subtask Plan

**Status:** All steps done. One command path, one read path and one auth path are active.

20 implementation steps across 5 phases. See `docs/plans/hybrid-ecs-wolverine.md` for the architecture overview.

## Phase 1 — Foundation

| # | Task | Depends | Estimate | Status |
|---|------|---------|----------|--------|
| H01 | Add Wolverine + Marten packages | — | 1 session | Done |
| H02 | Register Wolverine in Program.cs | H01 | 1 session | Done |
| H03 | Create shared command/event types | H01 | 1 session | Done |
| H04 | First command flow: EatCommand end-to-end | H02, H03 | 2 sessions | Done |
| H05 | Wolverine HTTP endpoints | H04 | 1 session | Done |
| H06 | Marten document model | H01, H03 | 2 sessions | Done |

## Phase 2 — Read Migration

| # | Task | Depends | Estimate | Status |
|---|------|---------|----------|--------|
| H07 | Marten inline projections from events | H03, H04, H06 | 2 sessions | Done |
| H08 | Replace GetAllSims with Marten query | H05, H07 | 1 session | Done |
| H09 | Replace company reads with Marten query | H07, H08 | 1 session | Done |
| H10 | Remove WorldSnapshot build from tick loop | H08, H09 | 1 session | Done |

## Phase 3 — Persistence

| # | Task | Depends | Estimate | Status |
|---|------|---------|----------|--------|
| H11 | WorldRestoreService: hydrate ECS from Marten | H06, H07 | 2 sessions | Done |
| H12 | WorldSaveService: persist ECS to Marten | H07, H11 | 2 sessions | Done |
| H13 | Outbox pattern for reliable events | H07, H12 | 1 session | Done |
| H14 | CI persistence test: start → restart → verify | H11, H12, H13 | 1 session | Done |

## Phase 4 — Commands and Sagas

| # | Task | Depends | Estimate | Status |
|---|------|---------|----------|--------|
| H15 | Command enqueue pattern for all mutations | H04, H05 | 2 sessions | Done |
| H16 | HiringSaga: replace collect→decide→apply | H15 | 3 sessions | Done (collect→decide→apply retained; saga scaffolding removed) |
| H17 | Authorization middleware for Wolverine | H05, H15 | 2 sessions | Done |

## Phase 5 — Realtime and Cleanup

| # | Task | Depends | Estimate | Status |
|---|------|---------|----------|--------|
| H18 | SignalR integration via Wolverine side effects | H07, H17 | 2 sessions | Done |
| H19 | Migrate remaining ASP.NET endpoints to Wolverine | H05, H08, H09, H15 | 1 session | Done |
| H20 | Feature flags, migration checklist, parity verification | H19 | 1 session | Done (flags removed; parity suite added) |

## Total estimate: 26 sessions (~2–3 weeks with 2 sessions/day)
