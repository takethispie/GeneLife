# F09 — Reproduction admin API and UI

**Phase:** 9 — Admin
**Depends:** F01–F08
**Estimate:** 1–2 sessions
**Status:** Partial — pregnancy/child/guardian read endpoints done; mutations and Razor page pending

## Goal

Let an operator inspect pregnancies, births, children, guardians and child costs, and perform controlled interventions.

## Context

The family page starts with relationships in R09. Reproduction adds tabs and endpoints; it must not create a second admin surface.

## Design

### DTOs

Add to the family DTOs:

```csharp
public sealed class PregnancyStatus
{
    public ulong NetworkId { get; set; }
    public ulong CarryingParentNetworkId { get; set; }
    public ulong OtherParentNetworkId { get; set; }
    public string CarryingParentName { get; set; } = string.Empty;
    public long ConceivedTick { get; set; }
    public long DueTick { get; set; }
    public int MonthsRemaining { get; set; }
    public int RiskBasisPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Complications { get; set; }
    public decimal CarePaid { get; set; }
}

public sealed class ChildStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public ulong? ParentANetworkId { get; set; }
    public ulong? ParentBNetworkId { get; set; }
    public ulong? GuardianNetworkId { get; set; }
    public ulong HouseholdNetworkId { get; set; }
    public string Household { get; set; } = string.Empty;
    public bool Enrolled { get; set; }
    public string School { get; set; } = string.Empty;
    public bool Overcrowded { get; set; }
    public decimal ChildCostsThisYear { get; set; }
}
```

### Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/family/pregnancies` | active and historical pregnancies |
| GET | `/api/family/pregnancies/{id}` | one pregnancy with risk and care |
| GET | `/api/family/children` | filterable child list |
| GET | `/api/family/children/{id}` | one child with parents, guardian, household and school |
| GET | `/api/family/guardianships` | guardian/ward pairs |
| POST | `/api/family/pregnancies` | admin force pregnancy (testing/operations) |
| DELETE | `/api/family/pregnancies/{id}` | admin cancel |
| POST | `/api/family/children/{id}/guardian` | assign a guardian |

All mutations require the admin policy and enqueue commands. Forced pregnancy is an explicit admin operation, not a natural-loop shortcut.

### UI

Extend `Family.razor` with:

- **Pregnancies** — due date, risk, parents, care paid, status.
- **Children** — parent/guardian, household, school, overcrowding, costs.
- **Operations** — force/cancel pregnancy, assign guardian, with confirmation.

The page shows the difference between natural and admin-sourced pregnancies and guardianships.

### Authorization and safety

- No player route for pregnancy, children or guardians.
- Admin actions are audited through the event stream.
- A forced pregnancy still respects one-active-pregnancy and age rules unless an explicit override is documented.

## Tasks

- [ ] Add pregnancy/child/guardianship DTOs.
- [ ] Add the engine builders.
- [ ] Add the endpoints and admin commands.
- [ ] Extend `Family.razor` with the new tabs.
- [ ] Add authorization and audit tests.
- [ ] Add page smoke tests for seeded, pregnant and migrated worlds.

## Files

- `Genelife.Contracts/Dtos/FamilyStatus.cs`
- `Genelife.API/Endpoints/Wolverine/FamilyEndpoints.cs`
- `Genelife.API/Commands/AdminCommand.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.Web/Pages/Family.razor`
- `Genelife.UnitTests/Endpoints/FamilyEndpointTests.cs`

## Acceptance

- The page lists pregnancies and children with parents, guardian, household and school.
- Risk and care are visible for an active pregnancy.
- Force/cancel/assign guardian return `202` and apply on the next tick.
- Non-admin mutations are rejected.
- Each admin action is visible in the event stream.

## Testing

- Endpoint tests for each route.
- Authorization tests for mutations.
- Page smoke test with a seeded pregnant household.
- Audit assertion for a forced pregnancy and a guardian assignment.