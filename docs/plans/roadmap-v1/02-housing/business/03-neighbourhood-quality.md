# Neighbourhood quality

Two identical units at the same rent should not feel identical. Quality is the abstraction that makes housing a choice rather than a slot.

## Quality dimensions

- **Space** — number of rooms per household member (overcrowding).
- **Condition** — maintenance state of the building.
- **Amenities** — kitchen, bathroom, heating, laundry.
- **Neighbourhood** — safety, pollution, access to work and schools.
- **Density** — how many households share the building.

V1 collapses these into a `HousingQuality` tier plus two numbers: `Condition` (0–1) and `NeighbourhoodScore` (0–1). Tiers set the base, condition and neighbourhood modify it.

## Effects on the population

| Quality | Sleep recovery | Hygiene | Health drift | Notes |
|---|---|---|---|---|
| Shelter | 50% | 25% | negative | sleeping rough or crowded shelter |
| Basic | 80% | 75% | neutral | small apartment |
| Comfortable | 100% | 100% | small positive | family home |
| Luxury | 115% | 110% | positive | large house |

Quality does not replace needs; it scales how efficiently actions restore them. A hungry NPC in a luxury home still needs to eat.

## Overcrowding

If members exceed `Capacity`, the unit becomes overcrowded regardless of tier:

- sleep recovery drops;
- hygiene decays faster;
- health drift turns negative;
- rent burden per person rises.

This makes household formation and housing supply interact: a growing household must move or suffer.

## Maintenance and decline

Buildings decay. Condition falls slowly each month unless maintenance is paid. A landlord who stops maintaining loses quality and tenants. This creates a reason for property owners to spend, and a visible decline path for neglected housing.

## Admin visibility

- quality distribution of occupied and vacant units;
- overcrowded households;
- buildings with low condition;
- correlation between quality and health/mortality (reported, not causal proof);
- manual quality adjustment for scenario testing.

Quality is the main tuning surface for making housing feel like it matters without adding a full building simulation.
