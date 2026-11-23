# T02 — Trait generation

**Phase:** 2 — Seeding
**Depends:** T01
**Estimate:** 1–2 sessions
**Status:** Done

## Goal

Give every sim created by the production paths a deterministic, plausible trait set from the world seed. The same seed must always produce the same sims with the same traits.

## Context

Sims are created by:

- `PopulationGenerator.CreateSim` for seeded children, workers, retirees and elders;
- `PopulationInflowSystem` for immigrants;
- `SimulationEngine.EnsurePlayerSim` for a browser player;
- `CreateSimHandler` for the admin `CreateSim` command;
- `BirthSystem` in feature 03 (future).

Today only the first two are routine population paths. V4 must not leave player, admin or immigrant sims without traits.

## Design

### Generator

```csharp
public static Traits Create(int worldSeed, ulong networkId, TraitSource source, GeneticsOptions options);
```

- Derive a per-sim seed from `worldSeed`, `networkId` and a stable string for the source (`traits.seed`, `traits.immigrant`, `traits.player`, `traits.admin`).
- Use `SimulationRandom.For("traits.seed")` as the stream registry, then a per-sim `Random` seeded from the stable hash. Do not depend on query order or creation order.
- Sex: configured female/male ratio, default 50/50.
- Aptitude: weighted distribution; default equal weights.
- Temperament: weighted distribution; default `Peaceful` 50%, `Neutral` 40%, `Violent` 10%.
- Physical traits: equal weights unless `GeneticsOptions` overrides them.
- Height potential: `Short` 25%, `Average` 50%, `Tall` 25%.

### Options

Add `GeneticsOptions`:

```csharp
public sealed class GeneticsOptions
{
    public decimal MutationRate { get; set; } = 0.04m;
    public decimal FemaleShare { get; set; } = 0.5m;
    public int[] AptitudeWeights { get; set; } = [1, 1, 1, 1, 1];
    public int[] TemperamentWeights { get; set; } = [50, 40, 10];
    public int[] HeightWeights { get; set; } = [25, 50, 25];
    public int[] EyeColorWeights { get; set; } = [40, 25, 20, 10, 5];
    public int[] HairColorWeights { get; set; } = [35, 30, 20, 10, 5];
    public int[] HandednessWeights { get; set; } = [88, 10, 2];
    public int[] MorphotypeWeights { get; set; } = [30, 45, 25];
}
```

Weights are validated: non-negative, at least one positive, and normalized internally.

### Integration points

- Seed population: add traits immediately after `NetworkId` allocation in `CreateSim`.
- Immigrants: add traits before the sim is added to a household or workforce.
- Player: add traits when `EnsurePlayerSim` creates the sim.
- Admin: add traits in `CreateSimHandler` when it creates a sim without traits.
- Tests that construct bare sims for unrelated systems do not need traits unless the trait invariant is explicitly exercised.

### Legacy and defaults

- A missing `Traits` component on a living sim is invalid in V4 and is fixed by the schema migration (T06), not by silently generating a new trait set at restore.
- A `Traits` component with undefined enum values is a hard restore error.

## Tasks

- [ ] Add `GeneticsOptions` and validation.
- [ ] Add `TraitsGenerator.Create` with stable per-sim seeding.
- [ ] Add `Traits` in `PopulationGenerator.CreateSim`.
- [ ] Add `Traits` in `PopulationInflowSystem` arrivals.
- [ ] Add `Traits` in `SimulationEngine.EnsurePlayerSim`.
- [ ] Add `Traits` in `CreateSimHandler`.
- [ ] Add a shared `SimTraitFactory` if more creation paths appear.
- [ ] Add generation tests for distribution and reproducibility.

## Files

- new `Genelife/Family/GeneticsOptions.cs`
- new `Genelife/Family/TraitsGenerator.cs`
- `Genelife.API/Services/PopulationGenerator.cs`
- `Genelife/Systems/PopulationInflowSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.API/Handlers/CreateSimHandler.cs`
- `Genelife.UnitTests/Family/TraitGenerationTests.cs`

## Acceptance

- Every sim created by the production paths has exactly one `Traits` component.
- The same seed and options produce identical trait values for identical network ids.
- Different seeds produce visibly different distributions over a population.
- Distribution weights are respected within a tolerance over a large sample.
- No trait generation reads ambient `Random.Shared` or wall-clock state.

## Testing

- Generate 10,000 sims and assert trait distributions and sex ratio.
- Generate the same seed twice and compare all traits.
- Create a player sim and an immigrant and assert both have traits.
- Assert weight validation rejects negative or all-zero arrays.