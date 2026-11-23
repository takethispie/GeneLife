# R09 — Relationship admin API and UI

**Phase:** 9 — Admin
**Depends:** R01–R08
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Make the family graph inspectable and operable without exposing direct world access to the browser.

## Context

The admin app is a Blazor WebAssembly read-mostly console. Existing pages poll DTOs and submit typed commands that return `202`. The family page follows the same pattern.

## Design

### DTOs

Add to `Genelife.Contracts/Dtos/FamilyStatus.cs`:

```csharp
public sealed class FamilyStatus
{
    public int LivingSims { get; set; }
    public int EligibleSingles { get; set; }
    public int PartneredSims { get; set; }
    public int Parents { get; set; }
    public int Children { get; set; }
    public decimal PartnershipRate { get; set; }
    public decimal AverageAgeGap { get; set; }
    public List<FamilyHealthCheckStatus> Health { get; set; } = [];
    public TraitDistributionStatus Traits { get; set; } = new();
}

public sealed class RelationshipStatus
{
    public ulong NetworkId { get; set; }
    public ulong FromNetworkId { get; set; }
    public ulong ToNetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public long SinceTick { get; set; }
}

public sealed class AttractionExplanation
{
    public ulong SimNetworkIdA { get; set; }
    public ulong SimNetworkIdB { get; set; }
    public int Score { get; set; }
    public bool Eligible { get; set; }
    public string? RejectionReason { get; set; }
    public List<AttractionComponentStatus> Components { get; set; } = [];
}
```

### Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/family` | summary, trait distribution and health |
| GET | `/api/family/sims` | filterable sim list with traits and family ids |
| GET | `/api/family/sims/{simId}` | one sim with parents, children, partner, guardian |
| GET | `/api/family/relationships` | edge list by sim, kind or source |
| GET | `/api/family/attraction/{a}/{b}` | score breakdown for a pair |
| GET | `/api/family/health` | invariants and projection staleness |
| POST | `/api/family/partnerships` | admin force-pair two eligible sims |
| DELETE | `/api/family/partnerships/{a}/{b}` | admin end a partnership |
| POST | `/api/family/guardians` | admin assign a guardian (feature 04) |

All mutations require the admin policy and enqueue commands; no HTTP-thread world mutation.

### UI

Add `Genelife.Web/Pages/Family.razor`:

- **Overview** — counts, partnership rate, trait distribution, health checks.
- **Sims** — filter by stage, age, household, partnered, has children; select a sim.
- **Relationships** — parents, children, partner, guardian, source and since tick.
- **Attraction** — choose two sims and show the component breakdown and eligibility reason.
- **Operations** — force/end partnership with confirmation and command result.

Use the existing `EmptyState`, polling interval and error pattern. Do not expose trait sorting as the default.

### Authorization

- Reads require an authenticated admin or the existing read policy.
- Mutations require `AuthPolicies.Admin`.
- No player endpoint for partner search or trait ranking.

## Tasks

- [ ] Add the family DTOs to `Genelife.Contracts`.
- [ ] Add `BuildFamilyStatus`, `BuildFamilySims`, `BuildRelationships` and `BuildAttractionExplanation` to the engine.
- [ ] Add the family endpoints.
- [ ] Add force/end partnership commands and handlers.
- [ ] Add `Family.razor` and navigation.
- [ ] Add endpoint authorization and page smoke tests.
- [ ] Add a "why this pair" explanation panel.

## Files

- `Genelife.Contracts/Dtos/FamilyStatus.cs`
- `Genelife.API/Endpoints/Wolverine/FamilyEndpoints.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Commands/AdminCommand.cs`
- `Genelife.Web/Pages/Family.razor`
- `Genelife.Web/Layout/NavMenu.razor`
- `Genelife.UnitTests/Endpoints/FamilyEndpointTests.cs`

## Acceptance

- The family page loads with empty, seeded and migrated worlds.
- A sim view shows parents, children, partner and guardian.
- An attraction explanation lists every component and the eligibility reason.
- Force/end partnership returns `202` and is visible after the next tick.
- Non-admin mutations are rejected.
- The page never calls the world directly.

## Testing

- Endpoint tests for status, list, detail, relationships and attraction.
- Authorization tests for admin mutations.
- A page smoke test with a seeded fixture.
- Assert no player route exposes trait ranking or partner search.