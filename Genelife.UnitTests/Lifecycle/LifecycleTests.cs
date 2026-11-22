using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Persistence;
using Genelife.Api.Services;
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
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.LifeStages;

public class LifecycleTests
{
    [Fact]
    public void Policy_classifies_boundaries_and_rejects_overlap()
    {
        var policy = new LifeStagePolicy();
        Assert.Equal(LifeStage.Education, policy.StageFor(5));
        Assert.Equal(LifeStage.Work, policy.StageFor(policy.WorkStartAge));
        Assert.Equal(LifeStage.Retirement, policy.StageFor(policy.RetirementAge));
        Assert.Equal(LifeStage.Elderly, policy.StageFor(policy.ElderlyAge));

        var invalid = new LifeStagePolicy { SchoolStartAge = 20, WorkStartAge = 18 };
        Assert.Throws<InvalidOperationException>(invalid.Validate);
    }

    [Fact]
    public void Aging_adds_exactly_one_year_per_year_with_phase_offset()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var clock = economy.Clock;
        var sim = CreateSim(world, economy, "Aging", 20, LifeStage.Work, new SkillProfile());
        SetAgeTicks(world, sim, 20L * clock.TicksPerYear + 500, lastAgedTick: 0);

        var aging = new AgingSystem(world, economy, interval: 1);
        aging.Update(clock.TicksPerYear, clock);
        Assert.Equal(21, world.Get<Alive>(sim).Age);

