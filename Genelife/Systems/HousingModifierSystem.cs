using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// Recomputes the per-sim housing modifier once per month. The system only
/// writes when values change, so a stable population does not allocate.
/// </summary>
public class HousingModifierSystem : ISimulationSystem
{
    private readonly World world;
    private readonly Components.Housing.HousingRegistry registry;
    private readonly QueryDescription simQuery;
    private readonly List<(Entity Entity, NeedsModifier Modifier)> pending = new(256);
    private readonly int interval;

    public HousingModifierSystem(World world, Components.Housing.HousingRegistry registry, int? interval = null)
    {
        this.world = world;
        this.registry = registry;
        this.interval = interval ?? 100;
        simQuery = new QueryDescription().WithAll<Needs>().WithNone<Components.Gen.Dead>();
    }

    public string Name => "HousingModifier";
    public int Order => 80;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        if (registry.UnitsByProperty.Count == 0)
            return;

        pending.Clear();

        world.Query(in simQuery, (Entity entity, ref Needs _) =>
        {
            var modifier = registry.TryGetUnitForSim(entity, out var unit)
                ? ForResident(entity, unit)
                : ForHomeless();

            if (world.Has<NeedsModifier>(entity))
            {
                if (world.Get<NeedsModifier>(entity) != modifier)
                    world.Get<NeedsModifier>(entity) = modifier;
                return;
            }

            if (modifier != NeedsModifier.Neutral)
                pending.Add((entity, modifier));
        });

        foreach (var (entity, modifier) in pending)
        {
            if (world.IsAlive(entity) && !world.Has<NeedsModifier>(entity))
                world.Add(entity, modifier);
        }
    }

    private NeedsModifier ForResident(Entity sim, Entity unit)
    {
        if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
            return ForHomeless();

        var data = world.Get<HousingUnit>(unit);
        var property = world.IsAlive(data.Property) && world.Has<Property>(data.Property)
            ? world.Get<Property>(data.Property)
            : default;

        var modifier = BaseFor(data.Tier);
        var conditionPenalty = Math.Max(0f, 0.5f - data.Condition) * 2f;
        modifier.HygieneDecay += conditionPenalty * 0.5f;
        modifier.HealthDrift -= conditionPenalty * 0.4f;

        var household = registry.HouseholdIn(unit);
        if (household != Entity.Null &&
            world.IsAlive(household) && world.Has<Household>(household) &&
            world.Get<Household>(household).NoUtilities)
        {
            modifier.HygieneDecay *= 1.5f;
            modifier.HealthDrift -= 0.2f;
            modifier.NoUtilities = true;
        }

        var occupants = registry.MemberCount(registry.HouseholdOf(sim));
        if (occupants > data.Capacity)
        {
            modifier.SleepRecovery *= 0.8f;
            modifier.HygieneDecay *= 1.25f;
            modifier.HealthDrift -= 0.2f;
            modifier.Overcrowded = true;
        }

        if (property.NeighbourhoodScore < 0.4f)
            modifier.HealthDrift -= 0.1f;

        return modifier;
    }

    private static NeedsModifier ForHomeless()
    {
        var modifier = BaseFor(HousingTier.Shelter);
        modifier.HygieneDecay *= 1.25f;
        modifier.SleepRecovery *= 0.8f;
        modifier.HealthDrift -= 0.5f;
        modifier.IsHomeless = true;
        return modifier;
    }

    public static NeedsModifier BaseFor(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => new NeedsModifier
        {
            HungerDecay = 1f,
            EnergyDecay = 1.5f,
            HygieneDecay = 1.75f,
            BladderDecay = 1f,
            SleepRecovery = 0.5f,
            HygieneRecovery = 0.75f,
            HealthDrift = -0.4f
        },
        HousingTier.Comfortable => new NeedsModifier
        {
            HungerDecay = 1f,
            EnergyDecay = 0.9f,
            HygieneDecay = 0.85f,
            BladderDecay = 1f,
            SleepRecovery = 1f,
            HygieneRecovery = 1.05f,
            HealthDrift = 0.1f
        },
        HousingTier.Luxury => new NeedsModifier
        {
            HungerDecay = 1f,
            EnergyDecay = 0.8f,
            HygieneDecay = 0.75f,
            BladderDecay = 1f,
            SleepRecovery = 1.15f,
            HygieneRecovery = 1.15f,
            HealthDrift = 0.2f
        },
        _ => new NeedsModifier
        {
            HungerDecay = 1f,
            EnergyDecay = 1f,
            HygieneDecay = 1f,
            BladderDecay = 1f,
            SleepRecovery = 0.8f,
            HygieneRecovery = 0.9f,
            HealthDrift = 0f
        }
    };
}
