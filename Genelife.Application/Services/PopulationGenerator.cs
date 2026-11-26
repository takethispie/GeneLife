using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Repositories;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Life;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.Api.Services;

public sealed record PopulationGenerationResult(
    int Total,
    int Education,
    int Work,
    int Retirement,
    int Elderly,
    int Schools,
    int Employed,
    int Households,
    int Housed);

/// <summary>
/// Builds a playable-ageing population: schools, age pyramid, households,
/// housing stock, companies and direct hires. Used by the admin generator and
/// seeds. All work is deterministic from the supplied seed.
/// </summary>
public sealed class PopulationGenerator
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext economy;
    private readonly EmploymentIndex employmentIndex;
    private readonly MapContext? map;
    private readonly RelationshipIndex relationshipIndex;
    private readonly RelationshipService relationshipService;
    private readonly RelationshipOptions relationshipOptions;
    private int worldSeed = 1;

    public PopulationGenerator(
        World world,
        NetworkIdRegistry networkIds,
        EconomyContext economy,
        EmploymentIndex employmentIndex,
        MapContext? map = null,
        RelationshipIndex? relationshipIndex = null,
        RelationshipService? relationshipService = null,
        RelationshipOptions? relationshipOptions = null)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
        this.employmentIndex = employmentIndex;
        this.map = map;
        this.relationshipIndex = relationshipIndex ?? new RelationshipIndex();
        this.relationshipService = relationshipService ?? new RelationshipService(world, this.relationshipIndex)
        {
            NetworkIdAllocator = entity => networkIds.Allocate(entity),
            NetworkIdRemover = entity => networkIds.Remove(entity)
        };
        this.relationshipOptions = relationshipOptions ?? RelationshipOptions.Default;
        this.relationshipIndex.NetworkIdResolver = entity =>
            world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
        this.relationshipOptions.Validate();
    }

    public PopulationGenerationResult Generate(
        int totalGenoses,
        int seed = 1,
        int? adults = null,
        int? children = null,
        long tick = 0)
    {
        if (totalGenoses <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalGenoses), "Total genoses must be positive.");

        worldSeed = seed;
        var random = new Random(seed);
        if (!economy.Government.Balance.IsPositive)
        {
            var endowment = economy.Options.SeededGovernmentFundPerGenos * totalGenoses;
            if (endowment.IsPositive)
            {
                economy.Government.Balance = endowment;
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.CapitalInjected,
                    null,
                    EconomyAccountIds.Government,
                    endowment,
                    "population.government.seed");
            }
        }

        var pensionSeed = economy.Options.SeededPensionFundPerGenos * totalGenoses;
        if (!economy.Pension.Balance.IsPositive && pensionSeed.IsPositive &&
            economy.Government.Balance >= pensionSeed)
        {
            economy.Government.Balance -= pensionSeed;
            economy.Pension.Balance += pensionSeed;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.PensionContribution,
                EconomyAccountIds.Government,
                EconomyAccountIds.PensionFund,
                pensionSeed,
                "population.pension.seed");
        }

        var childCount = children ?? Math.Max(0, totalGenoses * 22 / 100);
        var adultCount = adults ?? Math.Max(0, totalGenoses - childCount);
        if (childCount + adultCount > totalGenoses)
            adultCount = Math.Max(0, totalGenoses - childCount);

        var retirementCount = adultCount * 20 / 100;
        var elderlyCount = adultCount * 10 / 100;
        var workCount = Math.Max(0, adultCount - retirementCount - elderlyCount);

        var schoolCount = EnsureSchools(childCount, tick);
        var companyCount = EnsureCompanies(workCount, tick);
        EnsureHousing(adultCount);

        var education = new List<Entity>();
        var workers = new List<Entity>();
        var retirees = new List<Entity>();
        var elders = new List<Entity>();

        for (var i = 0; i < childCount; i++)
        {
            var age = random.Next(economy.Life.Policy.SchoolStartAge, economy.Life.Policy.WorkStartAge);
            education.Add(CreateGenos($"Student {i + 1}", age, LifeStage.Education, random, tick,
                new Money(0), profile: ChildProfile(random)));
        }

        for (var i = 0; i < workCount; i++)
        {
            var age = random.Next(economy.Life.Policy.WorkStartAge, economy.Life.Policy.RetirementAge);
            workers.Add(CreateGenos($"Worker {i + 1}", age, LifeStage.Work, random, tick,
                Money.FromUnits(400 + random.Next(0, 200)), profile: AdultProfile(random), lookingForWork: true));
        }

        for (var i = 0; i < retirementCount; i++)
        {
            var age = random.Next(economy.Life.Policy.RetirementAge, economy.Life.Policy.ElderlyAge);
            retirees.Add(CreateGenos($"Retiree {i + 1}", age, LifeStage.Retirement, random, tick,
                Money.FromUnits(400 + random.Next(0, 400)), profile: AdultProfile(random), retired: true));
        }

        for (var i = 0; i < elderlyCount; i++)
        {
            var age = random.Next(economy.Life.Policy.ElderlyAge, Math.Min(96, economy.Life.Policy.MaxAge));
            elders.Add(CreateGenos($"Elder {i + 1}", age, LifeStage.Elderly, random, tick,
                Money.FromUnits(200 + random.Next(0, 400)), profile: AdultProfile(random), retired: true));
        }

        var adultEntities = workers.Concat(retirees).Concat(elders).ToList();
        var familyBuilder = new SeedFamilyBuilder(world, relationshipIndex, relationshipService, relationshipOptions);
        familyBuilder.BuildPairs(adultEntities, tick);

        var households = BuildHouseholds(
            workers, retirees, elders, education, familyBuilder, random, tick);
        var employed = DirectHire(workers, tick);
        var housed = CountHoused();

        economy.Life.Index.Rebuild(world);
        economy.Life.Immigration.TargetPopulation = economy.Life.Index.TotalLiving;
        economy.Housing.Rebuild(world);

        var issues = LifecycleInvariants.Verify(world, economy.Life.Policy);
        if (issues.Count > 0)
            throw new InvalidOperationException("Generated population failed lifecycle validation: " +
                                                string.Join("; ", issues));

        var result = new PopulationGenerationResult(
            economy.Life.Index.TotalLiving,
            education.Count,
            workCount,
            retirees.Count,
            elders.Count,
            schoolCount,
            employed,
            households,
            housed);

        economy.Emit(new PopulationGenerated(
            result.Total,
            result.Education,
            result.Work,
            result.Retirement,
            result.Elderly,
            result.Schools,
            tick));
        economy.Emit(new PopulationHealthCheck(
            "generator",
            "Pass",
            $"{result.Total} genoses, {companyCount} companies, {result.Housed}/{result.Employed + result.Retirement + result.Elderly} adults housed"));
        return result;
    }

    private Entity CreateGenos(
        string name,
        int age,
        LifeStage stage,
        Random random,
        long tick,
        Money cash,
        SkillProfile profile,
        bool lookingForWork = false,
        bool retired = false)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f),
            new Wallet(cash),
            new ConsumptionState(),
            profile,
            new Health(HealthSystem.InitialForAge(age)),
            LifecycleFactory.Create(age, tick, economy.Clock, economy.Life.Policy, random.Next()));

        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        var traits = TraitsGenerator.Create(worldSeed, networkId.Value, TraitSource.Seed, economy.Genetics);
        world.Add(entity, traits);
        AptitudeSkills.ApplyStartingBonus(ref profile, traits.Aptitude);
        world.Get<SkillProfile>(entity) = profile;
        if (map is not null)
            MapSpawner.PlaceGenos(world, entity, map, networkId.Value);
        economy.Emit(new GenosCreated(networkId.Value, name, age));

        if (cash.IsPositive)
        {
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.Grant,
                null,
                networkId.Value,
                cash,
                "population.seed");
        }

        if (lookingForWork)
            world.Add(entity, new JobSeeker(true, profile.ToMask(),
                economy.Life.Careers.SalaryForSkills(profile.Total)));

        if (retired)
        {
            var account = new PensionAccount
            {
                EmployeeContributions = Money.FromUnits(4_000 + random.Next(0, 6_000)),
                EmployerContributions = Money.FromUnits(4_000 + random.Next(0, 6_000)),
                ContributingCycles = random.Next(20, 45)
            };
            world.Add(entity, account);

            var pension = economy.Life.Pensions.ProjectedMonthlyPension(account, economy.Life.Policy);
            world.Add(entity, new RetirementIncome(
                pension,
                tick - economy.Clock.TicksPerYear * random.Next(0, 8),
                "statement"));
        }

        return entity;
    }

    private int BuildHouseholds(
        List<Entity> workers,
        List<Entity> retirees,
        List<Entity> elders,
        List<Entity> children,
        SeedFamilyBuilder familyBuilder,
        Random random,
        long tick)
    {
        var adults = workers.Concat(retirees).Concat(elders).ToList();
        var households = new List<Entity>();
        var allocator = new HousingAllocator(world, economy);

        foreach (var adult in adults)
        {
            var household = CreateHousehold(adult, tick);
            world.Add(adult, new HouseholdMember(household, HouseholdRole.Head));
            economy.Housing.AddMember(adult, household);
            allocator.TryAssignBest(household, tick);
            households.Add(household);
        }

        if (households.Count > 0)
        {
            foreach (var child in children)
                AssignChild(child, households, random, familyBuilder, tick);
        }

        return households.Count;
    }

    private Entity CreateHousehold(Entity head, long tick)
    {
        var household = world.Create(new Household
        {
            Head = head,
            Unit = Entity.Null,
            Tenure = Tenure.Renter,
            MoveInTick = tick,
            MemberCount = 1
        });
        world.Add(household, networkIds.Allocate(household));
        return household;
    }

    private void AssignChild(
        Entity child,
        IReadOnlyList<Entity> households,
        Random random,
        SeedFamilyBuilder familyBuilder,
        long tick)
    {
        var target = households[random.Next(households.Count)];

        world.Add(child, new HouseholdMember(target, HouseholdRole.Child));
        economy.Housing.AddMember(child, target);

        var parentSet = new HashSet<Entity>();
        foreach (var member in economy.Housing.GetMembers(target))
        {
            if (member == child || !world.IsAlive(member) || !world.Has<Alive>(member))
                continue;
            parentSet.Add(member);
            foreach (var partner in relationshipIndex.PartnersOf(member))
                parentSet.Add(partner);
        }

        var parents = parentSet
            .Where(parent => world.IsAlive(parent))
            .OrderBy(NetworkIdOf)
            .ThenBy(parent => parent.Id)
            .ToList();
        familyBuilder.CreateParentEdges(child, parents, tick);
    }



    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;

    private int DirectHire(List<Entity> workers, long tick)
    {
        var postings = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<JobPosting>(),
            (Entity entity, ref JobPosting _) => postings.Add(entity));

        var hired = 0;
        foreach (var postingEntity in postings)
        {
            if (!world.IsAlive(postingEntity) || !world.Has<JobPosting>(postingEntity))
                continue;

            var posting = world.Get<JobPosting>(postingEntity);
            var seats = new List<Entity>();
            world.Query(in new QueryDescription().WithAll<OpenPosition>(),
                (Entity entity, ref OpenPosition seat) =>
                {
                    if (seat.Posting == postingEntity)
                        seats.Add(entity);
                });

            foreach (var worker in workers.ToList())
            {
                if (seats.Count == 0)
                    break;

                if (!world.IsAlive(worker) || world.Has<Employee>(worker))
                    continue;

                world.Add(worker, new Employee(
                    posting.Company,
                    posting.Office,
                    posting.Position,
                    posting.SalaryPerCycle)
                {
                    HiredAtTick = tick
                });
                world.Add(worker, new EmploymentRecord(tick));
                world.Add(worker, new WorkSchedule(posting.Office));
                if (world.Has<JobSeeker>(worker))
                    world.Remove<JobSeeker>(worker);

                employmentIndex.Add(posting.Company, worker);
                OfficeSeatAccounting.RefreshUsedSeats(world, posting.Office);

                var seat = seats[0];
                seats.RemoveAt(0);
                if (world.IsAlive(seat))
                    world.Destroy(seat);
                hired++;
                workers.Remove(worker);
            }

            posting.OpenSeats = seats.Count;
            posting.IsClosed = seats.Count == 0;
            world.Get<JobPosting>(postingEntity) = posting;
        }

        return hired;
    }

    private int EnsureSchools(int childCount, long tick)
    {
        var existing = 0;
        world.Query(in new QueryDescription().WithAll<School>(), (ref School _) => existing++);
        if (existing > 0)
            return existing;

        var capacity = Math.Max(27, childCount + 5);
        CreateSchool(
            "Central Primary",
            "Central",
            capacity,
            Skill.Programming,
            Money.FromUnits(50),
            tick);
        CreateSchool(
            "Central Arts",
            "Central",
            Math.Max(22, childCount / 2 + 5),
            Skill.Art,
            Money.FromUnits(50),
            tick);
        return 2;
    }

    private Entity CreateSchool(
        string name,
        string district,
        int capacity,
        Skill emphasis,
        Money monthlyCost,
        long tick)
    {
        var entity = world.Create(new School(
            name,
            district,
            capacity,
            0.6f + (emphasis == Skill.Programming ? 0.1f : 0.05f),
            emphasis,
            monthlyCost));
        var id = networkIds.Allocate(entity);
        world.Add(entity, id);
        if (map is not null)
        {
            var placement = map.Placement.TryPlace(
                entity,
                id,
                BuildingKind.School,
                BuildingFootprints.ForSchool(capacity),
                district);
            if (!placement.Success)
                throw new InvalidOperationException(
                    $"Unable to place school '{name}': {placement.Error} {placement.Message}");
        }
        return entity;
    }

    private static readonly string[] CompanyNames =
    {
        "Northwind", "Bluebird", "Cedar", "Harbor", "Summit", "Prairie", "Atlas",
        "Beacon", "Copper", "Delta", "Ember", "Falcon", "Granite", "Hollow",
        "Iron", "Juniper", "Keystone", "Lumen", "Maple", "Nimbus"
    };

    private int EnsureCompanies(int workCount, long tick)
    {
        var existing = 0;
        world.Query(in new QueryDescription().WithAll<CompanyName>(), (ref CompanyName _) => existing++);
        if (existing > 0)
            return existing;

        var employmentShare = Math.Clamp(1m - economy.Options.SeedUnemploymentRate, 0.5m, 1m);
        var seats = (int)Math.Round(workCount * (double)employmentShare);
        if (seats <= 0)
            return 0;

        const int maxSeatsPerCompany = 10;
        const int minSeatsPerCompany = 4;
        var companyCount = Math.Max(1, (int)Math.Ceiling(seats / (double)maxSeatsPerCompany));
        if (seats / companyCount < minSeatsPerCompany)
            companyCount = Math.Max(1, seats / minSeatsPerCompany);

        var companyRepository = new CompanyRepository(world, employmentIndex, networkIds, economy, map: map);
        var postingRepository = new JobPostingRepository(world, networkIds, economy);
        var skills = new[] { Skill.Sales, Skill.Programming, Skill.Engineering };
        var baseSeats = seats / companyCount;
        var remainder = seats % companyCount;

        for (var i = 0; i < companyCount; i++)
        {
            var companySeats = baseSeats + (i < remainder ? 1 : 0);
            if (companySeats <= 0)
                continue;

            var companyName = i < CompanyNames.Length
                ? CompanyNames[i]
                : $"{CompanyNames[i % CompanyNames.Length]} {i / CompanyNames.Length + 1}";
            NetworkId companyId;
            try
            {
                companyId = companyRepository.Add(
                    companyName,
                    capital: economy.Options.SeededCompanyCapital);
            }
            catch (InvalidOperationException)
            {
                break;
            }
            economy.Emit(new CompanyCreated(companyId.Value, companyName));
            if (!networkIds.TryGetEntity(companyId, out var company))
                continue;
            if (world.Has<CompanyAccount>(company))
            {
                ref var account = ref world.Get<CompanyAccount>(company);
                account.State = CompanySolvency.Healthy;
            }

            var office = Entity.Null;
            world.Query(in new QueryDescription().WithAll<Office, NetworkId>(),
                (Entity entity, ref Office officeData, ref NetworkId _) =>
                {
                    if (office == Entity.Null && officeData.Company == company)
                        office = entity;
                });
            if (office == Entity.Null)
                continue;

            var officeNetworkId = world.Get<NetworkId>(office);
            postingRepository.Open(
                companyId,
                officeNetworkId,
                i % 2 == 0 ? "Sales Associate" : "Engineer",
                Money.FromUnits(900 + (i % 2) * 150),
                skills[i % skills.Length],
                companySeats,
                tick);
        }

        return companyCount;
    }

    private void EnsureHousing(int householdCount)
    {
        var landlords = CompanyLandlords();
        var targetUnits = (int)Math.Ceiling(
            householdCount * (1d + (double)economy.HousingOptions.SeedVacancyRate));
        var total = 0;
        world.Query(in new QueryDescription().WithAll<Property>(), (ref Property _) => total++);

        if (total == 0)
        {
            var shelter = Math.Min(6, targetUnits / 8);
            var luxury = Math.Min(3, targetUnits / 20);
            var comfortable = Math.Min(12, Math.Max(0, (targetUnits - shelter - luxury) / 3));
            var basic = Math.Max(0, targetUnits - shelter - luxury - comfortable);

            if (basic > 0)
                AddBasicUnits(basic, landlords);
            if (comfortable > 0)
                HousingBootstrap.CreateProperty(
                    world, economy, "Central", HousingTier.Comfortable,
                    comfortable, 0.8f, 0.65f, 0, map: map);
            if (luxury > 0)
                HousingBootstrap.CreateProperty(
                    world, economy, "Central", HousingTier.Luxury,
                    luxury, 0.95f, 0.8f, 0, map: map);
            if (shelter > 0)
                HousingBootstrap.CreateShelter(world, economy, "Central", shelter, 0, map);
        }

        var units = 0;
        world.Query(in new QueryDescription().WithAll<HousingUnit>(), (ref HousingUnit _) => units++);
        if (units < targetUnits)
            AddBasicUnits(targetUnits - units, landlords);
    }

    private void AddBasicUnits(int toAdd, IReadOnlyList<Entity> landlords)
    {
        if (toAdd <= 0)
            return;

        if (landlords.Count > 0)
        {
            HousingBootstrap.CreateDistributedProperty(
                world, economy, "Central", HousingTier.Basic, toAdd,
                landlords, 0.6f, 0.5f, 0, map: map);
        }
        else
        {
            HousingBootstrap.CreateProperty(
                world, economy, "Central", HousingTier.Basic, toAdd,
                0.6f, 0.5f, 0, map: map);
        }
    }

    private List<Entity> CompanyLandlords()
    {
        var companies = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<CompanyName, NetworkId>(),
            (Entity entity, ref CompanyName _, ref NetworkId _) => companies.Add(entity));
        companies.Sort((a, b) =>
        {
            var hasA = networkIds.TryGetNetworkId(a, out var idA);
            var hasB = networkIds.TryGetNetworkId(b, out var idB);
            if (hasA && hasB)
                return idA.Value.CompareTo(idB.Value);
            return a.Id.CompareTo(b.Id);
        });
        return companies;
    }

    private static SkillProfile ChildProfile(Random random)
    {
        var profile = new SkillProfile();
        profile.Add(SkillProfile.Families[random.Next(SkillProfile.Families.Length)], 0.18f);
        profile.Add(SkillProfile.Families[random.Next(SkillProfile.Families.Length)], 0.12f);
        return profile;
    }

    private static SkillProfile AdultProfile(Random random) =>
        SkillProfile.FromMask(SkillProfile.Families[random.Next(SkillProfile.Families.Length)], 0.4f + (float)random.NextDouble() * 0.3f);

    private int CountHoused()
    {
        var count = 0;
        foreach (var (genos, household) in economy.Housing.HouseholdByGenos)
        {
            if (!world.IsAlive(genos) || !world.Has<Alive>(genos) || world.Get<Alive>(genos).Age < economy.Life.Policy.WorkStartAge)
                continue;
            if (economy.Housing.UnitOf(household) != Entity.Null)
                count++;
        }
        return count;
    }
}
