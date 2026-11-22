namespace Genelife.Enums;

/// <summary>
/// The four lifecycle stages. Children are the early part of
/// <see cref="Education"/>; there is no separate child stage.
/// </summary>
public enum LifeStage
{
    Education,
    Work,
    Retirement,
    Elderly
}

public enum StageTransitionCause
{
    AgedUp,
    Graduated,
    Retired,
    BecameElderly,
    Death,
    AdminOverride,
    Restored
}

public enum DeathCause
{
    Age,
    Illness,
    Starvation,
    Exposure,
    Other
}

public enum Seniority
{
    Associate,
    Specialist,
    Senior,
    Lead,
    Manager,
    Director
}
