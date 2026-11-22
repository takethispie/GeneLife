using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Life;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.Api.Services;

/// <summary>
/// Deterministic population and household assignment. The same plan and seed
/// produce the same sims, households and homes on every run.
/// </summary>
public sealed class HouseholdAssigner
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext economy;
    private readonly HouseholdRepository households;
    private readonly HousingAllocator allocator;
    private readonly HouseholdFinanceService finance;

    public HouseholdAssigner(World world, NetworkIdRegistry networkIds, EconomyContext economy)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
        households = new HouseholdRepository(world, networkIds, economy);
        allocator = new HousingAllocator(world, economy);
        finance = new HouseholdFinanceService(world, economy);
    }

    public AssignmentResult Spawn(PopulationPlan plan, int seed, long tick)
    {
        plan.Validate();
        EnsureStock(tick, plan);

        var random = new Random(seed);
        var adults = new List<Entity>();
        var children = new List<Entity>();

        for (var i = 0; i < plan.Adults; i++)
        {
            var age = random.Next(19, 66);
            adults.Add(CreateSim($"Adult {i + 1}", age, random, isAdult: true, tick));
        }

        for (var i = 0; i < plan.Children; i++)
        {
            var age = random.Next(1, 18);
            children.Add(CreateSim($"Child {i + 1}", age, random, isAdult: false, tick));
        }

        var householdIds = FormHouseholds(adults, random, tick);
        AttachChildren(children, householdIds, random, tick);
        var result = AssignHomes(householdIds, plan, tick);
        result = result with
        {
            Adults = plan.Adults,
            Children = plan.Children,
            Households = householdIds.Count
        };

        economy.Emit(new PopulationSpawned(
            result.Adults,
            result.Children,
            result.Households,
            result.Housed,
            result.Sheltered,
            result.Homeless,
            tick));

        return result;
    }

    public AssignmentResult AssignExistingHomeless(int seed, long tick)
    {
        EnsureStock(tick);

        var random = new Random(seed);
        var adults = new List<Entity>();
        var children = new List<Entity>();

        var query = new QueryDescription().WithAll<Alive>();
        world.Query(in query, (Entity entity, ref Alive alive) =>
        {
            if (economy.Housing.HouseholdBySim.ContainsKey(entity))
                return;
            if (alive.Age >= 18)
                adults.Add(entity);
            else
                children.Add(entity);
        });

        var householdIds = FormHouseholds(adults, random, tick);
        AttachChildren(children, householdIds, random, tick);
        var plan = new PopulationPlan { ExpectedMonthlyIncome = 300m };
        var result = AssignHomes(householdIds, plan, tick);

        economy.Emit(new HousingMigrated(result.Households, economy.Housing.UnitsByProperty.Count, tick));
        return result;
    }

    public AssignmentResult AssignSingle(ulong simId, ulong unitId, long tick)
    {
        if (!networkIds.TryGetEntity(new NetworkId(simId), out var sim) || !world.IsAlive(sim))
            throw new InvalidOperationException("Sim not found.");
        if (!world.Has<GenName>(sim))
            throw new InvalidOperationException("Entity is not a sim.");

        Entity household;
        if (economy.Housing.HouseholdBySim.TryGetValue(sim, out household))
        {
            if (unitId != 0 && networkIds.TryGetEntity(new NetworkId(unitId), out var unit))
                allocator.TryAssign(household, unit, Tenure.Renter, tick);
            else
                allocator.TryAssignBest(household, tick);
            return SummarizeHousehold(household, tick);
        }

        var id = households.Create(simId, tick);
        if (!households.TryGet(id, out household))
            throw new InvalidOperationException("Household creation failed.");

        if (unitId != 0 && networkIds.TryGetEntity(new NetworkId(unitId), out var chosen))
            allocator.TryAssign(household, chosen, Tenure.Renter, tick);
        else
            allocator.TryAssignBest(household, tick);

        return SummarizeHousehold(household, tick);
    }

    private Entity CreateSim(string name, int age, Random random, bool isAdult, long tick)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f),
            new Wallet(Money.FromUnits(400 + random.Next(0, 200))),
            new ConsumptionState(),
            new SkillProfile(),
            new Health(HealthSystem.InitialForAge(age)),
            LifecycleFactory.Create(age, tick, economy.Clock, economy.Life.Policy, age * 31 + name.Length + (name.Length > 0 ? name[0] : 0)));

        economy.Life.Index.OnBorn(economy.Life.Policy.StageFor(age));

        if (isAdult)
        {
            world.Add(entity, new JobSeeker(
                true,
                Skill.None,
                Money.FromUnits(1000m)));
        }

        var id = networkIds.Allocate(entity);
        world.Add(entity, id);
        return entity;
    }

    private List<NetworkId> FormHouseholds(List<Entity> adults, Random random, long tick)
    {
        var ids = new List<NetworkId>();
        var index = 0;

        while (index < adults.Count)
        {
            var head = adults[index++];
            var headId = NetworkIdOf(head);
            var householdId = households.Create(headId, tick);
            ids.Add(householdId);

            var roll = random.NextDouble();
            if (roll < 0.55 || index >= adults.Count)
                continue;

            var partner = adults[index++];
            households.AddMember(householdId, NetworkIdOf(partner), HouseholdRole.Partner, tick);

            if (roll > 0.8 && index < adults.Count)
            {
                var relative = adults[index++];
                households.AddMember(householdId, NetworkIdOf(relative), HouseholdRole.Relative, tick);
            }
        }

        return ids;
    }

    private void AttachChildren(
        List<Entity> children,
        List<NetworkId> householdIds,
        Random random,
        long tick)
    {
        if (householdIds.Count == 0)
            return;

        foreach (var child in children)
        {
            var target = householdIds[random.Next(householdIds.Count)];
            households.AddMember(target, NetworkIdOf(child), HouseholdRole.Child, tick);
        }
    }

    private AssignmentResult AssignHomes(List<NetworkId> householdIds, PopulationPlan plan, long tick)
    {
        var result = new AssignmentResult { Households = householdIds.Count };

        foreach (var id in householdIds)
        {
            if (!households.TryGet(id, out var household))
                continue;

            var income = finance.MonthlyIncome(household);
            var expected = Money.FromUnits(Math.Max(income.Units, plan.ExpectedMonthlyIncome));

            if (!allocator.TryFindBest(household, out var unit, out var sheltered, incomeOverride: expected))
            {
                result = result with { Homeless = result.Homeless + 1 };
                continue;
            }

            allocator.TryAssign(
                household,
                unit,
                sheltered ? Tenure.Sheltered : Tenure.Renter,
                tick);

            if (sheltered)
                result = result with { Sheltered = result.Sheltered + 1 };
            else
                result = result with { Housed = result.Housed + 1 };
        }

        return result;
    }

    private AssignmentResult SummarizeHousehold(Entity household, long tick)
    {
        var data = world.Get<Household>(household);
        var housed = data.Unit != Entity.Null && data.Tenure == Tenure.Renter ? 1 : 0;
        var sheltered = data.Unit != Entity.Null && data.Tenure == Tenure.Sheltered ? 1 : 0;
        var homeless = data.Unit == Entity.Null ? 1 : 0;
        return new AssignmentResult
        {
            Adults = 1,
            Households = 1,
            Housed = housed,
            Sheltered = sheltered,
            Homeless = homeless
        };
    }

    private void EnsureStock(long tick, PopulationPlan? plan = null)
    {
        if (economy.Housing.UnitsByProperty.Count > 0)
            return;

        if (plan is not null &&
            plan.BasicUnits + plan.ComfortableUnits + plan.LuxuryUnits + plan.ShelterBeds > 0)
        {
            HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Basic,
                plan.BasicUnits, 0.6f, 0.5f, tick);
            HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Comfortable,
                plan.ComfortableUnits, 0.8f, 0.65f, tick);
            HousingBootstrap.CreateProperty(world, economy, "Central", HousingTier.Luxury,
                plan.LuxuryUnits, 0.95f, 0.8f, tick);
            HousingBootstrap.CreateShelter(world, economy, "Central", plan.ShelterBeds, tick);
            return;
        }

        HousingBootstrap.CreateStock(world, economy, tick);
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}

public sealed record AssignmentResult
{
    public int Adults { get; init; }
    public int Children { get; init; }
    public int Households { get; init; }
    public int Housed { get; init; }
    public int Sheltered { get; init; }
    public int Homeless { get; init; }
}
