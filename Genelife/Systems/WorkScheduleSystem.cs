using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;

namespace Genelife.Systems;

/// <summary>
/// Starts and ends shifts for Work-stage employees. Commuting is abstract; shifts
/// only change presence, action and performance counters. Plays no role at all
/// for students, retirees, elders or player-controlled sims.
/// </summary>
public class WorkScheduleSystem : ISimulationSystem
{
    private const float InterruptThreshold = 15f;
    private const int ShiftLengthTicks = 75;
    private const int ShiftCooldownTicks = 100;

    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public WorkScheduleSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? 25;
        query = new QueryDescription()
            .WithAll<Employee, Lifecycle, Needs, CurrentAction>()
            .WithNone<Dead>();
    }

    public string Name => "WorkSchedule";
    public int Order => 330;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var missingSchedules = new List<Entity>();
        var events = new List<object>();

        world.Query(in query, (Entity entity, ref Employee employee, ref Lifecycle lifecycle,
            ref Needs needs, ref CurrentAction action) =>
        {
            if (lifecycle.Stage != LifeStage.Work || world.Has<PlayerControlled>(entity))
                return;

            if (!world.Has<WorkSchedule>(entity))
            {
                missingSchedules.Add(entity);
                return;
            }

            var schedule = world.Get<WorkSchedule>(entity);
            var record = world.Has<EmploymentRecord>(entity)
                ? world.Get<EmploymentRecord>(entity)
                : new EmploymentRecord(tick);

            if (schedule.IsAtWork)
            {
                var critical = CriticalNeed(needs);
                if (critical != ActionType.Idle)
                {
                    schedule.IsAtWork = false;
                    schedule.ShiftTicksRemaining = 0;
                    schedule.ShiftsInterrupted++;
                    action = new CurrentAction(ActionType.Idle, 0f);
                    record.Interruptions++;
                    world.Get<WorkSchedule>(entity) = schedule;
                    world.Get<CurrentAction>(entity) = action;
                    if (world.Has<EmploymentRecord>(entity))
                        world.Get<EmploymentRecord>(entity) = record;
                    events.Add(new ShiftInterrupted(LifeSystemHelpers.NetworkIdOf(world, entity), critical.ToString(), tick));
                    return;
                }

                needs.Energy = Math.Max(0f, needs.Energy - 0.05f * interval);
                schedule.ShiftTicksRemaining -= interval;
                if (schedule.ShiftTicksRemaining <= 0)
                {
                    schedule.IsAtWork = false;
                    schedule.ShiftTicksRemaining = 0;
                    schedule.LastShiftEndTick = tick;
                    schedule.ShiftsCompleted++;
                    action = new CurrentAction(ActionType.Idle, 0f);
                    record.ShiftsCompleted++;
                    world.Get<WorkSchedule>(entity) = schedule;
                    world.Get<CurrentAction>(entity) = action;
                    if (world.Has<EmploymentRecord>(entity))
                        world.Get<EmploymentRecord>(entity) = record;
                    events.Add(new ShiftEnded(LifeSystemHelpers.NetworkIdOf(world, entity),
                        LifeSystemHelpers.NetworkIdOf(world, employee.Office), tick));
                }
                else
                {
                    world.Get<WorkSchedule>(entity) = schedule;
                }
                return;
            }

            if (tick - schedule.LastShiftStartTick < ShiftCooldownTicks)
                return;

            schedule.IsAtWork = true;
            schedule.ShiftTicksRemaining = ShiftLengthTicks;
            schedule.LastShiftStartTick = tick;
            action = new CurrentAction(ActionType.Working, 0f);
            record.ShiftsExpected++;
            world.Get<WorkSchedule>(entity) = schedule;
            world.Get<CurrentAction>(entity) = action;
            if (world.Has<EmploymentRecord>(entity))
                world.Get<EmploymentRecord>(entity) = record;

            events.Add(new ShiftStarted(LifeSystemHelpers.NetworkIdOf(world, entity),
                LifeSystemHelpers.NetworkIdOf(world, employee.Office), tick));
        });

        foreach (var entity in missingSchedules)
        {
            if (!world.IsAlive(entity) || world.Has<WorkSchedule>(entity))
                continue;
            if (!world.Has<Employee>(entity))
                continue;
            world.Add(entity, new WorkSchedule(world.Get<Employee>(entity).Office));
            if (!world.Has<EmploymentRecord>(entity))
                world.Add(entity, new EmploymentRecord(tick));
        }

        foreach (var @event in events)
            economy.Emit(@event);
    }

    private static ActionType CriticalNeed(Needs needs)
    {
        if (needs.Energy < InterruptThreshold)
            return ActionType.Sleeping;
        if (needs.Hunger < InterruptThreshold)
            return ActionType.Eating;
        if (needs.Bladder < InterruptThreshold)
            return ActionType.UsingToilet;
        return ActionType.Idle;
    }
}
