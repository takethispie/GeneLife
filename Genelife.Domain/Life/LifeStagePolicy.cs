using Genelife.Enums;

namespace Genelife.Life;

/// <summary>
/// Age boundaries for the lifecycle. Bands must be ascending and the retirement
/// band must not overlap the elderly band.
/// </summary>
public sealed class LifeStagePolicy
{
    public int SchoolStartAge { get; set; } = 6;
    public int WorkStartAge { get; set; } = 18;
    public int RetirementAge { get; set; } = 65;
    public int ElderlyAge { get; set; } = 80;
    public int MaxAge { get; set; } = 110;

    public static LifeStagePolicy Default { get; } = new();

    public LifeStage StageFor(int age)
    {
        if (age < WorkStartAge)
            return LifeStage.Education;
        if (age < RetirementAge)
            return LifeStage.Work;
        if (age < ElderlyAge)
            return LifeStage.Retirement;
        return LifeStage.Elderly;
    }

    public int EntryAge(LifeStage stage) => stage switch
    {
        LifeStage.Education => 0,
        LifeStage.Work => WorkStartAge,
        LifeStage.Retirement => RetirementAge,
        LifeStage.Elderly => ElderlyAge,
        _ => 0
    };

    public LifeStage Next(LifeStage stage) => stage switch
    {
        LifeStage.Education => LifeStage.Work,
        LifeStage.Work => LifeStage.Retirement,
        LifeStage.Retirement => LifeStage.Elderly,
        _ => LifeStage.Elderly
    };

    public StageTransitionCause CauseFor(LifeStage target) => target switch
    {
        LifeStage.Work => StageTransitionCause.Graduated,
        LifeStage.Retirement => StageTransitionCause.Retired,
        LifeStage.Elderly => StageTransitionCause.BecameElderly,
        _ => StageTransitionCause.AgedUp
    };

    public void Validate()
    {
        if (SchoolStartAge < 0)
            throw new InvalidOperationException("School start age cannot be negative.");
        if (WorkStartAge <= SchoolStartAge)
            throw new InvalidOperationException("Work start age must be greater than school start age.");
        if (RetirementAge <= WorkStartAge)
            throw new InvalidOperationException("Retirement age must be greater than work start age.");
        if (ElderlyAge <= RetirementAge)
            throw new InvalidOperationException("Elderly age must be greater than retirement age.");
        if (MaxAge <= ElderlyAge)
            throw new InvalidOperationException("Max age must be greater than elderly age.");
    }
}
