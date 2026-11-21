# H06 — Marten document model for projections

**Phase:** 1 — Foundation
**Depends:** H01, H03
**Estimate:** 2 sessions

## Goal

Define Marten document types for Sim and Company read models. Configure Marten to auto-create tables. Set up inline (synchronous) projections so that when events are published, Marten documents update in the same transaction.

## Tasks

- [ ] Define `SimDocument`:
  - `Id` (Guid, Marten identity)
  - `NetworkId` (ulong, indexed)
  - `Name` (string)
  - `Age` (int)
  - `Hunger`, `Energy`, `Hygiene`, `Bladder` (float)
  - `CurrentAction` (string)
  - `ActionTimeRemaining` (float)
  - `IsPlayerControlled` (bool)
  - `PlayerId` (string?)
  - `CreatedAt`, `UpdatedAt` (DateTimeOffset)
- [ ] Define `CompanyDocument`:
  - `Id` (Guid)
  - `NetworkId` (ulong, indexed)
  - `Name` (string)
  - `EmployeeCount` (int)
  - `CreatedAt`, `UpdatedAt` (DateTimeOffset)
- [ ] Register documents in Marten configuration: `opts.Schema.For<SimDocument>()`
- [ ] Configure Marten to create/update tables on startup: `opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate`
- [ ] Create read-only test: `IDocumentSession.Query<SimDocument>()` returns empty list on fresh database

## Projections (H07 builds on this)

The projection classes are defined here but wired in H07:
- `SimProjection : Marten.Events.Projections.IProjection` or inline `IEvent<T>` handlers
- `CompanyProjection`

## Files

- new `Genelife.API/Models/SimDocument.cs`
- new `Genelife.API/Models/CompanyDocument.cs`
- new `Genelife.API/Projections/SimProjection.cs` (skeleton)
- new `Genelife.API/Projections/CompanyProjection.cs` (skeleton)
- `Genelife.API/Program.cs` — Marten schema registration

## Acceptance

- Postgres running; app starts, tables `mt_doc_simdocument` and `mt_doc_companydocument` created
- `INSERT` via `IDocumentSession.Store()` works
- `SELECT` via `IDocumentSession.Query()` returns stored documents