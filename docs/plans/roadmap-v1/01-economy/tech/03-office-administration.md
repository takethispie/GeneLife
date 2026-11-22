# E03 — Office administration

**Phase:** 2 — Labour market
**Depends:** E01, E02
**Estimate:** 2 sessions
**Status:** Implemented (V1)

## Goal

Make offices first-class, administrable resources: create, inspect, update capacity and close. Offices become the physical limit on employment and a recurring cost for the company.

## Context

`Office` exists as a component (`Genelife/Components/Company/Office.cs`) with `OfficeName`, `Company` entity and `Capacity`. Nothing creates offices outside tests, and `CompanyRepository.Add` creates only `CompanyName` (`Genelife.API/Repositories/CompanyRepository.cs:25`). `WorldDataStatus` reads offices opportunistically in `SimulationEngine.GetWorldData` (`SimulationEngine.cs:312`).

## Design

Extend the component:

```csharp
public record struct Office
{
    public string OfficeName;
    public Entity Company;
    public int Capacity;
    public Money OperatingCostPerCycle;
    public int SeatsUsed;
    public bool IsClosed;
}
```

Add `OfficeRepository` mirroring `CompanyRepository`:

- `NetworkId Add(NetworkId companyId, string name, int capacity, Money operatingCost)`
- validates the company exists, capacity > 0, name not empty;
- assigns `NetworkId` through `NetworkIdRegistry`;
- emits `OfficeCreated`.

Rules:

- Seats used = open seats + employees assigned to the office. A posting cannot reserve more seats than remain.
- A company must always have at least one open office to post jobs.
- Closing an office requires zero employees and zero open seats; otherwise the command is rejected with a conflict.
- Operating cost is charged by `CompanySolvencySystem` later; this step only stores it.

## Tasks

- [ ] Extend `Office` and add `OfficeStatus` fields (operating cost, seats used, closed).
- [ ] Add `CreateOffice`, `UpdateOffice`, `CloseOffice` messages with typed fields.
- [ ] Implement `OfficeRepository` with validation and `NetworkId` allocation.
- [ ] Add Wolverine endpoints `POST/PATCH/DELETE /api/offices` (admin policy).
- [ ] Add `OfficeCreated`, `OfficeUpdated`, `OfficeClosed` events and `OfficeDocument` projection.
- [ ] Enqueue mutations through `SimulationEngine.EnqueueCommand`; never mutate from the HTTP thread.
- [ ] Extend `CompanyRepository.Add` with an optional headquarters office so new companies are immediately usable.
- [ ] Add `SeatUtilization` to `OfficeStatus` and the admin read model.

## Files

- `Genelife/Components/Company/Office.cs`
- new `Genelife.API/Repositories/OfficeRepository.cs`
- new `Genelife.Messages/Commands/{CreateOffice,UpdateOffice,CloseOffice}.cs`
- new `Genelife.Messages/Events/{OfficeCreated,OfficeUpdated,OfficeClosed}.cs`
- new `Genelife.API/Models/OfficeDocument.cs`
- new `Genelife.API/Projections/OfficeProjection.cs`
- new `Genelife.API/Endpoints/Wolverine/OfficeEndpoints.cs`
- `Genelife.API/Services/SimulationEngine.cs`, `Genelife.API/Services/EnqueuedCommand.cs`

## Contracts

| Method | Route | Body | Result |
|---|---|---|---|
| POST | `/api/offices` | companyId, name, capacity, operatingCost | 202 + officeId |
| GET | `/api/offices` | — | `OfficeStatus[]` |
| PATCH | `/api/offices/{id}` | name?, capacity?, operatingCost? | 202 |
| DELETE | `/api/offices/{id}` | — | 202 or 409 if occupied |

## Acceptance

- Admin can create an office for an existing company and see it in `/api/data`.
- Capacity, seats used and utilisation are correct after hiring.
- Closing an occupied office is rejected.
- New companies can opt into an HQ office and immediately accept a posting.
- All mutations are applied at the next tick, not inside the HTTP request.

## Testing

- Repository validation tests (unknown company, zero capacity, duplicate name).
- Endpoint integration test with admin token.
- Close-occupied rejection test.
- Projection test for created/updated/closed.
