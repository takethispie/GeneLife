using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Monthly school enrollment, capacity ranking, attendance and public school
/// operating cost. Children without capacity are explicitly unenrolled and learn
/// at the reduced informal rate in <see cref="EducationSystem"/>.
/// </summary>
public class EnrollmentSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription studentsQuery;
    private readonly QueryDescription schoolsQuery;
    private readonly int interval;

    public EnrollmentSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        studentsQuery = new QueryDescription()
            .WithAll<Lifecycle, Alive>()
            .WithNone<Dead>();
        schoolsQuery = new QueryDescription().WithAll<School>();
    }

    public string Name => "Enrollment";
    public int Order => 310;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var policy = economy.Life.Policy;
        var schools = new List<(Entity Entity, School School)>();
        world.Query(in schoolsQuery, (Entity entity, ref School school) => schools.Add((entity, school)));

        schools.Sort((a, b) =>
        {
            var byQuality = b.School.Quality.CompareTo(a.School.Quality);
            return byQuality != 0 ? byQuality : a.Entity.Id.CompareTo(b.Entity.Id);
        });

        var newStudents = new List<Entity>();
        var existing = new List<(Entity Entity, Enrollment Enrollment, Enrollment Updated)>();

        world.Query(in studentsQuery, (Entity entity, ref Lifecycle lifecycle, ref Alive alive) =>
        {
            if (lifecycle.Stage != Enums.LifeStage.Education)
                return;

            var attendance = TargetAttendance(entity, alive);
            if (world.Has<Enrollment>(entity))
            {
                var current = world.Get<Enrollment>(entity);
                var updated = current;
                updated.Attendance = current.IsUnenrolled
                    ? Math.Clamp(0.7f * current.Attendance + 0.3f * attendance * 0.5f, 0f, 1f)
                    : Math.Clamp(0.7f * current.Attendance + 0.3f * attendance, 0f, 1f);
                updated.MonthsAttended = current.MonthsAttended + 1;
                existing.Add((entity, current, updated));
                return;
            }

            if (alive.Age < policy.SchoolStartAge)
                return;

            newStudents.Add(entity);
        });

        foreach (var (entity, _, updated) in existing)
        {
            if (world.IsAlive(entity) && world.Has<Enrollment>(entity))
                world.Get<Enrollment>(entity) = updated;
        }

        foreach (var entity in newStudents)
        {
            if (!world.IsAlive(entity) || world.Has<Enrollment>(entity))
                continue;

            var schoolEntity = Entity.Null;
            foreach (var (candidate, _) in schools)
            {
                if (world.IsAlive(candidate) && world.Has<School>(candidate) &&
                    world.Get<School>(candidate).HasCapacity)
                {
                    schoolEntity = candidate;
                    break;
                }
            }

            if (schoolEntity == Entity.Null)
            {
                world.Add(entity, new Enrollment(Entity.Null, tick, 0.5f, isUnenrolled: true));
                economy.Life.Index.OnEnrolled(Entity.Null, unenrolled: true);
                economy.Emit(new StudentUnenrolled(LifeSystemHelpers.NetworkIdOf(world, entity), tick));
                continue;
            }

            ref var school = ref world.Get<School>(schoolEntity);
            school.Enrolled++;
            var schoolNetworkId = LifeSystemHelpers.NetworkIdOf(world, schoolEntity);
            world.Add(entity, new Enrollment(schoolEntity, tick, 1f));
            economy.Life.Index.OnEnrolled(schoolEntity, unenrolled: false);
            economy.Emit(new StudentEnrolled(LifeSystemHelpers.NetworkIdOf(world, entity), schoolNetworkId, tick));

            if (!school.HasCapacity)
                economy.Emit(new SchoolCapacityReached(schoolNetworkId, school.Capacity, tick));
        }

        ChargeSchoolCosts(tick, schools);
    }

    private float TargetAttendance(Entity entity, Alive alive)
    {
        var attendance = 0.85f;
        if (world.Has<Health>(entity))
            attendance *= Math.Clamp(world.Get<Health>(entity).Value / 100f + 0.35f, 0.2f, 1f);

        if (world.Has<NeedsModifier>(entity))
        {
            var modifier = world.Get<NeedsModifier>(entity);
            if (modifier.IsHomeless)
                attendance -= 0.35f;
            if (modifier.NoUtilities)
                attendance -= 0.1f;
            if (modifier.Overcrowded)
                attendance -= 0.1f;
        }

        if (world.Has<Needs>(entity))
        {
            var needs = world.Get<Needs>(entity);
            var lowest = Math.Min(Math.Min(needs.Hunger, needs.Energy), Math.Min(needs.Hygiene, needs.Bladder));
            if (lowest < 20f)
                attendance -= 0.2f;
        }

        return Math.Clamp(attendance, 0.15f, 1f);
    }

    private void ChargeSchoolCosts(long tick, List<(Entity Entity, School School)> schools)
    {
        foreach (var (entity, school) in schools)
        {
            if (!world.IsAlive(entity) || !world.Has<School>(entity))
                continue;

            var enrolled = world.Get<School>(entity).Enrolled;
            if (!school.MonthlyCost.IsPositive || enrolled <= 0)
                continue;

            var due = school.MonthlyCost * enrolled;
            if (school.Owner != Entity.Null && world.IsAlive(school.Owner) && world.Has<CompanyAccount>(school.Owner))
            {
                var account = world.Get<CompanyAccount>(school.Owner);
                var paid = account.Balance.Min(due);
                if (paid.IsPositive &&
                    economy.Accounts.TryDebit(
                        school.Owner,
                        paid,
                        LedgerKind.SchoolCost,
                        Guid.NewGuid(),
                        EconomyAccountIds.Government,
                        "school.cost",
                        tick))
                {
                    economy.Government.Balance += paid;
                }
                continue;
            }

            // Public education is provided in kind: the government must be
            // solvent enough to fund it, but no cash leaves the public account.
            var publicPaid = due.Min(economy.Government.Balance);
            if (!publicPaid.IsPositive)
                continue;

            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.SchoolCost,
                EconomyAccountIds.Government,
                EconomyAccountIds.Government,
                publicPaid,
                "school.cost.public");
        }
    }
}
