using Genelife.Enums;

namespace Genelife.Components.Life;

/// <summary>
/// Single source of truth for a living sim's lifecycle state. AgeTicks is the
/// authoritative accumulator; <see cref="Components.Gen.Alive.Age"/> remains the
/// public integer age used by existing code and read models.
/// </summary>
public record struct Lifecycle
{
    public LifeStage Stage;
    public long AgeTicks;
    public long StageEnteredTick;
    public StageTransitionCause LastCause;
    public long LastAgedTick;
    public bool MaxAgeFlagged;

    public Lifecycle(
        LifeStage stage,
        long ageTicks,
        long stageEnteredTick,
        StageTransitionCause lastCause = StageTransitionCause.AgedUp,
        long lastAgedTick = 0)
    {
        Stage = stage;
        AgeTicks = ageTicks;
        StageEnteredTick = stageEnteredTick;
        LastCause = lastCause;
        LastAgedTick = lastAgedTick;
        MaxAgeFlagged = false;
    }
}
