using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Map;
using Genelife.Components.Map;

namespace Genelife.Application.Systems;

/// <summary>
/// Starts and ends shifts for Work-stage employees. Commuting is abstract; shifts
/// only change presence, action and performance counters. Plays no role at all
/// for students, retirees, elders or player-controlled genoses.
/// </summary>
public class WorkScheduleSystem : ISimulationSystem
{
    private const float InterruptThreshold = 15f;
    private const int ShiftLengthTicks = 75;
    private const int ShiftCooldownTicks = 100;

    private readonly World world;
    private readonly EconomyContext economy;
    private readonly MapContext? map;
    private readonly MovementIndex? movementIndex;
    private readonly CommuteOptions commute;
    private readonly QueryDescription query;
    private readonly int interval;

    public WorkScheduleSystem(
        World world,
        EconomyContext economy,
        MapContext? map = null,
        MovementIndex? movementIndex = null,
        CommuteOptions? commute = null,
        int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.map = map;
        this.movementIndex = movementIndex;
        this.commute = commute ?? CommuteOptions.Default;
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
                    MoveHome(entity);
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
                    MoveHome(entity);
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
            MoveToOffice(entity, employee.Office);
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

    private void MoveToOffice(Entity genos, Entity office)
    {
        if (map is null || office == Entity.Null || !world.IsAlive(office))
            return;
        if (world.Has<Placement>(office))
            MoveTo(genos, world.Get<Placement>(office).Entry);
    }

    private void MoveHome(Entity genos)
    {
        if (map is null)
            return;

        if (economy.Housing.TryGetUnitForGenos(genos, out var unit) &&
            world.IsAlive(unit) &&
            world.Has<Genelife.Components.Housing.HousingUnit>(unit))
        {
            var property = world.Get<Genelife.Components.Housing.HousingUnit>(unit).Property;
            if (world.IsAlive(property) && world.Has<Placement>(property))
            {
                MoveTo(genos, world.Get<Placement>(property).Entry);
                return;
            }
        }

        var id = world.Has<NetworkId>(genos) ? world.Get<NetworkId>(genos).Value : 0;
        MoveTo(genos, MapSpawner.FindSpawn(map, id));
    }

    private void MoveTo(Entity genos, TileCoord tile)
    {
        if (!map!.Map.IsWalkable(tile) || !world.Has<TilePosition>(genos))
            return;

        if (commute.Mode == CommuteMode.Abstract || movementIndex is null)
        {
            world.Get<TilePosition>(genos) = new TilePosition(tile);
            if (world.Has<Location>(genos))
            {
                var (x, y) = map.Options.ToWorld(tile);
                world.Get<Location>(genos) = new Location(x, y);
            }
            return;
        }

        var current = world.Get<TilePosition>(genos).Tile;
        if (current == tile)
            return;

        if (!world.Has<MovementState>(genos))
            world.Add(genos, new MovementState(commute.SpeedTilesPerSecond));
        var target = new MoveTarget(tile);
        if (world.Has<MoveTarget>(genos))
            world.Get<MoveTarget>(genos) = target;
        else
            world.Add(genos, target);

        movementIndex.RequestPath(genos, current, tile, map.Version);
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
