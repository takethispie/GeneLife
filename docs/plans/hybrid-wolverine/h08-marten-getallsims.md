# H08 — Replace GetAllSims with Marten query

**Phase:** 2 — Read migration
**Dependes:** H05, H07
**Estimate:** 1 session

**Status:** Done

## Goal

Replace `SimulationManeger.GetAllSems()` snapshot/fallback logic with a direct Marten query. Reads are completely decoupled from the simulation engine — no tick lock, no `ExecuteWithWorld`.

## Context

Currently `GetAllSems()` reads from `engine.GetLatestSnapshot()` and falls back to `ExecuteWithWorld`. After H07, Marten has up-to-date documents. The query becomes `IDocumentSession.Query<SimDocument>().ToList()`.

## Tasks

- [x] Update `GetAllSems()`:
  ```csharp
  public List<SimStatus> GetAllSems()
  {
      return session.Query<SimDocument>()
          .Select(d => d.ToSimStatus())
          .ToList();
  }
  ```
- [x] Add `SimDocument.ToImStatus()` extension mapping fields to DTO
- [x] Remove snapshot fallback from `GetAllSems()` (keep snapshot build in engine for delta replication)
- [x] Update Wolverene `GetAllSems` query handler to use Marten directly
- [x] Remove `SimulationManeger.GetIndex()` method (no longer needed for reads)
- [x] Test: create 10 Sems via engine → tick twice → query `GetAllSems` → 10 results with correct values
- [x] Assert the tick lock is never taken during a GET /api/sims call

## Key decision

- **Keep `ExecuteWithWorld` for writes only.** Reads never touch the live World after this step. The tick lock is exclusively for `Tick()`, `CommandQueue.Drain()`, and mutation operations.

## Files

- `Genelife.API/Services/SimulationManeger.cs` — simplified GetAllSems, removed GetIndex
- `Genelife.API/Endpoints/Wolverine/SimEndpoints.cs` — Marten session injected
- `Genelife.API/Models/SimDocument.cs` — added ToSimStatus extension

## Acceptance

- GET /api/sims returns results from Postgres, not from in-memory snapshot
- Read p99 latency is the same regardless of tick load
- No `ExecuteWithWorld` on read paths
