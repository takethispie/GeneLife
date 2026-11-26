using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Player;
using Genelife.Economy;
using Genelife.Messages.Events;

namespace Genelife.Application.Systems;

/// <summary>
/// Applies the visible consequences of reduced or cut-off utility service. It
/// modifies existing needs and health only; it never creates a parallel health
/// model and never moves money.
/// </summary>
public sealed class UtilityOutageSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public UtilityOutageSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "UtilityOutage";
    public int Order => 472;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var households = new List<(Entity Entity, Household Household, HouseholdMeter Meter)>();
        world.Query(in new QueryDescription().WithAll<Household, HouseholdMeter>(),
            (Entity entity, ref Household household, ref HouseholdMeter meter) =>
                households.Add((entity, household, meter)));
        households.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        foreach (var (entity, household, meter) in households)
        {
            if (!world.IsAlive(entity))
                continue;
            if (meter.WaterLevel == ServiceLevel.Full && meter.ElectricityLevel == ServiceLevel.Full)
                continue;

            var hygieneDelta = meter.WaterLevel switch
            {
                ServiceLevel.CutOff => -8f,
                ServiceLevel.Reduced => -3f,
                _ => 0f
            };
            var energyDelta = meter.ElectricityLevel switch
            {
                ServiceLevel.CutOff => -5f,
                ServiceLevel.Reduced => -2f,
                _ => 0f
            };
            var spoiled = 0;
            if (meter.ElectricityLevel == ServiceLevel.CutOff)
            {
                var pantryOwner = entity;
                if (world.Has<HouseholdPantry>(pantryOwner))
                {
                    var pantry = world.Get<HouseholdPantry>(pantryOwner);
                    spoiled = Math.Max(0, pantry.FoodUnits / 4);
                    if (spoiled > 0)
                    {
                        pantry.FoodUnits -= spoiled;
                        world.Get<HouseholdPantry>(pantryOwner) = pantry;
                        economy.Emit(new GrocerySpoiled(
                            GetNetworkId(entity),
                            "food",
                            spoiled,
                            "electricity_cutoff",
                            tick));
                    }
                }
            }

            var members = economy.Housing.GetMembers(entity);
            foreach (var member in members)
            {
                if (!world.IsAlive(member) || world.Has<Dead>(member))
                    continue;
                if (hygieneDelta != 0f && world.Has<Needs>(member))
                {
                    var needs = world.Get<Needs>(member);
                    needs.Hygiene = Math.Max(0f, needs.Hygiene + hygieneDelta);
                    needs.Energy = Math.Max(0f, needs.Energy + energyDelta);
                    world.Get<Needs>(member) = needs;
                }
                if (meter.WaterLevel == ServiceLevel.CutOff && world.Has<Health>(member))
                {
                    var health = world.Get<Health>(member);
                    health.Value = Math.Max(1f, health.Value - 0.5f);
                    world.Get<Health>(member) = health;
                }
            }

            if (hygieneDelta != 0f || energyDelta != 0f || spoiled > 0)
            {
                economy.Emit(new UtilityOutageApplied(
                    GetNetworkId(entity),
                    meter.WaterLevel.ToString(),
                    meter.ElectricityLevel.ToString(),
                    hygieneDelta,
                    energyDelta,
                    spoiled,
                    tick));
            }
        }
    }

    private ulong GetNetworkId(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}
