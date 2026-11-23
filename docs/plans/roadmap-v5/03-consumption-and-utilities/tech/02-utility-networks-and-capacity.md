# C02 — Utility networks and capacity

**Phase:** 2 — Infrastructure
**Depends:** C01, V2 placement, V1 housing bootstrap
**Estimate:** 2–3 sessions
**Status:** Planned (V5)

## Goal

Give water and electricity real production, capacity and load, so a growing town can strain or overload a service and the admin can see why.

## Context

Buildings are placed on the V2 map with a `Placement`. Housing bootstrap and construction create residential units. There is no infrastructure layer, no service connection and no capacity model.

V5a uses one authoritative network per service. It does not simulate a market, ownership or a full grid topology.

## Design

### Network creation

- The world creates one `Water` network and one `Electricity` network on bootstrap.
- Production and capacity come from `UtilityOptions` and scale with population/housing targets.
- Network entities have a `NetworkId` and are persisted.
- A map or housing development event can increase capacity through a configured expansion path.

### Service links

- A building gets a `BuildingServiceLink` for each service it needs.
- Residential, office, school and grocery buildings need water and electricity.
- A link has a demand value based on building kind, tier and occupant capacity.
- A missing link means the building is unserved and the household/service sees `CutOff`.

### Load and capacity

`UtilityConsumptionSystem` (order 470) runs monthly:

1. Sum linked demand and actual household usage.
2. Compare load to capacity.
3. Set `Normal`, `Strained`, `Overloaded` or `Offline`.
4. Update reserve.
5. Emit `UtilityNetworkStateChanged` when the state changes.

Load is deterministic. No random outage is applied here; outages are a separate step.

### Priority

When load exceeds capacity, service is reduced in a deterministic priority order:

1. civic and health buildings;
2. residential;
3. commercial;
4. luxury and discretionary.

The priority order is configuration, not hardcoded in a system.

### Expansion and maintenance

- Capacity expansion is a configured cost paid through the ledger.
- Maintenance can restore a strained network.
- A network never creates money; expansion is funded by government or a company account.

## Tasks

- [ ] Add the two networks to world bootstrap and restore.
- [ ] Add service links to building creation and migration.
- [ ] Add demand by building kind, tier and capacity.
- [ ] Implement `UtilityConsumptionSystem` load and state.
- [ ] Add deterministic priority and service reduction.
- [ ] Add network expansion and maintenance paths.
- [ ] Add `UtilityNetworkStateChanged` and admin read fields.
- [ ] Add tests for normal, strained, overloaded and offline states.

## Files

- new `Genelife/Consumption/UtilityNetworkService.cs`
- new `Genelife/Systems/UtilityConsumptionSystem.cs`
- `Genelife/Housing/HousingBootstrap.cs`
- `Genelife/Map/BuildingPlacementService.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Consumption/UtilityNetworkTests.cs`

## Acceptance

- Every served building has one link per service.
- Load equals the sum of linked demand and usage.
- Capacity changes move the network state deterministically.
- Overload reduces service in the configured priority order.
- Expansion and maintenance are ledgered.
- The admin can see production, capacity, load, reserve and state.
- No building is silently served without a link.

## Testing

- Add buildings and assert load and state.
- Overload the network and assert priority reduction.
- Expand capacity and assert the state recovers.
- Remove a link and assert the building is unserved.
- Restore and assert networks, links and load.
