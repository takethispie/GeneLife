# H03 — Create shared message types

**Phase:** 1 — Foundation
**Depends:** H01
**Estimate:** 1 session

## Goal

Define all command, event, and query types used by the hybrid architecture as POCOs in `Genelife.Messages`. These are the contract between the API layer, the engine, and projections. No behavior — just types with properties.

## Tasks

- [ ] Define `Commands/` types:
  - `CreateSim(string Name, int Age, string? PlayerId)`
  - `DeleteSim(ulong NetworkId)`
  - `SetSimAction(ulong NetworkId, string ActionType)`
  - `CreateCompany(string Name)`
  - `DeleteCompany(ulong NetworkId)`
  - `CreateJobPosting(ulong CompanyId, string Position, float Salary, List<string> Requirements)`
- [ ] Define `Events/` types:
  - `SimCreated(ulong NetworkId, string Name, int Age)`
  - `SimDeleted(ulong NetworkId)`
  - `SimActionStarted(ulong NetworkId, string ActionType, float Duration)`
  - `SimActionCompleted(ulong NetworkId, string ActionType)`
  - `SimNeedsChanged(ulong NetworkId, float Hunger, float Energy, float Hygiene, float Bladder)`
  - `CompanyCreated(ulong NetworkId, string Name)`
  - `CompanyDeleted(ulong NetworkId)`
  - `EmployeeHired(ulong CompanyId, ulong SimId, string Position, float Salary)`
  - `EmployeeFired(ulong CompanyId, ulong SimId)`
  - `TickCompleted(long Tick, int EntityCount, double DurationMs)`
- [ ] Define `Queries/` types:
  - `GetAllSims` (query, returns `List<SimStatus>`)
  - `GetSim(ulong NetworkId)` (query, returns `SimStatus?`)
  - `GetAllCompanies` (query, returns `List<CompanyStatus>`)

## Conventions

- All types are `record` (immutable)
- Commands are past-tense imperative: `CreateSim`, `SetSimAction`
- Events are past-tense declarative: `SimCreated`, `SimActionStarted`
- All references use `ulong NetworkId`, never raw `Entity` from Arch
- No Arch.Core dependency in `Genelife.Messages`

## Files

- `Genelife.Messages/Commands/CreateSim.cs`
- `Genelife.Messages/Commands/DeleteSim.cs`
- `Genelife.Messages/Commands/SetSimAction.cs`
- `Genelife.Messages/Commands/CreateCompany.cs`
- `Genelife.Messages/Commands/DeleteCompany.cs`
- `Genelife.Messages/Commands/CreateJobPosting.cs`
- `Genelife.Messages/Events/SimCreated.cs`
- `Genelife.Messages/Events/SimDeleted.cs`
- `Genelife.Messages/Events/SimActionStarted.cs`
- `Genelife.Messages/Events/SimActionCompleted.cs`
- `Genelife.Messages/Events/SimNeedsChanged.cs`
- `Genelife.Messages/Events/CompanyCreated.cs`
- `Genelife.Messages/Events/CompanyDeleted.cs`
- `Genelife.Messages/Events/EmployeeHired.cs`
- `Genelife.Messages/Events/EmployeeFired.cs`
- `Genelife.Messages/Events/TickCompleted.cs`
- `Genelife.Messages/Queries/GetAllSims.cs`
- `Genelife.Messages/Queries/GetSim.cs`
- `Genelife.Messages/Queries/GetAllCompanies.cs`

## Acceptance

- `dotnet build Genelife.Messages` succeeds
- All types are immutable records
- No Arch.Core or ASP.NET references in the project