using Arch.Core;

namespace Genelife.Components.Life;

/// <summary>
/// Daily work presence for an employee. Commuting is abstract: shifts start and
/// end at cadence boundaries with no pathfinding.
/// </summary>
public record struct WorkSchedule
{
    public Entity Office;
    public bool IsAtWork;
    public int ShiftTicksRemaining;
    public long LastShiftStartTick;
    public long LastShiftEndTick;
    public int ShiftsCompleted;
    public int ShiftsInterrupted;

    public WorkSchedule(Entity office)
    {
        Office = office;
        IsAtWork = false;
        ShiftTicksRemaining = 0;
        LastShiftStartTick = 0;
        LastShiftEndTick = 0;
        ShiftsCompleted = 0;
        ShiftsInterrupted = 0;
    }
}
