using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Family;

using Genelife.Family;

public enum PregnancyStatus : byte
{
    Active,
    ReadyToBirth,
    Completed,
    Lost,
    Cancelled
}

public enum PregnancySource : byte
{
    Conception,
    Admin,
    Migration
}

/// <summary>
/// One pregnancy entity. Completed, lost and cancelled pregnancies remain as
/// history; the entity is never destroyed by the reproduction loop.
/// </summary>
public record struct Pregnancy
{
    public Entity CarryingParent;
    public Entity OtherParent;
    public long ConceivedTick;
    public long DueTick;
    public int RiskBasisPoints;
    public int HealthAtConception;
    public PregnancyStatus Status;
    public PregnancySource Source;
    public long LastProgressTick;
    public int ChildrenExpected;
    public int Complications;
    public Money CarePaid;
    public Money CareArrears;
    public int CareMonths;
    public int MissedCareMonths;
    public long ResolvedTick;
}

/// <summary>Marker on a sim that is still a dependent child.</summary>
public record struct DependentChild;

/// <summary>Birth history attached to the child.</summary>
public record struct BirthRecord
{
    public ulong PregnancyNetworkId;
    public long BornTick;
}
