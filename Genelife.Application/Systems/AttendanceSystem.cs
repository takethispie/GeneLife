using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;

namespace Genelife.Application.Systems;

/// <summary>
/// Monthly attendance and performance review. Produces eligibility signals
/// (warnings, high performance) and never fires an employee directly; the
/// economy owns the employment relationship.
/// </summary>
public class AttendanceSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public AttendanceSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        query = new QueryDescription()
            .WithAll<Employee, Lifecycle, EmploymentRecord>()
            .WithNone<Dead>();
    }

    public string Name => "Attendance";
    public int Order => 340;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var missingRecords = new List<Entity>();
        var events = new List<object>();

        world.Query(in query, (Entity entity, ref Employee employee, ref Lifecycle lifecycle,
            ref EmploymentRecord record) =>
        {
            if (lifecycle.Stage != LifeStage.Work)
                return;

            var expected = record.ShiftsExpected;
            var monthlyAttendance = expected > 0
                ? Math.Clamp((record.ShiftsCompleted + record.Interruptions * 0.5f) / expected, 0f, 1f)
                : 1f;

            record.Attendance = Math.Clamp(0.7f * record.Attendance + 0.3f * monthlyAttendance, 0f, 1f);

            var profile = world.Has<SkillProfile>(entity) ? world.Get<SkillProfile>(entity) : new SkillProfile();
            var skillFit = Math.Clamp(profile.Total / 2.5f, 0f, 1f);
            var tenureFactor = Math.Clamp(record.TenureCycles / 8f, 0f, 1f);
            var variance = (float)economy.Life.Random
                .For($"performance.{LifeSystemHelpers.NetworkIdOf(world, entity)}.{tick}")
                .NextDouble();

            var healthFactor = world.Has<Health>(entity)
                ? 0.8f + 0.2f * world.Get<Health>(entity).Value / 100f
                : 1f;
            var performance = (0.4f * record.Attendance
                               + 0.3f * skillFit
                               + 0.2f * tenureFactor
                               + 0.1f * variance) * healthFactor;

            record.Performance = Math.Clamp(performance, 0f, 1f);
            record.TenureCycles++;
            record.LastReviewTick = tick;

            if (record.Performance < 0.35f)
                record.ConsecutiveLowMonths++;
            else
                record.ConsecutiveLowMonths = 0;

            if (record.ConsecutiveLowMonths >= 2)
            {
                record.Warnings++;
                record.ConsecutiveLowMonths = 0;
                events.Add(new EmployeeWarned(
                    LifeSystemHelpers.NetworkIdOf(world, entity),
                    record.Warnings,
                    record.Performance,
                    tick));
            }

            record.HighPerformanceMonths = record.Performance > 0.75f
                ? record.HighPerformanceMonths + 1
                : 0;

            var completed = record.ShiftsCompleted;
            var expectedShifts = record.ShiftsExpected;
            record.ResetMonthCounters();
            world.Get<EmploymentRecord>(entity) = record;
            events.Add(new AttendanceRecorded(
                LifeSystemHelpers.NetworkIdOf(world, entity),
                record.Attendance,
                completed,
                expectedShifts,
                tick));
            events.Add(new PerformanceReview(
                LifeSystemHelpers.NetworkIdOf(world, entity),
                record.Performance,
                record.Warnings,
                tick));
        });

        world.Query(in new QueryDescription().WithAll<Employee>().WithNone<EmploymentRecord, Dead>(),
            (Entity entity, ref Employee _) => missingRecords.Add(entity));

        foreach (var entity in missingRecords)
        {
            if (world.IsAlive(entity) && !world.Has<EmploymentRecord>(entity))
            {
                var hired = world.Has<Employee>(entity) ? world.Get<Employee>(entity).HiredAtTick : tick;
                world.Add(entity, new EmploymentRecord(hired));
            }
        }

        foreach (var @event in events)
            economy.Emit(@event);
    }
}
