using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Life;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Monthly immigration regulator. Arrivals are only admitted when a housing or
/// shelter path exists and are paced by an annual budget. Pending arrivals wait
/// for up to a year, then are cancelled instead of being made homeless.
/// </summary>
public class PopulationInflowSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly MapContext? map;
    private readonly int interval;

    public PopulationInflowSystem(World world, EconomyContext economy, MapContext? map = null, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.map = map;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "PopulationInflow";
    public int Order => 590;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        var life = economy.Life;
        var policy = life.Immigration;
        var state = life.ImmigrationState;

        if (policy.TargetPopulation <= 0 || !policy.Enabled)
            return;

        var current = life.Index.TotalLiving;
        if (clock.IsYearBoundary(tick))
            state.ResetYear(clock.YearOf(tick), policy.AnnualBudget(Math.Max(current, policy.TargetPopulation)));

        if (state.ArrivalBudget == 0)
            state.ArrivalBudget = policy.AnnualBudget(Math.Max(current, policy.TargetPopulation));

        var unemployed = CountUnemployed();
        var workers = life.Index.StageCount(LifeStage.Work);
        var unemploymentRate = workers > 0 ? unemployed / (decimal)workers : 0m;

        if (unemploymentRate > policy.UnemploymentPauseThreshold)
        {
            if (!state.Paused)
            {
                state.Paused = true;
                economy.Emit(new ImmigrationPaused($"unemployment {unemploymentRate:P1}", tick));
            }
            return;
        }

        state.Paused = false;
        ExpirePending(tick, clock, policy, state);

        var openSeats = CountOpenSeats();
        var populationGap = policy.TargetPopulation - (current + state.Pending.Count);
        var workingAgeShare = current > 0 ? workers / (decimal)current : 1m;
        var pyramidPressure = workingAgeShare < policy.MinimumWorkingAgeShare;
        var labourGap = openSeats - unemployed;

        if (populationGap <= 0)
        {
            if (!state.Paused)
            {
                state.Paused = true;
                economy.Emit(new ImmigrationPaused("TargetReached", tick));
            }
            return;
        }

        var allowed = Math.Min(
            Math.Max(1, state.ArrivalBudget / 12),
            Math.Max(0, state.ArrivalBudget - state.ArrivalsThisYear));

        var admitted = 0;
        while (admitted < allowed)
        {
            if (economy.Housing.VacancyCount() <= 0)
                break;

            if (!TryCreateArrival(tick, clock, out _))
                break;
            admitted++;
        }

        if (admitted == 0 && economy.Housing.VacancyCount() <= 0 &&
            state.ArrivalsThisYear + state.Pending.Count < state.ArrivalBudget)
        {
            state.Pending.Add(new PendingArrival
            {
                Name = $"Immigrant {state.ArrivalsThisYear + state.Pending.Count + 1}",
                Age = NextAge(state.ArrivalsThisYear + state.Pending.Count),
                QueuedTick = tick,
                ExpiresTick = tick + policy.PendingExpiryMonths * (long)clock.TicksPerMonth
            });
        }

        if (admitted > 0 && state.ArrivalsThisYear >= state.ArrivalBudget)
            economy.Emit(new ImmigrationPaused("annual budget exhausted", tick));
    }

    private void ExpirePending(long tick, SimulationClock clock, ImmigrationPolicy policy, ImmigrationState state)
    {
        for (var i = state.Pending.Count - 1; i >= 0; i--)
        {
            var pending = state.Pending[i];
            if (economy.Housing.VacancyCount() > 0 && TryCreateArrival(tick, clock, out _, pending))
            {
                state.Pending.RemoveAt(i);
                continue;
            }

            if (tick >= pending.ExpiresTick)
            {
                state.Pending.RemoveAt(i);
                state.CancelledThisYear++;
                economy.Emit(new ArrivalCancelled(pending.Name, pending.Age, tick));
            }
        }
    }

    private bool TryCreateArrival(long tick, SimulationClock clock, out Entity sim, PendingArrival? pending = null)
    {
        sim = Entity.Null;
        var life = economy.Life;
        if (life.ImmigrationState.ArrivalsThisYear >= life.ImmigrationState.ArrivalBudget &&
            life.ImmigrationState.ArrivalBudget > 0)
            return false;
        var policy = life.Immigration;
        var random = life.Random.For($"arrival.{tick}.{life.ImmigrationState.ArrivalsThisYear}");

        var age = pending?.Age ?? random.Next(policy.MinArrivalAge, policy.MaxArrivalAge + 1);
        var profile = pending?.Profile ?? ProfileForOpenRole(random);
        var name = pending?.Name ?? $"Immigrant {life.ImmigrationState.ArrivalsThisYear + 1}";

        sim = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f),
            new Wallet(policy.ImmigrantStartingCash),
            new ConsumptionState(),
            LifecycleFactory.Create(age, tick, clock, life.Policy, age * 31 + life.ImmigrationState.ArrivalsThisYear),
            profile,
            new Health(HealthSystem.InitialForAge(age)),
            new JobSeeker(true, profile.ToMask(), life.Careers.SalaryForSkills(profile.Total)));

        life.AllocateNetworkId(sim);
        var networkIdValue = LifeSystemHelpers.NetworkIdOf(world, sim);
        var traits = TraitsGenerator.Create(
            economy.Random.Seed, networkIdValue, TraitSource.Immigrant, economy.Genetics);
        world.Add(sim, traits);
        AptitudeSkills.ApplyStartingBonus(ref profile, traits.Aptitude);
        world.Get<Components.Life.SkillProfile>(sim) = profile;
        if (world.Has<JobSeeker>(sim))
        {
            ref var seeker = ref world.Get<JobSeeker>(sim);
            seeker = new JobSeeker(true, profile.ToMask(), seeker.DesiredSalary);
        }

        if (map is not null)
            MapSpawner.PlaceSim(world, sim, map, networkIdValue);

        var household = world.Create(new Household
        {
            Head = sim,
            Unit = Entity.Null,
            Tenure = Tenure.Renter,
            MoveInTick = tick,
            MemberCount = 1
        });
        life.AllocateNetworkId(household);

        world.Add(sim, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(sim, household);

        var allocator = new HousingAllocator(world, economy);
        if (!allocator.TryAssignBest(household, tick))
        {
            economy.Housing.RemoveMember(sim);
            if (world.Has<HouseholdMember>(sim))
                world.Remove<HouseholdMember>(sim);
            world.Destroy(household);
            world.Destroy(sim);
            sim = Entity.Null;
            return false;
        }

        var networkId = LifeSystemHelpers.NetworkIdOf(world, sim);
        economy.Emit(new SimCreated(networkId, name, age));
        if (policy.ImmigrantStartingCash.IsPositive)
        {
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                Components.Finance.LedgerKind.Grant,
                null,
                networkId,
                policy.ImmigrantStartingCash,
                "immigration.grant");
        }

        life.Index.OnBorn(LifeStage.Work);
        life.ArrivalsThisYear++;
        life.ImmigrationState.ArrivalsThisYear++;
        economy.Emit(new ImmigrantArrived(networkId, age, policy.ImmigrantStartingCash.Cents, tick));
        return true;
    }

    private int NextAge(int index)
    {
        var policy = economy.Life.Immigration;
        var random = economy.Life.Random.For($"arrival.age.{economy.Tick}.{index}");
        return random.Next(policy.MinArrivalAge, policy.MaxArrivalAge + 1);
    }

    private Components.Life.SkillProfile ProfileForOpenRole(Random random)
    {
        var requirements = new List<Skill>();
        world.Query(in new QueryDescription().WithAll<JobPosting>(),
            (ref JobPosting posting) =>
            {
                if (!posting.IsClosed && posting.Requirements != Skill.None)
                    requirements.Add(posting.Requirements);
            });

        if (requirements.Count > 0)
            return Components.Life.SkillProfile.FromMask(requirements[random.Next(requirements.Count)], 0.4f);

        var families = Components.Life.SkillProfile.Families;
        return Components.Life.SkillProfile.FromMask(families[random.Next(families.Length)], 0.4f);
    }

    private int CountOpenSeats()
    {
        var seats = 0;
        world.Query(in new QueryDescription().WithAll<OpenPosition>(), (ref OpenPosition _) => seats++);
        return seats;
    }

    private int CountUnemployed()
    {
        var count = 0;
        var query = new QueryDescription()
            .WithAll<Lifecycle, JobSeeker>()
            .WithNone<Employee, Dead>();
        world.Query(in query, (ref Lifecycle lifecycle) =>
        {
            if (lifecycle.Stage == LifeStage.Work)
                count++;
        });
        return count;
    }
}
