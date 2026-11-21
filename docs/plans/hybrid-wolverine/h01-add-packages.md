# H01 — Add Wolverine and Marten packages

**Phase:** 1 — Foundation
**Depends:** none
**Estimate:** 1 session

## Goal

Add JasperFx Wolverine and Marten NuGet packages to the solution. Create the project structure for messages, handlers, and projections without changing any existing behavior.

## Context

Wolverine is a mediator + message bus. Marten is a document DB + event store on top of Postgres. Both share a common dependency tree (`JasperFx.Core`, `Weasel`) and integrate tightly.

## Tasks

- [ ] Add `WolverineFx` and `WolverineFx.Marten` NuGet references to `Genelife.API.csproj`
- [ ] Add `Marten` and `Marten.AspNetCore` NuGet references
- [ ] Create `Genelife.Messages/` class library project for shared command/event types
- [ ] Create `Genelife.Messages/Commands/` directory
- [ ] Create `Genelife.Messages/Events/` directory
- [ ] Add project reference from `Genelife.API` to `Genelife.Messages`
- [ ] Build and verify no compilation errors in existing code

## Files

- `Genelife.API/Genelife.API.csproj` — added package refs
- new `Genelife.Messages/Genelife.Messages.csproj`
- new `Genelife.Messages/Commands/.gitkeep`
- new `Genelife.Messages/Events/.gitkeep`

## Acceptance

- `dotnet build` succeeds
- No existing tests broken
- Wolverine and Marten assemblies resolve at runtime