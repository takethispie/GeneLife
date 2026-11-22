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
using Genelife.Housing;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Systems;
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

    public PopulationGenerator(
        World world,
        NetworkIdRegistry networkIds,
        EconomyContext economy,
        EmploymentIndex employmentIndex)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
        this.employmentIndex = employmentIndex;
    }

    public PopulationGenerationResult Generate(
        int totalSims,
        int seed = 1,
        int? adults = null,
        int? children = null,
        long tick = 0)
    {
        if (totalSims <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalSims), "Total sims must be positive.");

        var random = new Random(seed);
        if (!economy.Government.Balance.IsPositive)
            economy.Government.Balance = Money.FromUnits(500_000m);
        var childCount = children ?? Math.Max(0, totalSims * 22 / 100);
        var adultCount = adults ?? Math.Max(0, totalSims - childCount);
        if (childCount + adultCount > totalSims)
            adultCount = Math.Max(0, totalSims - childCount);

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
            education.Add(CreateSim($"Student {i + 1}", age, LifeStage.Education, random, tick,
                new Money(0), profile: ChildProfile(random)));
        }

        for (var i = 0; i < workCount; i++)
        {
            var age = random.Next(economy.Life.Policy.WorkStartAge, economy.Life.Policy.RetirementAge);
            workers.Add(CreateSim($"Worker {i + 1}", age, LifeStage.Work, random, tick,
                Money.FromUnits(400 + random.Next(0, 200)), profile: AdultProfile(random), lookingForWork: true));
        }

        for (var i = 0; i < retirementCount; i++)
        {
            var age = random.Next(economy.Life.Policy.RetirementAge, economy.Life.Policy.ElderlyAge);
            retirees.Add(CreateSim($"Retiree {i + 1}", age, LifeStage.Retirement, random, tick,
                Money.FromUnits(400 + random.Next(0, 400)), profile: AdultProfile(random), retired: true));
        }

        for (var i = 0; i < elderlyCount; i++)
        {
            var age = random.Next(economy.Life.Policy.ElderlyAge, Math.Min(96, economy.Life.Policy.MaxAge));
            elders.Add(CreateSim($"Elder {i + 1}", age, LifeStage.Elderly, random, tick,
                Money.FromUnits(200 + random.Next(0, 400)), profile: AdultProfile(random), retired: true));
        }

        var households = BuildHouseholds(workers, retirees, elders, education, random, tick);
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
            $"{result.Total} sims, {companyCount} companies, {result.Housed}/{result.Employed + result.Retirement + result.Elderly} adults housed"));
        return result;
    }

    private Entity CreateSim(
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
        economy.Emit(new SimCreated(networkId.Value, name, age));

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
        Random random,
        long tick)
    {
        var adults = workers.Concat(retirees).Concat(elders).ToList();
        var households = new List<Entity>();
        var allocator = new HousingAllocator(world, economy);

        foreach (var adult in adults)
        {
            var household = world.Create(new Household
            {
                Head = adult,
                Unit = Entity.Null,
                Tenure = Tenure.Renter,
                MoveInTick = tick,
                MemberCount = 1
            });
            world.Add(household, networkIds.Allocate(household));
            world.Add(adult, new HouseholdMember(household, HouseholdRole.Head));
            economy.Housing.AddMember(adult, household);
            allocator.TryAssignBest(household, tick);
            households.Add(household);
        }

        if (households.Count > 0)
        {
            foreach (var child in children)
            {
                var household = households[random.Next(households.Count)];
                var data = world.Get<Household>(household);
                data.MemberCount++;
                world.Get<Household>(household) = data;

                world.Add(child, new HouseholdMember(household, HouseholdRole.Child));
                economy.Housing.AddMember(child, household);
            }
        }

        return households.Count;
    }

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
            if (posting.IsClosed)
                world.Destroy(postingEntity);
            foreach (var seat in seats)
            {
                if (world.IsAlive(seat))
                    world.Destroy(seat);
            }
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
        world.Add(entity, networkIds.Allocate(entity));
        return entity;
    }

    private int EnsureCompanies(int workCount, long tick)
    {
        var existing = 0;
        world.Query(in new QueryDescription().WithAll<CompanyName>(), (ref CompanyName _) => existing++);
        if (existing > 0)
            return existing;

        var companyRepository = new CompanyRepository(world, employmentIndex, networkIds, economy);
        var postingRepository = new JobPostingRepository(world, networkIds, economy);
        var seatsPerCompany = Math.Max(4, Math.Min(10, workCount / 2));
        var skills = new[] { Skill.Sales, Skill.Programming, Skill.Engineering };

        for (var i = 0; i < 2; i++)
        {
            var companyId = companyRepository.Add(i == 0 ? "Northwind" : "Bluebird");
            if (!networkIds.TryGetEntity(companyId, out var company))
                continue;
            if (world.Has<CompanyAccount>(company))
            {
                ref var account = ref world.Get<CompanyAccount>(company);
                account.Balance = Money.FromUnits(1_000_000m);
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
                i == 0 ? "Sales Associate" : "Engineer",
                Money.FromUnits(900 + i * 150),
                skills[i % skills.Length],
                seatsPerCompany,
                tick);
        }

        return 2;
    }

    private void EnsureHousing(int adultCount)
    {
        var total = 0;
        world.Query(in new QueryDescription().WithAll<Property>(), (ref Property _) => total++);
        if (total == 0)
            HousingBootstrap.CreateStock(world, economy, 0);

        var vacancy = economy.Housing.VacancyCount();
        if (vacancy < adultCount)
        {
            HousingBootstrap.CreateProperty(
                world,
                economy,
                "Central",
                HousingTier.Basic,
                adultCount - vacancy,
                0.6f,
                0.5f,
                0);
        }
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
        foreach (var (sim, household) in economy.Housing.HouseholdBySim)
        {
            if (!world.IsAlive(sim) || !world.Has<Alive>(sim) || world.Get<Alive>(sim).Age < economy.Life.Policy.WorkStartAge)
                continue;
            if (economy.Housing.UnitOf(household) != Entity.Null)
                count++;
        }
        return count;
    }
}
