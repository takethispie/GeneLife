using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Builds housing in response to low vacancy and construction economics.
/// Companies pay in monthly instalments; the municipality builds shelter when
/// homelessness crosses the configured threshold.
/// </summary>
public class HousingDevelopmentSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly MapContext? map;
    private readonly int interval;

    public HousingDevelopmentSystem(World world, EconomyContext economy, MapContext? map = null, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        this.map = map;
        this.interval = interval ?? Math.Max(1, economy.Clock.TicksPerPayCycle);
    }

    public string Name => "HousingDevelopment";
    public int Order => 595;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        AdvanceProjects(tick);

        if (CountActiveProjects() >= economy.HousingOptions.MaxActiveConstructionProjects)
            return;

        if (ShouldBuildShelter())
        {
            if (TryStartShelterProject(tick))
                return;
        }

        var total = 0;
        var vacant = 0;
        var unitQuery = new QueryDescription().WithAll<HousingUnit>();
        world.Query(in unitQuery, (ref HousingUnit unit) =>
        {
            if (unit.IsCondemned)
                return;
            total++;
            if (unit.IsVacant)
                vacant++;
        });

        var vacancyRate = total > 0 ? vacant / (decimal)total : 1m;
        if (vacancyRate > economy.HousingOptions.ConstructionVacancyTrigger)
            return;

        StartCompanyProject(tick);
    }

    public Entity StartProject(
        Entity developer,
        string district,
        HousingTier tier,
        int units,
        long tick)
    {
        if (units <= 0)
            throw new ArgumentOutOfRangeException(nameof(units));

        var months = Math.Max(1, economy.HousingOptions.ConstructionMonths);
        var costPerUnit = HousingTierPolicy.ConstructionCostPerUnit(tier);
        var cost = costPerUnit * units;

        var entity = world.Create(new ConstructionProject
        {
            Developer = developer,
            District = district,
            Tier = tier,
            Units = units,
            Cost = cost,
            PaidToDate = Money.Zero,
            MonthsRemaining = months,
            TotalMonths = months,
            StartedAtTick = tick
        });
        var id = registry.AllocateNetworkId(entity);
        var project = world.Get<ConstructionProject>(entity);
        project.ProjectId = id.Value;
        world.Get<ConstructionProject>(entity) = project;

        economy.Emit(new ConstructionStarted(
            id.Value,
            NetworkIdOf(developer),
            district,
            tier.ToString(),
            units,
            cost.Cents,
            months,
            tick));

        return entity;
    }

    private void StartCompanyProject(long tick)
    {
        var developers = new List<(Entity Entity, CompanyAccount Account)>();
        var query = new QueryDescription().WithAll<CompanyName, CompanyAccount>();
        world.Query(in query, (Entity entity, ref CompanyName _, ref CompanyAccount account) =>
        {
            if (account.State is CompanySolvency.Healthy or CompanySolvency.Tight)
                developers.Add((entity, account));
        });

        developers.Sort((a, b) =>
        {
            var byBalance = b.Account.Balance.Cents.CompareTo(a.Account.Balance.Cents);
            return byBalance != 0 ? byBalance : a.Entity.Id.CompareTo(b.Entity.Id);
        });

        var months = Math.Max(1, economy.HousingOptions.ConstructionMonths);
        var units = 6;
        var tier = HousingTier.Basic;
        var cost = HousingTierPolicy.ConstructionCostPerUnit(tier) * units;
        var firstPayment = new Money((long)Math.Ceiling(cost.Cents / (decimal)months));

        foreach (var (developer, account) in developers)
        {
            if (account.Balance < firstPayment)
                continue;

            var district = ChooseDistrict();
            if (district is null)
                return;

            StartProject(developer, district, tier, units, tick);
            return;
        }
    }

    private bool TryStartShelterProject(long tick)
    {
        var units = 4;
        var months = Math.Max(1, economy.HousingOptions.ConstructionMonths);
        var cost = HousingTierPolicy.ConstructionCostPerUnit(HousingTier.Shelter) * units;
        var firstPayment = new Money((long)Math.Ceiling(cost.Cents / (decimal)months));
        if (economy.Government.Balance < firstPayment)
            return false;

        StartProject(Entity.Null, ChooseDistrict() ?? "Municipal", HousingTier.Shelter, units, tick);
        return true;
    }

    private void AdvanceProjects(long tick)
    {
        var projects = new List<(Entity Entity, ConstructionProject Project)>();
        var query = new QueryDescription().WithAll<ConstructionProject>();
        world.Query(in query, (Entity entity, ref ConstructionProject project) =>
            projects.Add((entity, project)));
        projects.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

        foreach (var entry in projects)
        {
            var entity = entry.Entity;
            var project = entry.Project;
            if (!world.IsAlive(entity))
                continue;

            var payment = project.PaymentPerMonth;
            var paid = Pay(project.Developer, payment, tick);

            if (paid < payment)
            {
                economy.Emit(new ConstructionCancelled(project.ProjectId, "funding-shortfall", tick));
                registry.DetachNetworkId(entity);
                world.Destroy(entity);
                continue;
            }

            project.PaidToDate += paid;
            project.MonthsRemaining--;

            if (project.MonthsRemaining <= 0)
            {
                CompleteProject(tick, entity, project);
                continue;
            }

            world.Get<ConstructionProject>(entity) = project;
        }
    }

    private void CompleteProject(long tick, Entity entity, ConstructionProject project)
    {
        var property = world.Create(new Property(
            $"{project.District} {project.Tier} {project.ProjectId}",
            project.District,
            project.Developer,
            project.Units,
            HousingTierPolicy.Quality(project.Tier),
            Neighbourhood(project.ProjectId),
            economy.HousingOptions.DefaultPropertyUpkeep,
            project.Tier == HousingTier.Shelter));
        var propertyId = registry.AllocateNetworkId(property);
        registry.AddProperty(property, project.Developer);

        if (map is not null)
        {
            var footprint = BuildingFootprints.ForProperty(project.Units, project.Tier == HousingTier.Shelter);
            var kind = project.Tier == HousingTier.Shelter ? BuildingKind.Civic : BuildingKind.Residential;
            var placement = map.Placement.TryPlace(property, propertyId, kind, footprint, project.District);
            if (!placement.Success)
            {
                registry.DetachNetworkId(property);
                world.Destroy(property);
                economy.Emit(new ConstructionCancelled(project.ProjectId, "no-placement", tick));
                registry.DetachNetworkId(entity);
                world.Destroy(entity);
                return;
            }
        }

        for (var i = 0; i < project.Units; i++)
        {
            var unit = world.Create(new HousingUnit(
                property,
                $"{project.ProjectId}-{i + 1}",
                project.Tier,
                HousingTierPolicy.Capacity(project.Tier),
                HousingTierPolicy.BaseRent(project.Tier),
                HousingTierPolicy.Quality(project.Tier),
                isVacant: true,
                owner: Entity.Null,
                purchasePrice: HousingTierPolicy.PurchasePrice(project.Tier)));
            registry.AllocateNetworkId(unit);
            registry.AddUnit(unit, property);
        }

        registry.Market.CompletionsThisYear += project.Units;
        economy.Emit(new ConstructionCompleted(project.ProjectId, propertyId.Value, project.Units, tick));
        economy.Emit(new PropertyBuilt(
            propertyId.Value,
            NetworkIdOf(project.Developer),
            project.District,
            project.Units,
            tick));

        registry.DetachNetworkId(entity);
        world.Destroy(entity);
    }

    private Money Pay(Entity developer, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return Money.Zero;

        if (developer == Entity.Null)
        {
            var paid = economy.Government.Balance.Min(amount);
            if (paid.IsPositive)
            {
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.ConstructionCost,
                    EconomyAccountIds.Government,
                    EconomyAccountIds.Government,
                    paid,
                    "housing.construction.municipal");
            }
            return paid;
        }

        if (!world.IsAlive(developer) || !world.Has<CompanyAccount>(developer))
            return Money.Zero;

        var account = world.Get<CompanyAccount>(developer);
        var payable = account.Balance.Min(amount);
        if (!payable.IsPositive)
            return Money.Zero;

        if (!economy.Accounts.TryDebit(
                developer,
                payable,
                LedgerKind.ConstructionCost,
                Guid.NewGuid(),
                EconomyAccountIds.Government,
                "housing.construction",
                tick))
            return Money.Zero;

        economy.Government.Balance += payable;
        return payable;
    }

    private bool ShouldBuildShelter()
    {
        var households = 0;
        var homeless = 0;
        var query = new QueryDescription().WithAll<Household>();
        world.Query(in query, (ref Household household) =>
        {
            households++;
            if (household.Unit == Entity.Null)
                homeless++;
        });

        if (households == 0)
            return false;

        var share = homeless / (decimal)households;
        if (share <= economy.HousingOptions.HomelessConstructionTrigger)
            return false;

        var activeShelter = false;
        var projectQuery = new QueryDescription().WithAll<ConstructionProject>();
        world.Query(in projectQuery, (ref ConstructionProject project) =>
        {
            if (project.Tier == HousingTier.Shelter)
                activeShelter = true;
        });
        return !activeShelter;
    }

    private int CountActiveProjects()
    {
        var count = 0;
        var query = new QueryDescription().WithAll<ConstructionProject>();
        world.Query(in query, (ref ConstructionProject _) => count++);
        return count;
    }

    private string? ChooseDistrict()
    {
        string? best = null;
        var bestRoom = 0;
        foreach (var (district, capacity) in registry.Market.DistrictCapacity)
        {
            var room = capacity - registry.Market.UnitsIn(district);
            if (room > bestRoom)
            {
                bestRoom = room;
                best = district;
            }
        }

        if (best is null)
        {
            var defaultRoom = economy.HousingOptions.DefaultDistrictCapacity - registry.Market.UnitsIn("Central");
            best = "Central";
            bestRoom = defaultRoom;
        }

        return bestRoom > 0 ? best : null;
    }

    private static float Neighbourhood(ulong projectId) =>
        0.4f + (projectId % 5) * 0.1f;

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}
