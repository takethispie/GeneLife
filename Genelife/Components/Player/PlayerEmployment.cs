using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Player;

public enum PlayerEmploymentSource : byte
{
    PlayerApplied = 1,
    PlayerAcceptedOffer = 2,
    AdminAssigned = 3
}

/// <summary>
/// Player-specific employment state. The <see cref="Employee"/> component stays
/// the authoritative employment relationship so payroll, attendance, career and
/// termination work exactly as they do for NPCs.
/// </summary>
public record struct PlayerEmployment
{
    public Entity Office;
    public long HiredTick;
    public long LastAttendedTick;
    public int ConsecutiveMissedShifts;
    public PlayerEmploymentSource Source;
    public bool AttendedThisMonth;

    public PlayerEmployment(Entity office, long hiredTick, PlayerEmploymentSource source)
    {
        Office = office;
        HiredTick = hiredTick;
        LastAttendedTick = 0;
        ConsecutiveMissedShifts = 0;
        Source = source;
        AttendedThisMonth = false;
    }
}

/// <summary>
/// A deterministic, expiring job offer produced by a successful player
/// application. Accepting the offer reuses the normal hiring path.
/// </summary>
public record struct PlayerJobOffer
{
    public Guid OfferId;
    public Entity Posting;
    public Entity Office;
    public string Position;
    public Money SalaryPerCycle;
    public long CreatedTick;
    public long ExpiresTick;
}