        aging.Update(2L * clock.TicksPerYear, clock);
        Assert.Equal(22, world.Get<Alive>(sim).Age);
    }

    [Fact]
    public void Aging_graduates_student_to_work_and_creates_job_seeker()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var clock = economy.Clock;
        var sim = CreateSim(world, economy, "Student", 17, LifeStage.Education, new SkillProfile { Programming = 0.6f });
        SetAgeTicks(world, sim, 18L * clock.TicksPerYear - 50, lastAgedTick: 1);

        new AgingSystem(world, economy, interval: 1).Update(clock.TicksPerYear, clock);
        Assert.Equal(18, world.Get<Alive>(sim).Age);

        new StageProgressionSystem(world, economy, interval: 1).Update(clock.TicksPerYear, clock);

        Assert.Equal(LifeStage.Work, world.Get<Lifecycle>(sim).Stage);
        Assert.True(world.Has<JobSeeker>(sim));
        Assert.True(world.Get<JobSeeker>(sim).Skills.HasFlag(Skill.Programming));
        Assert.True(world.Get<JobSeeker>(sim).DesiredSalary.IsPositive);
        Assert.Equal(1, economy.Life.GraduatesThisYear);
    }

    [Fact]
    public void Education_grows_the_school_emphasis()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var school = world.Create(new School("Tech", "Central", 30, 1f, Skill.Programming));
        var sim = CreateSim(world, economy, "Pupil", 10, LifeStage.Education, new SkillProfile());
        world.Add(sim, new Enrollment(school, 0, 1f));
        economy.Life.Index.OnEnrolled(school, unenrolled: false);

        var enrollment = new EnrollmentSystem(world, economy);
        var education = new EducationSystem(world, economy);
        long tick = 0;
        for (var i = 0; i < 120; i++)
        {
            tick += economy.Clock.TicksPerMonth;
            enrollment.Update(tick);
            education.Update(tick);
        }

        var profile = world.Get<SkillProfile>(sim);
        Assert.True(profile.Programming > 0.4f, $"programming={profile.Programming}");
        Assert.True(profile.Programming > profile.Art * 2f, $"art={profile.Art}");
        Assert.True(world.Get<Enrollment>(sim).MonthsAttended >= 120);
    }

    [Fact]
    public void Enrollment_respects_capacity_across_a_monthly_batch()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var school = world.Create(new School("Small", "Central", 1, 1f, Skill.Programming));
        var first = CreateSim(world, economy, "First", 10, LifeStage.Education, new SkillProfile());
        var second = CreateSim(world, economy, "Second", 10, LifeStage.Education, new SkillProfile());

        new EnrollmentSystem(world, economy).Update(economy.Clock.TicksPerMonth);

        Assert.Equal(1, world.Get<School>(school).Enrolled);
        Assert.Equal(1, economy.Life.Index.EnrolledStudents);
        Assert.Equal(1, economy.Life.Index.UnenrolledStudents);

        var firstEnrollment = world.Get<Enrollment>(first);
        var secondEnrollment = world.Get<Enrollment>(second);
        Assert.True(firstEnrollment.IsUnenrolled ^ secondEnrollment.IsUnenrolled);
        var enrolled = firstEnrollment.IsUnenrolled ? secondEnrollment : firstEnrollment;
        Assert.Equal(school, enrolled.School);
    }

    [Fact]
    public void Attendance_updates_and_career_promotes_high_performer()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = CreateCompany(world, "Acme");
        var office = world.Create(new Office("HQ", company, 10, Money.Zero));
        var sim = CreateSim(world, economy, "Worker", 30, LifeStage.Work,
            new SkillProfile { Programming = 0.9f, Engineering = 0.8f });
        world.Add(sim, new Employee(company, office, "Engineer", Money.FromUnits(1000m))
        {
            Seniority = Seniority.Associate,
            HiredAtTick = 0
        });
        world.Add(sim, new EmploymentRecord(0)
        {
            ShiftsCompleted = 4,
            ShiftsExpected = 4,
            TenureCycles = 10,
            Performance = 0.9f,
            HighPerformanceMonths = 12
        });
        economy.Employment.Add(company, sim);

        new AttendanceSystem(world, economy).Update(economy.Clock.TicksPerMonth);
        var record = world.Get<EmploymentRecord>(sim);
        Assert.True(record.Attendance > 0.9f, $"attendance={record.Attendance}");
        Assert.Equal(11, record.TenureCycles);

        var before = world.Get<Employee>(sim).SalaryPerCycle;
        new CareerSystem(world, economy).Update(economy.Clock.TicksPerYear);

        var after = world.Get<Employee>(sim);
        Assert.Equal(Seniority.Specialist, after.Seniority);
        Assert.True(after.SalaryPerCycle > before);
    }

    [Fact]
    public void Retirement_moves_employee_to_pension_and_releases_seat()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        economy.Pension.Balance = Money.FromUnits(50_000m);
        var company = CreateCompany(world, "Acme");
        var office = world.Create(new Office("HQ", company, 10, Money.Zero) { SeatsUsed = 1 });
        var sim = CreateSim(world, economy, "Retiring", 65, LifeStage.Work, new SkillProfile { Sales = 0.6f });
        world.Add(sim, new Employee(company, office, "Seller", Money.FromUnits(1000m))
        {
            HiredAtTick = 0
        });
        world.Add(sim, new EmploymentRecord(0));
        world.Add(sim, new PensionAccount
        {
            EmployeeContributions = Money.FromUnits(4_000m),
            EmployerContributions = Money.FromUnits(4_000m),
            ContributingCycles = 20
        });
        economy.Employment.Add(company, sim);

        var tick = economy.Clock.TicksPerYear;
        new StageProgressionSystem(world, economy).Update(tick, economy.Clock);

        Assert.Equal(LifeStage.Retirement, world.Get<Lifecycle>(sim).Stage);
        Assert.False(world.Has<Employee>(sim));
        Assert.False(world.Has<JobSeeker>(sim));
        Assert.True(world.Has<RetirementIncome>(sim));
        Assert.True(world.Get<RetirementIncome>(sim).MonthlyPension.IsPositive);
        Assert.Equal(0, world.Get<Office>(office).SeatsUsed);

        var walletBefore = world.Get<Wallet>(sim).Balance;
        var pension = new PensionSystem(world, economy);
        pension.Update(tick + economy.Clock.TicksPerMonth, economy.Clock);
        var walletAfter = world.Get<Wallet>(sim).Balance;
        Assert.True(walletAfter > walletBefore, $"{walletBefore} -> {walletAfter}");

        pension.Update(tick + economy.Clock.TicksPerMonth, economy.Clock);
        Assert.Equal(walletAfter, world.Get<Wallet>(sim).Balance);
    }

    [Fact]
    public void Means_tested_retiree_is_paid_by_government()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        economy.Government.Balance = Money.FromUnits(10_000m);
        var sim = CreateSim(world, economy, "NoContributions", 65, LifeStage.Work, new SkillProfile());
        var tick = economy.Clock.TicksPerYear;

        new StageProgressionSystem(world, economy).Update(tick, economy.Clock);
        var income = world.Get<RetirementIncome>(sim);
        Assert.True(income.MeansTested);
        Assert.True(income.MonthlyPension.IsPositive);

        var fundBefore = economy.Pension.Balance;
        var governmentBefore = economy.Government.Balance;
        new PensionSystem(world, economy).Update(tick + economy.Clock.TicksPerMonth, economy.Clock);

        Assert.Equal(fundBefore, economy.Pension.Balance);
        Assert.True(economy.Government.Balance < governmentBefore);
        Assert.True(world.Get<Wallet>(sim).Balance.IsPositive);
    }

    [Fact]
    public void Health_declines_and_zero_health_death_pays_heir()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var sim = CreateSim(world, economy, "Elder", 90, LifeStage.Elderly, new SkillProfile());
        var health = world.Get<Health>(sim);
        health.Value = 100f;
        world.Get<Health>(sim) = health;

        new HealthSystem(world, economy).Update(economy.Clock.TicksPerMonth);
        Assert.True(world.Get<Health>(sim).Value < 100f);

        var heir = CreateSim(world, economy, "Heir", 40, LifeStage.Work, new SkillProfile());
        var household = world.Create(new Household
        {
            Head = sim,
            Unit = Entity.Null,
            Tenure = Tenure.Renter,
            MemberCount = 2
        });
        world.Add(household, new NetworkId(900));
        world.Add(sim, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(heir, new HouseholdMember(household, HouseholdRole.Child));
        economy.Housing.AddMember(sim, household);
        economy.Housing.AddMember(heir, household);
        world.Get<Wallet>(sim).Balance = Money.FromUnits(1_000m);
        world.Get<Wallet>(heir).Balance = Money.Zero;

        health = world.Get<Health>(sim);
        health.Value = 0f;
        world.Get<Health>(sim) = health;

        var tick = 2L * economy.Clock.TicksPerYear;
        new MortalitySystem(world, economy).Update(tick);
        Assert.True(world.Has<Dead>(sim));

        new DeathProcessingSystem(world, economy).Update(tick);
        new EstateSettlementSystem(world, economy).Update(tick);

        Assert.Equal(Money.Zero, world.Get<Wallet>(sim).Balance);
        Assert.Equal(Money.FromUnits(900m), world.Get<Wallet>(heir).Balance);
        Assert.True(economy.Life.MortalityLog.Count > 0);
        Assert.True(economy.Government.Balance.IsZero);
    }

    [Fact]
    public void Population_inflow_waits_for_housing_then_admits()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        economy.Life.AttachNetworkIdAllocator(entity => new NetworkId((ulong)entity.Id));
        economy.Life.Immigration.TargetPopulation = 50;
        economy.Life.Immigration.AnnualArrivalShare = 0.5m;
        economy.Life.ImmigrationState.ArrivalBudget = 10;

        var inflow = new PopulationInflowSystem(world, economy);
        var clock = economy.Clock;
        var tick = (long)clock.TicksPerMonth;
        inflow.Update(tick, clock);

        Assert.Equal(0, economy.Life.Index.TotalLiving);
        Assert.True(economy.Life.ImmigrationState.Pending.Count > 0);

        for (var i = 0; i < economy.Life.Immigration.PendingExpiryMonths + 1; i++)
        {
            tick += clock.TicksPerMonth;
            inflow.Update(tick, clock);
        }
        Assert.True(economy.Life.ImmigrationState.CancelledThisYear > 0);

        HousingBootstrap.CreateStock(world, economy, tick);
        tick += clock.TicksPerMonth;
        inflow.Update(tick, clock);

        Assert.True(economy.Life.Index.TotalLiving > 0);
        Assert.True(economy.Life.ImmigrationState.ArrivalsThisYear > 0);
        Assert.Empty(economy.Housing.Verify());
    }

    [Fact]
    public void Population_generator_creates_a_consistent_pyramid()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        var result = engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world,
                engine.NetworkIds,
                engine.Economy!,
                engine.EmploymentIndex!);
            return generator.Generate(60, seed: 7);
        });

        Assert.True(result.Total == 60, result.ToString());
        Assert.Equal(result.Total, result.Education + result.Work + result.Retirement + result.Elderly);
        Assert.True(result.Schools > 0);
        Assert.True(result.Employed >= 0);

        engine.ExecuteWithWorld(world =>
        {
            world.Query(in new QueryDescription().WithAll<Lifecycle, Alive>().WithNone<Dead>(),
                (Entity entity, ref Lifecycle lifecycle, ref Alive alive) =>
                {
                    Assert.Equal(engine.Economy!.Life.Policy.StageFor(alive.Age), lifecycle.Stage);
                    if (lifecycle.Stage == LifeStage.Education)
                        Assert.False(world.Has<Employee>(entity));
                    if (lifecycle.Stage is LifeStage.Retirement or LifeStage.Elderly)
                    {
                        Assert.False(world.Has<Employee>(entity));
                        Assert.True(world.Has<RetirementIncome>(entity));
                    }
                });
            return 0;
        });
    }

    [Fact]
    public void Full_tick_path_graduates_and_retires_without_errors()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var clock = engine.Economy!.Clock;

        engine.ExecuteWithWorld(world =>
        {
            var student = world.Create(
                new GenName("Tick Student"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(17),
                new Location(0f, 0f),
                new Wallet(Money.Zero),
                new ConsumptionState(),
                new SkillProfile { Programming = 0.5f },
                new Health(100f),
                LifecycleFactory.Create(17, 0, clock));
            var life = world.Get<Lifecycle>(student);
            life.AgeTicks = 18L * clock.TicksPerYear - 50;
            world.Get<Lifecycle>(student) = life;
            world.Add(student, engine.NetworkIds.Allocate(student));
            engine.Economy.Life.Index.OnBorn(LifeStage.Education);

            var worker = world.Create(
                new GenName("Tick Worker"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(64),
                new Location(0f, 0f),
                new Wallet(Money.FromUnits(500m)),
                new ConsumptionState(),
                new SkillProfile { Sales = 0.6f },
                new Health(100f),
                LifecycleFactory.Create(64, 0, clock),
                new PensionAccount
                {
                    EmployeeContributions = Money.FromUnits(5_000m),
                    EmployerContributions = Money.FromUnits(5_000m),
                    ContributingCycles = 20
                });
            var workerLife = world.Get<Lifecycle>(worker);
            workerLife.AgeTicks = 65L * clock.TicksPerYear - 50;
            world.Get<Lifecycle>(worker) = workerLife;
            world.Add(worker, engine.NetworkIds.Allocate(worker));
            engine.Economy.Life.Index.OnBorn(LifeStage.Work);
            return 0;
        });

        for (var i = 0; i < 120; i++)
            engine.Tick(1f / 20f);

        engine.ExecuteWithWorld(world =>
        {
            world.Query(in new QueryDescription().WithAll<GenName, Lifecycle>(),
                (Entity entity, ref GenName name, ref Lifecycle lifecycle) =>
                {
                    if (name.Name == "Tick Student")
                        Assert.Equal(LifeStage.Work, lifecycle.Stage);
                    if (name.Name == "Tick Worker")
                        Assert.Equal(LifeStage.Retirement, lifecycle.Stage);
                });
            Assert.Empty(LifecycleInvariants.Verify(world, engine.Economy!.Life.Policy));
            return 0;
        });
    }

    [Fact]
    public void Death_reassigns_head_and_transfers_an_owned_home()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        HousingBootstrap.CreateStock(world, economy, 0);

        var head = CreateSim(world, economy, "Owner", 75, LifeStage.Retirement, new SkillProfile());
        var heir = CreateSim(world, economy, "Heir", 40, LifeStage.Work, new SkillProfile());
        var household = world.Create(new Household
        {
            Head = head,
            Unit = Entity.Null,
            Tenure = Tenure.Owner,
            MemberCount = 2
        });
        world.Add(household, new NetworkId(1_000));
        world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(heir, new HouseholdMember(household, HouseholdRole.Relative));
        economy.Housing.AddMember(head, household);
        economy.Housing.AddMember(heir, household);

        Entity unit = Entity.Null;
        world.Query(in new QueryDescription().WithAll<HousingUnit>(),
            (Entity entity, ref HousingUnit data) =>
            {
                if (unit == Entity.Null && data.IsVacant)
                    unit = entity;
            });
        Assert.NotEqual(Entity.Null, unit);

        economy.Housing.MoveIn(unit, household);
        economy.Housing.TransferUnit(unit, household);
        var householdData = world.Get<Household>(household);
        householdData.Tenure = Tenure.Owner;
        world.Get<Household>(household) = householdData;

        world.Add(head, new Dead(500, DeathCause.Illness));
        economy.Life.DeathQueue.Add(head);
        new DeathProcessingSystem(world, economy).Update(600);
        new EstateSettlementSystem(world, economy).Update(600);

        Assert.Equal(heir, world.Get<Household>(household).Head);
        Assert.Equal(heir, world.Get<HousingUnit>(unit).Owner);
    }

    [Fact]
    public void Restore_settles_a_pending_dead_estate_before_destroying_the_sim()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var clock = engine.Economy!.Clock;

        engine.ExecuteWithWorld(world =>
        {
            var head = world.Create(
                new GenName("Pending Estate"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(75),
                new Location(0f, 0f),
                new Wallet(Money.FromUnits(1_000m)),
                new ConsumptionState(),
                new SkillProfile(),
                new Health(0f),
                LifecycleFactory.Create(75, 0, clock),
                new Dead(500, DeathCause.Illness));
            world.Add(head, engine.NetworkIds.Allocate(head));

            var heir = world.Create(
                new GenName("Estate Heir"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(40),
                new Location(0f, 0f),
                new Wallet(Money.Zero),
                new ConsumptionState(),
                new SkillProfile(),
                new Health(100f),
                LifecycleFactory.Create(40, 0, clock));
            world.Add(heir, engine.NetworkIds.Allocate(heir));
            engine.Economy.Life.Index.OnBorn(LifeStage.Elderly);
            engine.Economy.Life.Index.OnBorn(LifeStage.Work);

            var household = world.Create(new Household
            {
                Head = head,
                Unit = Entity.Null,
                Tenure = Tenure.Renter,
                MemberCount = 2
            });
            world.Add(household, engine.NetworkIds.Allocate(household));
            world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
            world.Add(heir, new HouseholdMember(household, HouseholdRole.Relative));
            engine.Economy.Housing.AddMember(head, household);
            engine.Economy.Housing.AddMember(heir, household);
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        restored.ExecuteWithWorld(world =>
        {
            Entity heir = Entity.Null;
            world.Query(in new QueryDescription().WithAll<GenName>(),
                (Entity entity, ref GenName name) =>
                {
                    if (name.Name == "Estate Heir")
                        heir = entity;
                });

            Assert.True(world.IsAlive(heir));
            Assert.Equal(Money.FromUnits(900m), world.Get<Wallet>(heir).Balance);
            Assert.True(restored.Economy!.Life.MortalityLog.Count > 0);
            return 0;
        });
    }

    [Fact]
    public void Legacy_snapshot_classifies_ages_and_cleans_impossible_roles()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var clock = engine.Economy!.Clock;

        engine.ExecuteWithWorld(world =>
        {
            var company = world.Create(new CompanyName("Legacy Co"));
            world.Add(company, engine.NetworkIds.Allocate(company));
            var office = world.Create(new Office("Legacy HQ", company, 10, Money.Zero));
            world.Add(office, engine.NetworkIds.Allocate(office));

            var elder = world.Create(
                new GenName("Legacy Elder"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(72),
                new Location(0f, 0f),
                new Wallet(Money.FromUnits(100m)),
                new ConsumptionState(),
                new SkillProfile { Sales = 0.5f },
                new Health(60f),
                new Employee(company, office, "Seller", Money.FromUnits(900m)),
                new EmploymentRecord(0),
                new PensionAccount
                {
                    EmployeeContributions = Money.FromUnits(4_000m),
                    EmployerContributions = Money.FromUnits(4_000m),
                    ContributingCycles = 30
                },
                LifecycleFactory.Create(72, 0, clock));
            world.Add(elder, engine.NetworkIds.Allocate(elder));
            engine.EmploymentIndex!.Add(company, elder);
            engine.Economy.Life.Index.OnBorn(LifeStage.Work);
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        state.Tick = 5_000;
        state.LifecycleMigrated = false;
        foreach (var sim in state.Sims)
            sim.Stage = string.Empty;

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        restored.ExecuteWithWorld(world =>
        {
            Entity elder = Entity.Null;
            world.Query(in new QueryDescription().WithAll<GenName>(),
                (Entity entity, ref GenName name) =>
                {
                    if (name.Name == "Legacy Elder")
                        elder = entity;
                });

            Assert.True(world.IsAlive(elder));
            Assert.Equal(LifeStage.Retirement, world.Get<Lifecycle>(elder).Stage);
            Assert.Equal(5_000, world.Get<Lifecycle>(elder).LastAgedTick);
            Assert.False(world.Has<Employee>(elder));
            Assert.True(world.Has<RetirementIncome>(elder));
            Assert.Empty(LifecycleInvariants.Verify(world, restored.Economy!.Life.Policy));
            return 0;
        });
    }

    [Fact]
    public void Ten_year_population_scenario_stays_consistent()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world,
                engine.NetworkIds,
                engine.Economy!,
                engine.EmploymentIndex!);
            generator.Generate(60, seed: 21);
            return 0;
        });

        var start = engine.Economy!.Life.Index.TotalLiving;
        var ticks = 10 * engine.Economy.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(1f / 20f);

        engine.ExecuteWithWorld(world =>
        {
            Assert.Empty(LifecycleInvariants.Verify(world, engine.Economy!.Life.Policy));
            Assert.Empty(engine.Economy.Housing.Verify());
            Assert.InRange(engine.Economy.Life.Index.TotalLiving, (int)(start * 0.5), (int)(start * 1.8));
            Assert.True(engine.Economy.Life.MetricHistory.Count >= 9,
                $"snapshots={engine.Economy.Life.MetricHistory.Count}");
            Assert.True(engine.Economy.Pension.Balance.Units >= 0m);
            return 0;
        });
    }

    [Fact]
    public void Lifecycle_round_trips_through_snapshot()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var clock = engine.Economy!.Clock;

        engine.ExecuteWithWorld(world =>
        {
            var school = world.Create(new School("Snapshot School", "Central", 20, 0.7f, Skill.Programming));
            world.Add(school, engine.NetworkIds.Allocate(school));

            var worker = world.Create(
                new GenName("Snapshot Sim"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(32),
                new Location(0f, 0f),
                new Wallet(Money.FromUnits(750m)),
                new ConsumptionState(),
                new SkillProfile { Programming = 0.7f, Sales = 0.4f },
                new Health(82f),
                LifecycleFactory.Create(32, 500, clock));
            world.Add(worker, engine.NetworkIds.Allocate(worker));
            engine.Economy.Life.Index.OnBorn(LifeStage.Work);

            var student = world.Create(
                new GenName("Snapshot Student"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(10),
                new Location(0f, 0f),
                new Wallet(Money.Zero),
                new ConsumptionState(),
                new SkillProfile { Art = 0.2f },
                new Health(95f),
                new Enrollment(school, 500, 0.9f),
                LifecycleFactory.Create(10, 500, clock));
            world.Add(student, engine.NetworkIds.Allocate(student));
            world.Get<School>(school).Enrolled = 1;
            engine.Economy.Life.Index.OnBorn(LifeStage.Education);
            engine.Economy.Life.Index.OnEnrolled(school, unenrolled: false);

            var retiree = world.Create(
                new GenName("Snapshot Retiree"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(70),
                new Location(0f, 0f),
                new Wallet(Money.FromUnits(200m)),
                new ConsumptionState(),
                new SkillProfile { Medical = 0.3f },
                new Health(70f),
                new PensionAccount
                {
                    EmployeeContributions = Money.FromUnits(3_000m),
                    EmployerContributions = Money.FromUnits(3_000m),
                    ContributingCycles = 30
                },
                new RetirementIncome(Money.FromUnits(55m), 400, "statement"),
                LifecycleFactory.Create(70, 500, clock));
            world.Add(retiree, engine.NetworkIds.Allocate(retiree));
            engine.Economy.Life.Index.OnBorn(LifeStage.Retirement);
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        Assert.Equal(PersistedWorldState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.True(state.LifecycleMigrated);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        var restoredWorld = restored.World!;
        Entity sim = Entity.Null;
        restoredWorld.Query(in new QueryDescription().WithAll<GenName>(),
            (Entity entity, ref GenName name) =>
            {
                if (name.Name == "Snapshot Sim")
                    sim = entity;
            });

        Assert.True(restoredWorld.IsAlive(sim));
        Assert.Equal(LifeStage.Work, restoredWorld.Get<Lifecycle>(sim).Stage);
        Assert.Equal(82f, restoredWorld.Get<Health>(sim).Value);
        Assert.Equal(0.7f, restoredWorld.Get<SkillProfile>(sim).Programming, 3);
        Assert.Equal(Money.FromUnits(750m), restoredWorld.Get<Wallet>(sim).Balance);

        Entity student = Entity.Null;
        Entity retiree = Entity.Null;
        restoredWorld.Query(in new QueryDescription().WithAll<GenName>(),
            (Entity entity, ref GenName name) =>
            {
                if (name.Name == "Snapshot Student")
                    student = entity;
                if (name.Name == "Snapshot Retiree")
                    retiree = entity;
            });
        Assert.True(restoredWorld.IsAlive(student));
        Assert.Equal(LifeStage.Education, restoredWorld.Get<Lifecycle>(student).Stage);
        Assert.True(restoredWorld.Has<Enrollment>(student));
        Assert.False(restoredWorld.Get<Enrollment>(student).IsUnenrolled);
        Assert.True(restoredWorld.IsAlive(retiree));
        Assert.Equal(LifeStage.Retirement, restoredWorld.Get<Lifecycle>(retiree).Stage);
        Assert.Equal(Money.FromUnits(55m), restoredWorld.Get<RetirementIncome>(retiree).MonthlyPension);
        Assert.Empty(LifecycleInvariants.Verify(restoredWorld, restored.Economy!.Life.Policy));
        Assert.Equal(3, restored.Economy!.Life.Index.TotalLiving);
    }

    private static Entity CreateSim(
        World world,
        EconomyContext economy,
        string name,
        int age,
        LifeStage stage,
        SkillProfile profile)
    {
        var lifecycle = LifecycleFactory.Create(age, 0, economy.Clock, economy.Life.Policy);
        lifecycle.AgeTicks = (long)age * economy.Clock.TicksPerYear;
        lifecycle.Stage = stage;

        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f),
            new Wallet(Money.FromUnits(100m)),
            new ConsumptionState(),
            profile,
            new Health(HealthSystem.InitialForAge(age)),
            lifecycle);
        economy.Life.Index.OnBorn(stage);
        return entity;
    }

    private static void SetAgeTicks(World world, Entity sim, long ageTicks, long lastAgedTick)
    {
        var lifecycle = world.Get<Lifecycle>(sim);
        lifecycle.AgeTicks = ageTicks;
        lifecycle.LastAgedTick = lastAgedTick;
        world.Get<Lifecycle>(sim) = lifecycle;
    }

    private static Entity CreateCompany(World world, string name)
    {
        var company = world.Create(new CompanyName(name));
        var account = CompanyAccount.Create(Money.FromUnits(100_000m), 0);
        account.State = CompanySolvency.Healthy;
        world.Add(company, account);
        return company;
    }
}
