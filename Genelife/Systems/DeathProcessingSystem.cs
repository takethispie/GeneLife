using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Single-pass death cleanup: relationships, indices, jobs, school slots,
/// household membership and pending applications. The entity is destroyed after
/// the estate has had a chance to settle.
/// </summary>
public class DeathProcessingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipService? relationships;
    private readonly RelationshipIndex? relationshipIndex;
    private readonly GuardianshipService? guardianship;
    private readonly InheritanceOptions inheritanceOptions;
    private readonly int interval;

    public DeathProcessingSystem(
        World world,
        EconomyContext economy,
        int? interval = null,
        RelationshipService? relationships = null,
        RelationshipIndex? relationshipIndex = null,
        GuardianshipService? guardianship = null,
        InheritanceOptions? inheritanceOptions = null)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.relationshipIndex = relationshipIndex;
        this.guardianship = guardianship;
        this.inheritanceOptions = inheritanceOptions ?? InheritanceOptions.Default;
        this.interval = interval ?? 1;
    }

    public string Name => "DeathProcessing";
    public int Order => 576;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var life = economy.Life;
        if (!life.HasPendingDestroy && life.DeathQueue.Count == 0)
            return;

        life.ProcessPendingDestroy(tick);
        if (life.DeathQueue.Count == 0)
            return;

        var queue = life.DeathQueue.Count == 1
            ? [life.DeathQueue[0]]
            : life.DeathQueue.Distinct().ToList();
        life.DeathQueue.Clear();
        var applications = PendingApplicationsByApplicant();

        foreach (var entity in queue)
        {
            if (!world.IsAlive(entity) || !world.Has<Lifecycle>(entity))
                continue;

            Process(entity, tick, applications);
        }
    }

    private Dictionary<Entity, List<Entity>> PendingApplicationsByApplicant()
    {
        var applications = new Dictionary<Entity, List<Entity>>();
        world.Query(in new QueryDescription().WithAll<JobApplication>(),
            (Entity entity, ref JobApplication application) =>
            {
                if (application.Status != ApplicationStatus.Pending)
                    return;
                if (!applications.TryGetValue(application.Applicant, out var list))
                {
                    list = new List<Entity>();
                    applications[application.Applicant] = list;
                }
                list.Add(entity);
            });
        return applications;
    }

    private void Process(Entity entity, long tick, Dictionary<Entity, List<Entity>> applicationsByApplicant)
    {
        if (relationships is not null && relationshipIndex is not null)
        {
            foreach (var partner in relationshipIndex.PartnersOf(entity).ToList())
                relationships.EndPartnership(entity, partner, tick);
        }

        var life = economy.Life;
        var lifecycle = world.Get<Lifecycle>(entity);
        var alive = world.Has<Alive>(entity) ? world.Get<Alive>(entity) : new Alive();
        var cause = world.Has<Dead>(entity) ? world.Get<Dead>(entity).Cause : DeathCause.Other;

        if (!world.Has<Dead>(entity))
            world.Add(entity, new Dead(tick, cause));

        var networkId = LifeSystemHelpers.NetworkIdOf(world, entity);
        var name = world.Has<GenName>(entity) ? world.Get<GenName>(entity).Name : string.Empty;
        var household = LifeSystemHelpers.HouseholdOf(world, economy, entity);
        var unit = household != Entity.Null ? economy.Housing.UnitOf(household) : Entity.Null;
        var heirs = new List<Entity>();
        if (relationshipIndex is not null)
        {
            heirs.AddRange(HeirResolver.Resolve(
                    world,
                    relationshipIndex,
                    entity,
                    inheritanceOptions,
                    economy.Life.Policy)
                .Allocations
                .Select(allocation => allocation.Heir));
        }
        else if (household != Entity.Null)
        {
            foreach (var member in economy.Housing.GetMembers(household))
            {
                if (member != entity && world.IsAlive(member))
                    heirs.Add(member);
            }
        }

        var wealth = Money.Zero;
        if (world.Has<Wallet>(entity))
            wealth += world.Get<Wallet>(entity).Balance;
        if (world.Has<SavingsAccount>(entity))
            wealth += world.Get<SavingsAccount>(entity).Balance;

        var employerNetworkId = world.Has<Employee>(entity)
            ? LifeSystemHelpers.NetworkIdOf(world, world.Get<Employee>(entity).Company)
            : 0UL;

        LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, entity, tick);
        LifeSystemHelpers.RemoveJobSearch(world, entity);
        RemoveIfPresent<WorkSchedule>(entity);
        RemoveIfPresent<EmploymentRecord>(entity);
        RemoveIfPresent<RetirementIncome>(entity);
        RemoveIfPresent<UnemploymentRecord>(entity);

        EnrollmentHelpers.ReleaseSeat(world, entity, economy.Life.Index);

        RejectApplications(entity, applicationsByApplicant);

        if (household != Entity.Null && economy.Housing.HouseholdBySim.ContainsKey(entity))
            economy.Housing.RemoveMember(entity);
        if (world.Has<HouseholdMember>(entity))
            world.Remove<HouseholdMember>(entity);
        if (world.Has<Residence>(entity))
            world.Remove<Residence>(entity);

        if (household != Entity.Null && economy.Housing.MemberCount(household) == 0 &&
            unit != Entity.Null && world.IsAlive(unit))
            economy.Housing.MoveOut(unit, household);

        var householdData = household != Entity.Null && world.IsAlive(household) && world.Has<Household>(household)
            ? world.Get<Household>(household)
            : default;

        var ownedUnit = householdData.Tenure == Tenure.Owner && unit != Entity.Null && world.IsAlive(unit)
            ? unit
            : Entity.Null;

        guardianship?.ReassignAfterGuardianDeath(entity, tick);
        if (household != Entity.Null)
            guardianship?.EnsureHouseholdMinors(household, tick);

        ReassignHead(household, entity, tick);

        var householdArrears = household != Entity.Null && world.IsAlive(household) && world.Has<Household>(household)
            ? world.Get<Household>(household)
            : default;

        life.MortalityLog.Add(new MortalityRecord(
            tick,
            networkId,
            name,
            alive.Age,
            cause,
            wealth.Units));
        const int maxMortalityRecords = 2_000;
        if (life.MortalityLog.Count > maxMortalityRecords)
            life.MortalityLog.RemoveRange(0, life.MortalityLog.Count - maxMortalityRecords);

        economy.Emit(new SimDied(
            networkId,
            name,
            alive.Age,
            cause.ToString(),
            employerNetworkId,
            household == Entity.Null ? 0UL : LifeSystemHelpers.NetworkIdOf(world, household),
            wealth.Cents,
            tick));
        economy.Emit(new EstateSettlementRequested(networkId, tick));

        life.EstateQueue.Add(new EstateRequest
        {
            Sim = entity,
            Household = household,
            OwnedUnit = ownedUnit,
            Unit = unit,
            Tick = tick,
            Name = name,
            SimNetworkId = networkId,
            Age = alive.Age,
            Cause = cause,
            Heirs = heirs,
            HouseholdRentArrears = householdArrears.RentArrears.Units,
            HouseholdUtilityArrears = householdArrears.UtilityArrears.Units
        });

        life.Index.OnDied(lifecycle.Stage);
        life.CriticalSince.Remove(entity);
        life.QueueDestroy(entity, tick + economy.Clock.TicksPerMonth + 1);
    }

    private void ReassignHead(Entity household, Entity deceased, long tick)
    {
        if (household == Entity.Null || !world.IsAlive(household) || !world.Has<Household>(household))
            return;

        var data = world.Get<Household>(household);
        if (data.Head != deceased)
            return;

        Entity newHead = Entity.Null;
        if (relationshipIndex is not null)
        {
            newHead = relationshipIndex.ExPartnersOf(deceased)
                .Concat(relationshipIndex.PartnersOf(deceased))
                .FirstOrDefault(candidate =>
                    candidate != deceased && world.IsAlive(candidate) && !world.Has<Dead>(candidate));
        }

        if (newHead == Entity.Null)
        {
            foreach (var member in economy.Housing.GetMembers(household))
            {
                if (member == deceased || !world.IsAlive(member) || world.Has<Dead>(member))
                    continue;

                if (newHead == Entity.Null)
                    newHead = member;

                if (world.Has<HouseholdMember>(member) &&
                    world.Get<HouseholdMember>(member).Role is HouseholdRole.Partner or HouseholdRole.Relative)
                {
                    newHead = member;
                    break;
                }
            }
        }

        data.Head = newHead;
        world.Get<Household>(household) = data;

        if (newHead != Entity.Null && world.Has<HouseholdMember>(newHead))
            world.Get<HouseholdMember>(newHead).Role = HouseholdRole.Head;

        economy.Emit(new HouseholdHeadChanged(
            LifeSystemHelpers.NetworkIdOf(world, household),
            newHead == Entity.Null ? 0UL : LifeSystemHelpers.NetworkIdOf(world, newHead),
            tick));
    }

    private void RejectApplications(Entity sim, Dictionary<Entity, List<Entity>> applicationsByApplicant)
    {
        if (!applicationsByApplicant.TryGetValue(sim, out var applications))
            return;

        foreach (var entity in applications)
        {
            if (world.IsAlive(entity) && world.Has<JobApplication>(entity))
                world.Get<JobApplication>(entity).Status = ApplicationStatus.Rejected;
        }
    }

    private void RemoveIfPresent<T>(Entity entity) where T : struct
    {
        if (world.Has<T>(entity))
            world.Remove<T>(entity);
    }
}
